using System.Collections.Generic;
using MoonlitTails.Drinks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoonlitTails.Gameplay
{
    /// <summary>Small helpers for the visuals: the taste colours, flat-colour materials, copies of the small prefabs, and measuring props.</summary>
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

        /// <summary>
        /// A copy of one of the small prefabs in <c>Resources/Moonlit</c> (FloatingLabel, Liquid, Ice, PourStream),
        /// placed under <paramref name="parent"/>. Their look is edited in those prefabs.
        /// </summary>
        public static T Spawn<T>(string prefab, Transform parent) where T : Object
        {
            var source = Resources.Load<T>("Moonlit/" + prefab);
            if (!source) Debug.LogError($"GameVisuals: Resources/Moonlit/{prefab}.prefab is missing or has no {typeof(T).Name}.");
            return Object.Instantiate(source, parent, false);
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
