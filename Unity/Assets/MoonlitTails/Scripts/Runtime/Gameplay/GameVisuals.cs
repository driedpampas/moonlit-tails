using System.Collections.Generic;
using MoonlitTails.Drinks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoonlitTails.Gameplay
{
    /// <summary>Small helpers for the procedural visuals: flat-colour materials, primitives without colliders, text.</summary>
    public static class GameVisuals
    {
        static readonly Dictionary<int, Material> materials = new Dictionary<int, Material>();
        static Material flatBase;
        static bool clearsOnUnload;

        public static readonly Color BitterColor = new Color(0.55f, 0.34f, 0.18f);
        public static readonly Color SweetColor = new Color(0.96f, 0.47f, 0.68f);
        public static readonly Color SourColor = new Color(0.86f, 0.9f, 0.3f);
        public static readonly Color Good = new Color(0.55f, 0.95f, 0.6f);
        public static readonly Color Bad = new Color(1f, 0.55f, 0.5f);
        public static readonly Color Ink = new Color(1f, 0.97f, 0.9f);

        /// <summary>
        /// A shared unlit material in one colour: a copy of <c>Resources/Moonlit/FlatUnlit.mat</c> (URP Unlit), so the
        /// shader is always in builds. One material per colour, rounded to 1/32 per channel so near-equal liquid colours
        /// share one. They're destroyed when a scene unloads.
        /// </summary>
        public static Material Flat(Color c)
        {
            int key = Step(c.r) | Step(c.g) << 6 | Step(c.b) << 12 | Step(c.a) << 18;
            if (materials.TryGetValue(key, out var m) && m) return m;
            if (!flatBase) flatBase = Resources.Load<Material>("Moonlit/FlatUnlit");
            if (!flatBase)
            {
                Debug.LogError("GameVisuals: Resources/Moonlit/FlatUnlit.mat is missing, so liquids and pour streams have no material.");
                return null;
            }
            if (!clearsOnUnload)
            {
                clearsOnUnload = true;
                SceneManager.sceneUnloaded += _ => ClearMaterials();
            }
            c = new Color(Step(c.r) / 32f, Step(c.g) / 32f, Step(c.b) / 32f, Step(c.a) / 32f);
            m = new Material(flatBase) { name = "Flat " + ColorUtility.ToHtmlStringRGB(c) };
            m.SetColor("_BaseColor", c);
            m.color = c;
            materials[key] = m;
            return m;
        }

        static int Step(float v) => Mathf.RoundToInt(Mathf.Clamp01(v) * 32f);

        static void ClearMaterials()
        {
            foreach (var m in materials.Values) if (m) Object.Destroy(m);
            materials.Clear();
        }

        /// <summary>A primitive with its collider removed straight away, so it never pushes props around.</summary>
        public static GameObject Primitive(PrimitiveType type, Transform parent, string name, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col)
            {
                col.enabled = false;   // off at once so it never pushes anything; removed at the end of the frame
                Object.Destroy(col);
            }
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Flat(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        public static TextMeshPro Text(Transform parent, string name, float size, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshPro>();
            t.fontSize = size;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.color = Ink;   // no outline: it needs a material per label (bad for Quest) and fails on hidden labels
            t.rectTransform.sizeDelta = new Vector2(0.5f, 0.1f);
            t.richText = true;
            return t;
        }

        /// <summary>
        /// Builds a set of taste bars under <paramref name="parent"/>: per taste a background, a fill, a zero tick, a name
        /// and a value, plus a caption. Used by floating labels; the serve panel has its own in its prefab.
        /// </summary>
        public static StatBarsView BuildStatBars(Transform parent, float width)
        {
            var root = new GameObject("Taste Bars");
            root.transform.SetParent(parent, false);
            var view = new StatBarsView { root = root, width = width };
            Color[] colors = { BitterColor, SweetColor, SourColor };
            string[] names = { "Bitter", "Sweet", "Sour" };
            const float row = 0.018f, barH = 0.011f;
            var t = root.transform;
            for (int i = 0; i < 3; i++)
            {
                float y = -i * row;
                var bg = Primitive(PrimitiveType.Quad, t, names[i] + " Bar", new Color(0.12f, 0.1f, 0.22f));
                bg.transform.localPosition = new Vector3(0, y, 0);
                bg.transform.localScale = new Vector3(width, barH, 1);

                var fill = Primitive(PrimitiveType.Quad, t, names[i] + " Fill", colors[i]);
                fill.transform.localPosition = new Vector3(0, y, -0.001f);
                fill.transform.localScale = new Vector3(0, barH * 0.8f, 1);
                view.fills[i] = fill.transform;

                var tick = Primitive(PrimitiveType.Quad, t, names[i] + " Zero", Color.white);
                tick.transform.localPosition = new Vector3(0, y, -0.002f);
                tick.transform.localScale = new Vector3(0.0012f, barH * 1.3f, 1);

                var label = Text(t, names[i] + " Name", 0.09f, TextAlignmentOptions.Right);
                label.text = names[i];
                label.rectTransform.sizeDelta = new Vector2(0.05f, 0.012f);
                label.transform.localPosition = new Vector3(-width * 0.5f - 0.03f, y, -0.002f);

                var value = Text(t, names[i] + " Value", 0.1f, TextAlignmentOptions.Left);
                value.rectTransform.sizeDelta = new Vector2(0.03f, 0.012f);
                value.transform.localPosition = new Vector3(width * 0.5f + 0.02f, y, -0.002f);
                view.values[i] = value;
            }
            view.caption = Text(t, "Caption", 0.1f);
            view.caption.transform.localPosition = new Vector3(0, 0.017f, -0.002f);
            return view;
        }

        /// <summary>A colour for a liquid, from its tastes (liquids don't change the order's colour; this is just to see them).</summary>
        public static Color LiquidColor(Taste t)
        {
            float b = Mathf.Max(0, t.Bitter), s = Mathf.Max(0, t.Sweet), o = Mathf.Max(0, t.Sour);
            float sum = b + s + o;
            if (sum <= 0) return new Color(0.75f, 0.8f, 0.9f);
            var c = (BitterColor * b + SweetColor * s + SourColor * o) / sum;
            c.a = 1f;
            return Color.Lerp(c, Color.white, 0.15f);
        }

        const int SignedRange = 20;
        static readonly string[] signed = new string[SignedRange * 2 + 1];

        /// <summary>"+2", "0", "-1". The usual range comes from a table, so showing a taste makes no new strings.</summary>
        public static string Signed(int v)
        {
            if (v < -SignedRange || v > SignedRange) return v > 0 ? "+" + v : v.ToString();
            return signed[v + SignedRange] ??= v > 0 ? "+" + v : v.ToString();
        }

        public static string StatsLine(Taste t) =>
            $"<color=#{ColorUtility.ToHtmlStringRGB(BitterColor * 1.5f)}>Bitter {Signed(t.Bitter)}</color>  " +
            $"<color=#{ColorUtility.ToHtmlStringRGB(SweetColor)}>Sweet {Signed(t.Sweet)}</color>  " +
            $"<color=#{ColorUtility.ToHtmlStringRGB(SourColor)}>Sour {Signed(t.Sour)}</color>";

        /// <summary>The local axis of an object that points most nearly up right now (props are modelled upright).</summary>
        public static Vector3 LocalUpAxis(Transform t)
        {
            Vector3 up = t.InverseTransformDirection(Vector3.up);
            Vector3 a = new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z));
            if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(up.x), 0, 0);
            if (a.y >= a.z) return new Vector3(0, Mathf.Sign(up.y), 0);
            return new Vector3(0, 0, Mathf.Sign(up.z));
        }

        /// <summary>World bounds of every renderer in an object and its children (pieces can be made of parts).</summary>
        public static Bounds WorldBounds(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        public static Bounds LocalBounds(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            return mf && mf.sharedMesh ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one * 0.1f);
        }

        /// <summary>Half the size of the bounds along a local axis (x, y or z, any sign).</summary>
        public static float Extent(Bounds b, Vector3 axis) => Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(axis.x), Mathf.Abs(axis.y), Mathf.Abs(axis.z))));

        /// <summary>The smaller half-size across the two axes that aren't the up axis.</summary>
        public static float Radius(Bounds b, Vector3 upAxis)
        {
            Vector3 e = b.extents;
            if (Mathf.Abs(upAxis.x) > 0.5f) return Mathf.Min(e.y, e.z);
            if (Mathf.Abs(upAxis.y) > 0.5f) return Mathf.Min(e.x, e.z);
            return Mathf.Min(e.x, e.y);
        }
    }
}
