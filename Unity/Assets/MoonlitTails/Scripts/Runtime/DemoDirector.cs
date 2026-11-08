#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MoonlitTails.Drinks;
using MoonlitTails.Gameplay;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;

namespace MoonlitTails.EditorTools
{
    /// <summary>
    /// The script for the editor's DemoVideoRecorder (editor only: it lives here because Unity can't add a component
    /// from an Editor folder): a camera that eases between shots, props moved by an invisible
    /// hand through the real game rules (pouring, shaking, stirring, ice, garnish, serving, reset), and every frame
    /// piped into ffmpeg. Game time runs at exactly 1/30 s per frame, so the video plays at real speed however slow
    /// the rendering is. Captions are burned in at the end.
    /// </summary>
    [DefaultExecutionOrder(10000)]   // after the floating labels have turned to face the camera
    public class DemoDirector : MonoBehaviour
    {
        public const int Fps = 30;
        public int width = 1920, height = 1080;
        public Action<bool> Done;

        // output
        Camera cam;
        RenderTexture rt;
        Texture2D frame;
        Process ffmpeg;
        Stream pipe;
        int frames;
        string dir, rawPath, finalPath;
        bool failed;
        float Now => frames / (float)Fps;

        // camera: eases toward the goal shot
        Vector3 camPos, camLook, goalPos, goalLook;
        float camFov = 60f, goalFov = 60f, ease = 2.2f;

        // captions
        readonly List<(float start, float end, string text)> captions = new List<(float, float, string)>();
        string captionText;
        float captionStart;

        // the bar
        Vector3 serve, player, eye, fwd, right;
        DrinkGame game;
        ServeStation station;
        OrderBook orders;
        readonly Dictionary<Transform, Quaternion> upright = new Dictionary<Transform, Quaternion>();
        static readonly FieldInfo LabelCam = typeof(FloatingLabel).GetField("cam", BindingFlags.NonPublic | BindingFlags.Instance);

        // ------------------------------------------------------------------ setup and capture
        void Start()
        {
            Time.captureDeltaTime = 1f / Fps;
            dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "Demo"));
            Directory.CreateDirectory(dir);
            rawPath = Path.Combine(dir, "demo_raw.mp4");
            finalPath = Path.Combine(dir, "moonlit_tails_week2.mp4");

            // the simulator would move the rig and hands around; Play mode changes are thrown away afterwards
            foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (mb && mb.GetType().Name.Contains("Simulator")) mb.gameObject.SetActive(false);

            game = FindFirstObjectByType<DrinkGame>();
            station = FindFirstObjectByType<ServeStation>();
            orders = FindFirstObjectByType<OrderBook>();
            serve = Find(DrinkGame.ServeSpot).position;
            player = Find(DrinkGame.PlayerSpot).position;
            fwd = serve - player; fwd.y = 0; fwd.Normalize();
            right = Vector3.Cross(Vector3.up, fwd);
            eye = player + Vector3.up * 1.6f;
            foreach (var p in PropRespawn.All) upright[p.transform] = p.transform.rotation;

            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) c.enabled = false;
            cam = new GameObject("Demo Camera").AddComponent<Camera>();
            cam.enabled = false;   // rendered by hand below, once per frame
            cam.nearClipPlane = 0.02f; cam.farClipPlane = 80f;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.06f, 0.14f);
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            cam.targetTexture = rt;
            cam.aspect = (float)width / height;
            frame = new Texture2D(width, height, TextureFormat.RGB24, false);

            StartFfmpeg();
            StartCoroutine(Safe(Script()));
        }

        /// <summary>Runs the script (and the steps it waits on) so that an exception ends the video instead of hanging it.</summary>
        IEnumerator Safe(IEnumerator root)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                bool moved;
                try { moved = stack.Peek().MoveNext(); }
                catch (Exception e) { Debug.LogException(e); failed = true; Finish(); yield break; }
                if (!moved) { stack.Pop(); continue; }
                if (stack.Peek().Current is IEnumerator inner) { stack.Push(inner); continue; }
                yield return stack.Peek().Current;
            }
        }

        void StartFfmpeg()
        {
            var info = new ProcessStartInfo("ffmpeg",
                $"-y -loglevel error -f rawvideo -pix_fmt rgb24 -s {width}x{height} -r {Fps} -i - " +
                $"-vf vflip -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p \"{rawPath}\"")
            {
                UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true,
            };
            ffmpeg = Process.Start(info);
            pipe = ffmpeg.StandardInput.BaseStream;
        }

        void LateUpdate()
        {
            if (cam == null || pipe == null) return;
            float k = 1f - Mathf.Exp(-ease * Time.deltaTime);
            camPos = Vector3.Lerp(camPos, goalPos, k);
            camLook = Vector3.Lerp(camLook, goalLook, k);
            camFov = Mathf.Lerp(camFov, goalFov, k);
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(camLook - camPos, Vector3.up));
            cam.fieldOfView = camFov;

            // labels face whatever camera they last saw: make them face this one, every frame
            foreach (var l in FindObjectsByType<FloatingLabel>(FindObjectsSortMode.None))
            {
                LabelCam.SetValue(l, cam);
                Vector3 away = l.transform.position - camPos;
                if (away.sqrMagnitude > 1e-6f) l.transform.rotation = Quaternion.LookRotation(away, Vector3.up);
            }

            cam.Render();
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = prev;
            var bytes = frame.GetRawTextureData<byte>();
            try { pipe.Write(bytes.ToArray(), 0, bytes.Length); }
            catch (Exception e) { Debug.LogError("[Moonlit] Demo: ffmpeg stopped taking frames: " + e.Message); failed = true; Finish(); return; }
            frames++;
        }

        void Finish()
        {
            if (pipe == null) return;
            Caption(null);
            try { pipe.Close(); } catch { }
            pipe = null;
            ffmpeg.WaitForExit();
            string err = ffmpeg.StandardError.ReadToEnd();
            if (ffmpeg.ExitCode != 0) { Debug.LogError("[Moonlit] Demo: ffmpeg failed: " + err); failed = true; }
            Time.captureDeltaTime = 0;

            if (!failed)
            {
                string srt = Path.Combine(dir, "captions.srt");
                File.WriteAllText(srt, Srt());
                // burn the captions in: a soft box along the bottom
                string style = "FontName=DejaVu Sans,FontSize=15,PrimaryColour=&H00F5F8FF,OutlineColour=&H90140E10,BorderStyle=3,Outline=7,Shadow=0,MarginV=22";
                var burn = Process.Start(new ProcessStartInfo("ffmpeg",
                    $"-y -loglevel error -i \"{rawPath}\" -vf \"subtitles=captions.srt:force_style='{style}'\" " +
                    $"-c:v libx264 -preset medium -crf 20 -pix_fmt yuv420p -movflags +faststart \"{finalPath}\"")
                { UseShellExecute = false, RedirectStandardError = true, WorkingDirectory = dir, CreateNoWindow = true });
                string burnErr = burn.StandardError.ReadToEnd();
                burn.WaitForExit();
                if (burn.ExitCode != 0) { Debug.LogError("[Moonlit] Demo: burning captions failed: " + burnErr); failed = true; }
            }
            Debug.Log(failed ? "[Moonlit] Demo: failed" : $"[Moonlit] Demo: {frames} frames ({Now:F1} s) -> {finalPath}");
            Done?.Invoke(!failed);
        }

        void OnDestroy()
        {
            if (rt) rt.Release();
            Time.captureDeltaTime = 0;
        }

        // ------------------------------------------------------------------ captions
        void Caption(string text)
        {
            if (captionText != null && Now > captionStart) captions.Add((captionStart, Now, captionText));
            captionText = text;
            captionStart = Now;
        }

        string Srt()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < captions.Count; i++)
            {
                var (s, e, t) = captions[i];
                sb.AppendLine((i + 1).ToString());
                sb.AppendLine($"{Stamp(s)} --> {Stamp(e)}");
                sb.AppendLine(t);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static string Stamp(float t)
        {
            var ts = TimeSpan.FromSeconds(t);
            return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00},{3:000}", (int)ts.TotalHours, ts.Minutes, ts.Seconds, ts.Milliseconds);
        }

        // ------------------------------------------------------------------ camera
        void Shot(Vector3 pos, Vector3 look, float fov = 60f, float speed = 2.2f)
        {
            goalPos = pos; goalLook = look; goalFov = fov; ease = speed;
        }

        void Cut(Vector3 pos, Vector3 look, float fov = 60f)
        {
            camPos = goalPos = pos; camLook = goalLook = look; camFov = goalFov = fov;
        }

        /// <summary>From the player's eye, zoomed so the bounds fill most of the frame.</summary>
        void FromEye(Bounds b, float fill = 0.75f, Vector3? from = null)
        {
            Vector3 p = from ?? eye;
            float d = Mathf.Max(0.3f, Vector3.Distance(p, b.center));
            float fov = Mathf.Clamp(2f * Mathf.Atan(b.extents.magnitude / fill / d) * Mathf.Rad2Deg, 8f, 70f);
            Shot(p, b.center, fov);
        }

        /// <summary>A close look at the counter around a point, from the player's side, slightly above.</summary>
        void CounterShot(Vector3 at, float back = 0.5f, float up = 0.62f, float side = 0f, float fov = 50f)
        {
            Shot(at - fwd * back + Vector3.up * up + right * side, at + Vector3.up * 0.06f, fov);
        }

        // ------------------------------------------------------------------ props
        Transform Find(string n) => GameObject.Find("MoonlitBar").GetComponentsInChildren<Transform>(true).First(t => t.name == n);
        DrinkContainer Vessel(ContainerKind k) => DrinkContainer.All.First(c => c.kind == k);
        DrinkContainer GlassOf(string v) => DrinkContainer.All.First(c => c.IsGlass && c.variant == v);
        IngredientSource Source(Ingredient i) => FindObjectsByType<IngredientSource>(FindObjectsSortMode.None).First(s => s.Ingredient == i);

        Quaternion Upright(Component c) => upright.TryGetValue(c.transform, out var q) ? q : c.transform.rotation;

        static void Hold(Component c)
        {
            if (!c.TryGetComponent<Rigidbody>(out var rb)) return;
            if (!rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
        }

        static void SetPose(Component c, Vector3 pos, Quaternion rot)
        {
            c.transform.SetPositionAndRotation(pos, rot);
            if (c.TryGetComponent<Rigidbody>(out var rb)) { rb.position = pos; rb.rotation = rot; }
            Physics.SyncTransforms();
        }

        /// <summary>Where to put a prop, upright, so its base stands on the counter at a point.</summary>
        Vector3 OnCounter(Component c, Vector3 at)
        {
            var keep = (c.transform.position, c.transform.rotation);
            c.transform.rotation = Upright(c);
            var b = MeshBounds(c.transform);
            Vector3 pos = c.transform.position + new Vector3(at.x - b.center.x, serve.y + 0.002f - b.min.y, at.z - b.center.z);
            c.transform.SetPositionAndRotation(keep.position, keep.rotation);
            return pos;
        }

        /// <summary>Bounds of the visible meshes only (a prop's hidden pour stream sits at the world origin).</summary>
        static Bounds MeshBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<MeshRenderer>().Where(r => r.enabled).ToArray();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Carry a prop to a pose, lifted a little on the way like a hand would.</summary>
        IEnumerator Move(Component c, Vector3 to, Quaternion rot, float seconds, float lift = 0.08f)
        {
            Hold(c);
            Vector3 from = c.transform.position; Quaternion r0 = c.transform.rotation;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float u = Mathf.SmoothStep(0, 1, t / seconds);
                SetPose(c, Vector3.Lerp(from, to, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * lift, Quaternion.Slerp(r0, rot, u));
                yield return null;
            }
            SetPose(c, to, rot);
        }

        IEnumerator SetDown(Component c, Vector3 at, float seconds = 0.8f) => Move(c, OnCounter(c, at), Upright(c), seconds);

        /// <summary>Bring a bottle or cup over a container, tip it until one pour lands, then tilt it back.</summary>
        IEnumerator Pour(Component c, DrinkContainer into, int pours = 1)
        {
            var spout = c.GetComponent<PourSpout>();
            Quaternion up = Upright(c);
            // tip toward the container, seen side-on from the player
            Vector3 toward = into.OpeningWorld - c.transform.position; toward.y = 0;
            Vector3 axis = Vector3.Cross(Vector3.up, toward.sqrMagnitude > 1e-4f ? toward.normalized : right);
            Vector3 mouth = into.OpeningWorld + Vector3.up * 0.1f;

            Hold(c);
            SetPose(c, c.transform.position, c.transform.rotation);
            var keep = c.transform.rotation; c.transform.rotation = up;
            Vector3 over = c.transform.position + (mouth + Vector3.up * 0.06f - spout.SpoutWorld);
            c.transform.rotation = keep;
            yield return Move(c, over, up, 0.9f, 0.1f);

            for (int n = 0; n < pours; n++)
            {
                yield return Tilt(c, spout, axis, mouth, 0f, 130f, 0.7f);
                yield return Wait(1.0f);   // a pour takes half a second past 100 degrees
                yield return Tilt(c, spout, axis, mouth, 130f, n < pours - 1 ? 40f : 0f, 0.5f);
                if (n < pours - 1) yield return Wait(0.3f);
            }
        }

        IEnumerator Tilt(Component c, PourSpout spout, Vector3 axis, Vector3 mouth, float a0, float a1, float seconds)
        {
            Quaternion up = Upright(c);
            for (float t = 0; t <= seconds; t += Time.deltaTime)
            {
                float a = Mathf.Lerp(a0, a1, Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / seconds)));
                c.transform.rotation = Quaternion.AngleAxis(a, axis) * up;
                // keep the mouth over the container while turning, a little higher when upright
                Vector3 target = mouth + Vector3.up * 0.06f * (1f - Mathf.Clamp01(a / 100f));
                SetPose(c, c.transform.position + (target - spout.SpoutWorld), c.transform.rotation);
                yield return null;
            }
        }

        static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        IEnumerator Shake(DrinkContainer shaker, float seconds = 1.8f)
        {
            Hold(shaker);
            Vector3 home = shaker.transform.position + Vector3.up * 0.12f;
            Quaternion up = Upright(shaker);
            yield return Move(shaker, home, up, 0.4f, 0f);
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float s = Mathf.Sin(t * Mathf.PI * 2f * 3.5f);
                SetPose(shaker, home + (Vector3.up * 0.07f + fwd * 0.03f) * s, Quaternion.AngleAxis(12f * s, right) * up);
                yield return null;
            }
            SetPose(shaker, home, up);
            // the shake gesture needs a real hand holding the tin; this is the same mix it triggers
            shaker.TryMix(MixMethod.Shake);
        }

        IEnumerator Stir(DrinkContainer cup, float seconds = 2.6f)
        {
            var spoon = FindFirstObjectByType<StirTool>();
            Hold(spoon);
            var t = spoon.transform;
            var b = GameVisuals.LocalBounds(spoon.gameObject);
            Vector3 e = b.extents;
            Vector3 axis = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
            float half = GameVisuals.Extent(b, axis) * 0.9f;

            // the spoon stood up straight, its lower end at a point on a small circle inside the cup
            UnityEngine.Pose PoseAt(float angle, float depth)
            {
                var keep = (t.position, t.rotation);
                Quaternion rot = Quaternion.FromToRotation(Upright(spoon) * axis, Vector3.up) * Upright(spoon);
                t.rotation = rot;
                Vector3 a = t.TransformPoint(b.center + axis * half), z = t.TransformPoint(b.center - axis * half);
                Vector3 low = a.y < z.y ? a : z;
                Vector3 c = cup.OpeningWorld + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * cup.OpeningRadiusWorld * 0.5f - Vector3.up * depth;
                Vector3 pos = t.position + (c - low);
                t.SetPositionAndRotation(keep.position, keep.rotation);
                return new UnityEngine.Pose(pos, rot);
            }

            var above = PoseAt(0, -0.12f);
            yield return Move(spoon, above.position, above.rotation, 0.9f);
            var inCup = PoseAt(0, 0.03f);
            yield return Move(spoon, inCup.position, inCup.rotation, 0.4f, 0f);
            for (float s = 0; s < seconds && cup.PartCount > 1; s += Time.deltaTime)
            {
                var p = PoseAt(s * Mathf.PI * 2f * 1.4f, 0.03f);
                SetPose(spoon, p.position, p.rotation);
                yield return null;
            }
            yield return Wait(0.3f);
            var lift = PoseAt(0, -0.12f);
            yield return Move(spoon, lift.position, lift.rotation, 0.4f, 0f);
            yield return Home(spoon);
        }

        IEnumerator Home(Component c, float seconds = 0.9f)
        {
            var home = c.GetComponent<PropRespawn>();
            if (!home) yield break;
            yield return Move(c, home.HomePosition, Upright(c), seconds);
        }

        PokeButton PanelButton(int index) =>
            station.transform.Find("Serve Panel").GetComponentsInChildren<PokeButton>(true)[index];

        IEnumerator Ring()
        {
            var bell = Find(DrinkGame.BellProp);
            Vector3 p = bell.position;
            for (float t = 0; t < 0.25f; t += Time.deltaTime)
            {
                bell.position = p + Vector3.down * 0.008f * Mathf.Sin(t / 0.25f * Mathf.PI);
                yield return null;
            }
            bell.position = p;
            station.Serve();
        }

        static readonly MethodInfo ShowTicket = typeof(OrderBook).GetMethod("ShowTicket", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>The order book picks random orders; for the video, the first two ask for the drinks made below.</summary>
        void SetOrder(int customer, DrinkOrder order)
        {
            var c = orders.Customers[customer];
            c.order = order;
            c.missed = false;
            ShowTicket.Invoke(orders, new object[] { c });
        }

        static readonly MethodInfo SayLine = typeof(OrderBook).GetMethod("Say", BindingFlags.NonPublic | BindingFlags.Instance);

        void Greet(OrderBook.Customer c)
        {
            var lines = c.profile?.Greetings;
            if (lines == null || lines.Length == 0) return;
            SayLine.Invoke(orders, new object[] { c, lines[0], GameVisuals.Ink, 6f });
        }

        Bounds HeadOf(OrderBook.Customer c)
        {
            var anim = c.body.GetComponentInChildren<Animator>();
            var head = anim && anim.isHuman ? anim.GetBoneTransform(HumanBodyBones.Head) : null;
            var all = GameVisuals.WorldBounds(c.body);
            Vector3 centre = head ? head.position : new Vector3(all.center.x, all.max.y - 0.15f, all.center.z);
            // the head and the speech bubble above it (the bubble floats over the top of the whole character)
            Vector3 bubble = c.bubble ? c.bubble.transform.position : centre + Vector3.up * 0.35f;
            float tall = Mathf.Abs(bubble.y - centre.y) + 0.3f;
            // a bubble floating far over the head (a tall character): frame the words, or they're too small to read
            if (tall > 0.9f) return new Bounds(bubble + Vector3.down * 0.02f, new Vector3(0.42f, 0.14f, 0.05f));
            return new Bounds((centre + bubble) / 2f, new Vector3(0.55f, Mathf.Max(0.5f, tall), 0.3f));
        }

        // ------------------------------------------------------------------ the video
        IEnumerator Script()
        {
            var shaker = Vessel(ContainerKind.ShakeMixer);
            var cup = Vessel(ContainerKind.StirCup);
            var highball = GlassOf("Highball");
            var rocks = GlassOf("Rocks");
            var whiskey = Source(Ingredients.Whiskey);
            var cola = Source(Ingredients.Cola);
            var tonic = Source(Ingredients.TonicWater);
            var scoop = FindFirstObjectByType<IceScoop>();
            var lime = FindObjectsByType<Garnish>(FindObjectsSortMode.None).First(g => g.color == "Lime");
            var customers = orders.Customers;

            // work spots on the counter, to the right of the serve coaster (the serve panel is on its left)
            Vector3 W(float r, float f = 0f) => serve + right * r + fwd * f;
            Vector3 work = W(0.42f);

            // ---- 1. the bar
            var layout = FindFirstObjectByType<BarLayout>();
            Vector3 high = player - fwd * 1.2f + right * 3.6f + Vector3.up * 3.0f;
            Cut(high, player + fwd * 2.5f - right * 0.5f, 62f);
            Shot(high - right * 1.6f + fwd * 0.3f, player + fwd * 2.2f + right * 0.3f, 58f, 0.35f);
            Caption("Moonlit Tails · Week 2\nRecorded in the Unity editor without a headset; a script moves the props");

            // stage the props off camera: bottles and cups go to the counter beside the work area
            foreach (var (c, at) in new (Component, Vector3)[]
            {
                (shaker, W(0.3f)), (cup, W(0.55f)), (highball, W(0.18f, -0.1f)), (rocks, W(0.4f, -0.12f)),
                (whiskey, W(0.75f, 0.05f)), (cola, W(0.88f, 0.05f)), (tonic, W(1.0f, 0.05f)),
            })
            {
                Hold(c);
                SetPose(c, OnCounter(c, at), Upright(c));
            }
            Debug.Log($"[Moonlit] Demo layout: serve {serve} player {player} fwd {fwd} right {right}\n" + string.Join("\n",
                new Component[] { shaker, cup, highball, rocks, whiskey, cola, tonic, scoop, lime }.Select(c => $"  {c.name}: {c.transform.position} bounds {MeshBounds(c.transform)}")));
            yield return Wait(3.5f);
            Caption("The bar is now one prefab per piece, and Stiv's models are prefabs with colliders");
            yield return Wait(4.5f);

            // ---- 2. customers and tickets
            while (customers.Count > 1 && (customers[0].order == null || customers[1].order == null)) yield return null;
            SetOrder(0, new DrinkOrder { Target = new Taste(2, 2, 0), Method = PrepMethod.Mixed, GlassVariant = "Highball", Cold = true, Color = "Lime", Hint = "Whiskey and cola, shaken" });
            SetOrder(1, new DrinkOrder { Target = new Taste(3, 1, 1), Method = PrepMethod.Layered, GlassVariant = "Rocks", Hint = "Whiskey and tonic, stirred, two layers, then +1 Sweet and +1 Sour" });

            Caption("Three customers come in and order. Each order goes on a ticket on the rail");
            Shot(eye, serve + fwd * 1.0f + Vector3.up * 0.45f, 62f, 1.2f);
            yield return Wait(3.5f);
            for (int i = 0; i < Mathf.Min(2, customers.Count); i++)
            {
                Greet(customers[i]);
                FromEye(HeadOf(customers[i]), 0.9f);
                yield return Wait(3.2f);
                var ticketAt = Find(DrinkGame.Tickets[i]).position;
                FromEye(new Bounds(ticketAt + Vector3.up * 0.1f, new Vector3(0.3f, 0.3f, 0.1f)), 0.8f);
                yield return Wait(2.6f);
            }

            // ---- 3. a mixed drink
            Caption("Mixed drink: tip a bottle over the Shake Mixer. One tip pours one unit");
            CounterShot(W(0.55f), 0.6f, 0.7f, 0f, 55f);
            yield return Wait(1.5f);
            yield return Pour(whiskey, shaker);
            yield return SetDown(whiskey, W(0.75f, 0.05f));
            yield return Pour(cola, shaker);
            yield return SetDown(cola, W(0.88f, 0.05f));
            Caption("Whiskey and cola have different densities, so they need a shake");
            yield return Wait(0.8f);
            yield return Shake(shaker);
            yield return Wait(1.2f);
            Caption("The Shake Mixer pours the whole drink into the glass at once");
            CounterShot(W(0.25f, -0.05f));
            yield return Pour(shaker, highball);
            yield return SetDown(shaker, W(0.3f, 0.08f));
            yield return Wait(1.0f);

            Caption("Dip the scoop in the ice well, then bring it to the glass: the drink is now Cold");
            var well = GameVisuals.WorldBounds(Find(DrinkGame.IceWell));
            CounterShot((well.center + highball.CenterWorld) / 2f, 0.55f, 0.75f, 0f, 55f);
            Vector3 ScoopTo(Vector3 p) => scoop.transform.position + (p - scoop.GetComponent<Renderer>().bounds.center);
            yield return Move(scoop, ScoopTo(well.center + Vector3.up * 0.12f), scoop.transform.rotation, 1.0f);
            yield return Move(scoop, ScoopTo(well.center), scoop.transform.rotation, 0.4f, 0f);
            yield return Wait(0.6f);
            yield return Move(scoop, ScoopTo(well.center + Vector3.up * 0.15f), scoop.transform.rotation, 0.4f, 0f);
            CounterShot(highball.CenterWorld, 0.45f, 0.5f, 0f, 45f);
            yield return Move(scoop, ScoopTo(highball.OpeningWorld + Vector3.up * 0.03f), scoop.transform.rotation, 1.0f);
            yield return Wait(1.2f);
            yield return Home(scoop);

            Caption("A lime wedge on the rim");
            yield return Move(lime, highball.OpeningWorld + Vector3.up * 0.04f, lime.transform.rotation, 1.0f);
            highball.AttachGarnish(lime);
            yield return Wait(1.8f);

            Caption("Let go of the glass near the serve coaster and it snaps on, upright");
            CounterShot(serve, 0.5f, 0.55f, 0.05f, 50f);
            yield return Move(highball, serve + Vector3.up * 0.12f + right * 0.05f, Quaternion.AngleAxis(25f, fwd) * Upright(highball), 1.1f);
            yield return Wait(0.3f);
            station.Snap.Snap(highball);
            yield return Wait(1.8f);
            Caption("The panel shows the final taste. Ring the bell to serve");
            yield return Wait(2.0f);
            yield return Ring();
            yield return Wait(0.4f);
            Caption("The right drink: they thank you and tell the next part of their story");
            FromEye(HeadOf(customers[0]), 0.8f);
            yield return Wait(5.0f);

            // ---- 4. a layered drink
            Caption("Layered drink: whiskey and tonic in the Stir Cup");
            CounterShot(W(0.7f), 0.6f, 0.7f, 0f, 55f);
            yield return Wait(1.0f);
            yield return Pour(whiskey, cup);
            yield return SetDown(whiskey, W(0.75f, 0.05f));
            yield return Pour(tonic, cup);
            yield return SetDown(tonic, W(1.0f, 0.05f));
            Caption("Same density, so stir with the bar spoon");
            CounterShot(cup.CenterWorld, 0.4f, 0.5f, 0f, 45f);
            yield return Stir(cup);
            yield return Wait(0.8f);
            Caption("The Stir Cup pours one layer per tip: two tips, two layers");
            CounterShot(W(0.45f, -0.05f));
            yield return Pour(cup, rocks, 2);
            yield return SetDown(cup, W(0.6f, 0.08f));
            yield return Wait(0.6f);

            Caption("Serve it before the finishing touch...");
            CounterShot(serve, 0.5f, 0.55f, 0.05f, 50f);
            yield return Move(rocks, serve + Vector3.up * 0.1f, Upright(rocks), 1.0f);
            station.Snap.Snap(rocks);
            yield return Wait(1.0f);
            yield return Ring();
            yield return Wait(0.3f);
            var missed = customers.FirstOrDefault(c => c.missed) ?? customers[1];
            Caption("Wrong drink: the customer says what's off, and their ticket shows a tip");
            FromEye(HeadOf(missed), 0.8f);
            yield return Wait(4.0f);
            int idx = customers.ToList().IndexOf(missed);
            if (idx >= 0 && idx < DrinkGame.Tickets.Length)
            {
                var ticketAt = Find(DrinkGame.Tickets[idx]).position;
                FromEye(new Bounds(ticketAt + Vector3.up * 0.1f, new Vector3(0.3f, 0.3f, 0.1f)), 0.8f);
                yield return Wait(3.0f);
            }

            Caption("End-of-drink adjustment on the panel: 2 points for a layered drink (+1 Sweet, +1 Sour)");
            var panel = station.transform.Find("Serve Panel");
            Shot(panel.position - panel.forward * 0.55f + Vector3.up * 0.12f, panel.position, 45f);
            yield return Wait(1.5f);
            PanelButton(3).Press();
            yield return Wait(1.0f);
            PanelButton(5).Press();
            yield return Wait(1.5f);
            yield return Ring();
            yield return Wait(0.3f);
            Caption("Now it matches the order");
            FromEye(HeadOf(customers[1]), 0.8f);
            yield return Wait(4.5f);

            // ---- 5. reset
            Caption("Things get messy...");
            Shot(eye + Vector3.up * 0.2f - fwd * 0.3f, serve + Vector3.up * 0.1f, 70f, 1.5f);
            yield return Wait(0.8f);
            var rng = new System.Random(7);
            foreach (var c in new Component[] { whiskey, cola, tonic, shaker, cup, highball, rocks })
            {
                if (!c.TryGetComponent<Rigidbody>(out var rb)) continue;
                rb.isKinematic = false;
                rb.constraints = RigidbodyConstraints.None;
                rb.linearVelocity = fwd * (float)(rng.NextDouble() * 1.5 + 0.5) + Vector3.up * 1.5f + right * (float)(rng.NextDouble() * 2 - 1);
                rb.angularVelocity = new Vector3((float)rng.NextDouble() * 6, (float)rng.NextDouble() * 6, (float)rng.NextDouble() * 6);
                yield return Wait(0.15f);
            }
            yield return Wait(1.8f);
            var reset = FindObjectsByType<PokeButton>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.transform.parent && b.transform.parent.name == ResetButton.Name);
            Caption("...so the big red DO NOT PUSH button puts every prop back where it started");
            if (reset)
            {
                CounterShot(reset.transform.position, 0.45f, 0.5f, 0f, 50f);
                yield return Wait(2.0f);
                reset.Press();
                yield return Wait(0.3f);
            }
            Shot(eye + Vector3.up * 0.2f - fwd * 0.3f, serve + Vector3.up * 0.1f, 70f, 1.5f);
            yield return Wait(3.0f);

            // ---- 6. the menu board
            var rules = GameObject.Find("How To Play");
            if (rules)
            {
                // the text faces its local -Z
                Caption("How to play, on the menu board, now readable");
                var r = rules.transform;
                Cut(r.position - r.forward * 1.5f, r.position, 42f);
                yield return Wait(5.5f);
            }

            // ---- 7. the end
            Caption("Moonlit Tails · Week 2");
            Shot(high, player + fwd * 2.5f - right * 0.5f, 62f, 0.6f);
            yield return Wait(4.0f);
            Finish();
        }
    }
}
#endif
