using System.Collections.Generic;
using System.Linq;
using MoonlitTails.Drinks;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails.Gameplay
{
    public enum ContainerKind
    {
        /// <summary>A serving glass. Takes a whole drink from the Shake Mixer (mixed), or layers from the Stir Cup.</summary>
        Glass,
        /// <summary>Mix two things by stirring (bar spoon) or shaking by hand. Pouring into a glass adds one layer.</summary>
        StirCup,
        /// <summary>Mix two things the same way. Pouring into a glass pours the whole drink as a mixed drink.</summary>
        ShakeMixer,
    }

    /// <summary>
    /// A glass, the Stir Cup or the Shake Mixer, with what's in it.
    ///
    /// Mixing vessels (Stir Cup, Shake Mixer) hold at most two unmixed parts. Stir or shake to combine them; the wrong
    /// method does nothing, and the same ingredient can't go in twice. A single combined liquid can be poured on:
    /// into the other vessel (as one part), or into a glass. From the Shake Mixer the whole drink goes into the glass
    /// as a mixed drink. From the Stir Cup each tilt pours one 1-unit layer, as many times as the drink has units.
    ///
    /// Glasses hold either one mixed drink or up to their capacity of layers, plus ice, a garnish and the
    /// end-of-drink stat adjustment. Tip anything out over nothing to empty it.
    /// </summary>
    public class DrinkContainer : Pourer
    {
        public static readonly List<DrinkContainer> All = new List<DrinkContainer>();
        public const int VesselCapacity = 5;

        public ContainerKind kind;
        [Tooltip("For glasses: the variant customers ask for, e.g. Highball. For the vessels: Stir Cup / Shake Mixer.")] public string variant;
        public GlassLength length = GlassLength.Medium;
        [Tooltip("The label shows when the player's head is closer than this (m). 0 = always.")]
        [SerializeField] float showWithin = -1f;   // -1 = default: 0.9 m for glasses, always for the vessels

        // mixing vessels
        readonly List<Liquid> parts = new List<Liquid>();
        int layersPoured;   // layers already poured from the Stir Cup's drink
        int LayerPoursLeft => parts.Count == 1 && parts[0].IsPrepared ? parts[0].Units - layersPoured : 0;

        // glasses
        Liquid mixed;
        readonly List<Liquid> layers = new List<Liquid>();
        LayerResult? layered;   // Layering.Resolve(layers), kept until a layer is added or the glass is cleared
        LayerResult Layered => layered ??= Layering.Resolve(layers, length);
        Taste adjust;
        public bool Cold { get; private set; }
        public Garnish Garnish { get; private set; }

        public FloatingLabel Label { get; private set; }
        public bool IsGlass => kind == ContainerKind.Glass;
        public bool IsHeld => grab && grab.isSelected;
        public int Capacity => IsGlass ? Glass.Capacity(length) : VesselCapacity;

        XRGrabInteractable grab;
        PropRespawn home;
        Transform liquidRoot;
        readonly List<MeshRenderer> liquidBits = new List<MeshRenderer>();   // a pool: reused, never destroyed
        GameObject iceBits;   // a copy of the Ice prefab, made the first time the glass gets ice

        // refreshes are batched: a change marks the container, and DrinkGame's LateUpdate redraws each one once
        static readonly HashSet<DrinkContainer> dirty = new HashSet<DrinkContainer>();
        static readonly List<DrinkContainer> refreshing = new List<DrinkContainer>();

        // shake detection
        Vector3 lastPos, lastDir;
        readonly Queue<float> reversals = new Queue<float>();

        bool setUp;

        void Awake()
        {
            if (string.IsNullOrEmpty(variant))
            {
                Debug.LogError($"[Moonlit] {name}: DrinkContainer has no variant name set", this);
                enabled = false;
                return;
            }
            setUp = true;
            EnsureGeometry();
            grab = GetComponent<XRGrabInteractable>();
            home = GetComponent<PropRespawn>();

            liquidRoot = new GameObject("Liquid").transform;
            liquidRoot.SetParent(transform, false);
            liquidRoot.localRotation = Quaternion.FromToRotation(Vector3.up, upAxis);
            liquidRoot.localPosition = bounds.center;

            Label = FloatingLabel.Create(transform, OpeningWorld.y - transform.position.y + 0.05f, IsGlass ? 0.22f : 0.26f);
            Label.showWithin = showWithin >= 0f ? showWithin : IsGlass ? 0.9f : 0f;
            lastPos = transform.position;
            if (isActiveAndEnabled && !All.Contains(this)) All.Add(this);
            RefreshNow();
        }

        void OnEnable() { if (setUp && !All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        // ------------------------------------------------------------------ geometry
        float HalfHeight => GameVisuals.Extent(bounds, upAxis);
        public Vector3 OpeningWorld => transform.TransformPoint(bounds.center + upAxis * HalfHeight);
        public Vector3 BottomWorld => transform.TransformPoint(bounds.center - upAxis * HalfHeight);
        public Vector3 CenterWorld => transform.TransformPoint(bounds.center);
        public float OpeningRadiusWorld => GameVisuals.Radius(bounds, upAxis) * transform.lossyScale.x;
        bool Upright => Vector3.Angle(transform.TransformDirection(upAxis), Vector3.up) < 45f;

        /// <summary>The container whose mouth is under a pouring spout.</summary>
        public static DrinkContainer FindBelow(Vector3 spout, DrinkContainer except)
        {
            DrinkContainer best = null; float bestD = float.MaxValue;
            foreach (var c in All)
            {
                if (c == except || !c.Upright) continue;
                Vector3 o = c.OpeningWorld;
                float dy = spout.y - o.y;
                if (dy < -0.03f || dy > 0.6f) continue;
                float d = new Vector2(spout.x - o.x, spout.z - o.z).magnitude;
                if (d > c.OpeningRadiusWorld + 0.035f) continue;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>The closest upright glass whose rim is within <paramref name="reach"/> of a point.</summary>
        public static DrinkContainer FindGlassNear(Vector3 p, float reach)
        {
            DrinkContainer best = null; float bestD = reach;
            foreach (var c in All)
            {
                if (!c.IsGlass || !c.Upright) continue;
                Vector3 o = c.OpeningWorld;
                float horizontal = new Vector2(p.x - o.x, p.z - o.z).magnitude;
                float d = Mathf.Max(0, horizontal - c.OpeningRadiusWorld) + Mathf.Abs(p.y - o.y) * 0.5f;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        /// <summary>The mixing vessel a point (a spoon tip) is inside.</summary>
        public static DrinkContainer FindVesselContaining(Vector3 p)
        {
            foreach (var c in All)
            {
                if (c.IsGlass || !c.Upright) continue;
                Vector3 o = c.OpeningWorld, b = c.BottomWorld;
                if (p.y < b.y - 0.01f || p.y > o.y + 0.03f) continue;
                if (new Vector2(p.x - o.x, p.z - o.z).magnitude <= c.OpeningRadiusWorld * 1.15f + 0.01f) return c;
            }
            return null;
        }

        // ------------------------------------------------------------------ state
        /// <summary>Unmixed parts in a Stir Cup / Shake Mixer (0, 1 or 2).</summary>
        public int PartCount => parts.Count;
        public bool IsEmpty => IsGlass ? mixed == null && layers.Count == 0 : parts.Count == 0;
        public int Units => IsGlass ? (mixed != null ? mixed.Units : layers.Count) : parts.Sum(p => p.Units) - layersPoured;
        public PrepMethod Method => mixed != null ? PrepMethod.Mixed : PrepMethod.Layered;
        public int AdjustmentLeft => IsEmpty ? 0 : Finishing.AdjustmentPoints(Method) - adjust.AbsTotal;

        /// <summary>The drink's stats before the end-of-drink adjustment.</summary>
        public Taste RawStats
        {
            get
            {
                if (mixed != null) return mixed.Stats;
                if (layers.Count > 0) return Layered.Stats;
                return Taste.Zero;
            }
        }

        public Taste FinalStats => RawStats + adjust;

        public ServedDrink ToServed() => new ServedDrink
        {
            Stats = FinalStats, Method = Method, GlassVariant = variant, Cold = Cold, Color = Garnish ? Garnish.color : null,
        };

        // ------------------------------------------------------------------ receiving
        public bool ReceiveIngredient(Ingredient ingredient, out string message)
        {
            if (IsGlass)
            {
                message = "Make it in the Stir Cup or Shake Mixer first";
                return false;
            }
            return AddPart(Liquid.Of(ingredient), ingredient.DisplayName, out message);
        }

        bool AddPart(Liquid l, string what, out string message)
        {
            if (parts.Count >= 2) { message = "Mix what's in here first"; return false; }
            if (layersPoured > 0) { message = "Finish pouring this drink (or tip it out) first"; return false; }
            if (parts.Any(p => p.SharesIngredientWith(l))) { message = "Already in here (no self-mixing)"; return false; }
            if (Units + l.Units > VesselCapacity) { message = "Too full"; return false; }
            parts.Add(l);
            message = parts.Count == 2 ? $"+ {what}: now stir or shake" : $"+ {what}";
            Refresh();
            return true;
        }

        bool ReceiveMixed(Liquid l, out string message)
        {
            if (!l.IsPrepared) { message = "A drink needs a base and a modifier"; return false; }
            if (!IsEmpty) { message = "Empty the glass first"; return false; }
            if (l.Units > Capacity) { message = $"Too much for this glass ({l.Units}/{Capacity} units)"; return false; }
            mixed = l;
            message = "Mixed drink poured";
            Refresh();
            return true;
        }

        /// <summary>Returns true if the layer went in (even if it collapsed; then <paramref name="collapsed"/> is true).</summary>
        bool ReceiveLayer(Liquid l, out string message, out bool collapsed)
        {
            collapsed = false;
            if (!l.IsPrepared) { message = "Layers must be prepared drinks (base + modifier)"; return false; }
            if (mixed != null) { message = "This glass already has a mixed drink"; return false; }
            if (layers.Count >= Capacity) { message = "The glass is full"; return false; }
            int collapsedBefore = layers.Count > 0 ? Layered.Collapsed.Count(c => c) : 0;
            layers.Add(l);
            layered = null;
            collapsed = Layered.Collapsed.Count(c => c) > collapsedBefore;
            message = collapsed ? "The layers collapsed!" : $"Layer {layers.Count} poured";
            Refresh();
            return true;
        }

        public bool AddIce(out string message)
        {
            if (!IsGlass) { message = "Ice goes in the glass"; return false; }
            if (Cold) { message = "Already has ice"; return false; }
            Cold = true;
            message = "Ice: Cold";
            Refresh();
            return true;
        }

        public void AttachGarnish(Garnish g)
        {
            if (Garnish && Garnish != g) Garnish.Detach(true);
            Garnish = g;
            Vector3 side = Vector3.Cross(Vector3.up, transform.right).normalized;
            if (side.sqrMagnitude < 0.01f) side = Vector3.forward;
            g.AttachTo(this, OpeningWorld + side * OpeningRadiusWorld * 0.9f);
            if (Label) Label.Flash($"Garnish: {g.color}", GameVisuals.Good);
            Refresh();
        }

        public void ForgetGarnish(Garnish g) { if (Garnish == g) { Garnish = null; Refresh(); } }

        public bool TryAdjust(Taste delta, out string message)
        {
            if (!IsGlass || IsEmpty) { message = "Nothing to adjust"; return false; }
            var next = adjust + delta;
            int allowed = Finishing.AdjustmentPoints(Method);
            if (next.AbsTotal > allowed) { message = $"Only {allowed} adjustment point{(allowed == 1 ? "" : "s")} for a {Method.ToString().ToLower()} drink"; return false; }
            adjust = next;
            message = null;
            Refresh();
            return true;
        }

        // ------------------------------------------------------------------ mixing
        public void TryMix(MixMethod method)
        {
            if (IsGlass || parts.Count < 2 || !setUp) return;
            var result = Mixing.Combine(parts[0], parts[1], method);
            switch (result.Outcome)
            {
                case MixOutcome.Mixed:
                    parts.Clear();
                    parts.Add(result.Liquid);
                    layersPoured = 0;
                    Label.Flash(method == MixMethod.Stir ? "Stirred!" : "Shaken!", GameVisuals.Good);
                    break;
                case MixOutcome.WrongMethod:
                    var need = Mixing.RequiredMethod(parts[0], parts[1]);
                    Label.Flash(need == MixMethod.Stir
                        ? "Nothing happens: same density, stir it"
                        : "Nothing happens: different density, shake it", GameVisuals.Bad);
                    break;
                default:
                    Label.Flash("Can't mix an ingredient with itself", GameVisuals.Bad);
                    break;
            }
            Refresh();
        }

        protected override void Update()
        {
            base.Update();   // pouring
            if (IsGlass || !setUp) return;
            // hand shaking: quick back-and-forth while held
            Vector3 p = transform.position;
            float dt = Time.deltaTime;
            if (dt > 0 && IsHeld)
            {
                Vector3 v = (p - lastPos) / dt;
                if (v.magnitude > 0.6f)
                {
                    Vector3 dir = v.normalized;
                    if (lastDir == Vector3.zero) lastDir = dir;
                    float dot = Vector3.Dot(dir, lastDir);
                    if (dot < -0.2f) { reversals.Enqueue(Time.time); lastDir = dir; }
                    else if (dot > 0.5f) lastDir = dir;
                }
            }
            else lastDir = Vector3.zero;
            lastPos = p;
            while (reversals.Count > 0 && Time.time - reversals.Peek() > 1.5f) reversals.Dequeue();
            if (reversals.Count >= 6)
            {
                reversals.Clear();
                TryMix(MixMethod.Shake);
            }
        }

        // ------------------------------------------------------------------ pouring out
        public override bool HasSomethingToPour => !IsEmpty;
        protected override bool CanDump => true;
        public override Color StreamColor => IsGlass ? GameVisuals.LiquidColor(RawStats) : GameVisuals.LiquidColor(parts.Count > 0 ? parts[0].Stats : Taste.Zero);

        public override bool PourInto(DrinkContainer target, out string message)
        {
            if (IsGlass) { message = "Serve the glass at the serve spot"; return false; }
            if (parts.Count != 1) { message = "Mix it first"; return false; }
            var drink = parts[0];
            bool ok;
            if (!target.IsGlass)
            {
                if (layersPoured > 0) { message = "Part of it is already poured as layers"; return false; }
                ok = target.AddPart(drink, OrderGenerator.Describe(drink), out message);
                if (ok) Clear();
                return ok;
            }
            if (kind == ContainerKind.ShakeMixer)
            {
                ok = target.ReceiveMixed(drink, out message);
                if (ok) Clear();
                return ok;
            }
            bool landed = target.ReceiveLayer(drink, out message, out bool collapsed);
            if (landed)
            {
                layersPoured++;
                if (LayerPoursLeft <= 0) Clear(); else Refresh();
            }
            return landed && !collapsed;
        }

        public override void Dump()
        {
            if (IsEmpty && !Cold && !Garnish) return;
            Clear();
            if (Label) Label.Flash("Emptied", GameVisuals.Ink, 1.5f);
        }

        public void Clear()
        {
            parts.Clear();
            layersPoured = 0;
            mixed = null;
            layers.Clear();
            layered = null;
            adjust = Taste.Zero;
            if (IsGlass)
            {
                Cold = false;
                if (Garnish) Garnish.Detach(true);
                Garnish = null;
            }
            Refresh();
        }

        /// <summary>After serving: empty it and put it back where it started.</summary>
        public void ServedAndReturned()
        {
            Clear();
            if (home) home.SendHome();
        }

        // ------------------------------------------------------------------ visuals
        /// <summary>Marks the label and liquid for redrawing at the end of the frame (several changes, one redraw).</summary>
        void Refresh() => dirty.Add(this);

        /// <summary>Redraws every container that changed this frame. Called once per frame by the Drink Game.</summary>
        public static void RefreshChanged()
        {
            if (dirty.Count == 0) return;
            refreshing.AddRange(dirty);
            dirty.Clear();
            for (int i = 0; i < refreshing.Count; i++) if (refreshing[i]) refreshing[i].RefreshNow();
            refreshing.Clear();
        }

        void RefreshNow()
        {
            RebuildLiquid();
            if (!Label) return;

            if (IsGlass)
            {
                string head = $"<b>{variant}</b> <size=75%>{length} · {Units}/{Capacity} units</size>";
                if (IsEmpty && !Cold && !Garnish) { Label.SetTitle(head); Label.ShowBars(null); return; }
                var extras = new List<string> { IsEmpty ? "empty" : Method.ToString() };
                if (Cold) extras.Add("Cold");
                if (Garnish) extras.Add(Garnish.color);
                if (!IsEmpty && AdjustmentLeft > 0) extras.Add($"{AdjustmentLeft} adjust left");
                Label.SetTitle(head + "\n<size=75%>" + string.Join(" · ", extras) + "</size>");
                Label.ShowBars(IsEmpty ? (Taste?)null : FinalStats);
                return;
            }

            string name = kind == ContainerKind.StirCup ? "Stir Cup" : "Shake Mixer";
            string role = kind == ContainerKind.StirCup ? "pours layers" : "pours a mixed drink";
            if (parts.Count == 0) { Label.SetTitle($"<b>{name}</b> <size=70%>({role})</size>"); Label.ShowBars(null); return; }
            if (parts.Count == 1)
            {
                int left = LayerPoursLeft;
                string pours = kind == ContainerKind.StirCup && parts[0].IsPrepared ? $" · {left} layer pour{(left == 1 ? "" : "s")} left" : "";
                Label.SetTitle($"<b>{name}</b>\n<size=70%>{OrderGenerator.Describe(parts[0])} · {Units}u · density {parts[0].Stats.BaseValue}{pours}</size>");
                Label.ShowBars(parts[0].Stats);
                return;
            }
            Label.SetTitle($"<b>{name}</b>\n<size=70%>{OrderGenerator.Describe(parts[0])} (density {parts[0].Stats.BaseValue})  +  {OrderGenerator.Describe(parts[1])} (density {parts[1].Stats.BaseValue})</size>");
            Label.ShowBars(parts[0].Stats + parts[1].Stats, "unmixed");
        }

        void RebuildLiquid()
        {
            if (!liquidRoot) return;

            float half = HalfHeight * 0.9f;
            float radius = GameVisuals.Radius(bounds, upAxis) * 0.78f;
            float usable = half * 2f * 0.9f;
            float y = -half;
            int used = 0;

            void Add(float units, Color c)
            {
                float h = usable * units / Capacity;
                if (used == liquidBits.Count)
                    liquidBits.Add(GameVisuals.Spawn<MeshRenderer>("Liquid", liquidRoot));
                var cyl = liquidBits[used++];
                cyl.sharedMaterial = GameVisuals.Flat(c);
                cyl.transform.localScale = new Vector3(radius * 2f, h * 0.5f, radius * 2f);
                cyl.transform.localPosition = new Vector3(0, y + h * 0.5f, 0);
                if (!cyl.gameObject.activeSelf) cyl.gameObject.SetActive(true);
                y += h;
            }

            if (IsGlass)
            {
                if (mixed != null) Add(mixed.Units, GameVisuals.LiquidColor(mixed.Stats));
                else if (layers.Count > 0)
                {
                    var collapsed = Layered.Collapsed;
                    for (int i = 0; i < layers.Count; i++)
                        Add(1, collapsed[i] ? new Color(0.45f, 0.45f, 0.5f) : GameVisuals.LiquidColor(layers[i].Stats));
                }
            }
            else for (int i = 0; i < parts.Count; i++) Add(parts[i].Units - (i == 0 ? layersPoured : 0), GameVisuals.LiquidColor(parts[i].Stats));
            for (int i = used; i < liquidBits.Count; i++)
                if (liquidBits[i].gameObject.activeSelf) liquidBits[i].gameObject.SetActive(false);

            if (IsGlass && Cold && !iceBits)
            {
                // the Ice prefab is three cubes sized for a glass of radius 1
                iceBits = GameVisuals.Spawn<GameObject>("Ice", liquidRoot);
                iceBits.transform.localPosition = new Vector3(0, half * 0.55f, 0);
                iceBits.transform.localScale = Vector3.one * radius;
            }
            if (iceBits && iceBits.activeSelf != Cold) iceBits.SetActive(Cold);
        }
    }
}
