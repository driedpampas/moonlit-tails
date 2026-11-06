using System.Collections.Generic;
using MoonlitTails.Drinks;
using TMPro;
using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>Small helpers for the procedural visuals: flat-colour materials, primitives without colliders, text.</summary>
    public static class GameVisuals
    {
        static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        static Shader unlit;

        public static readonly Color BitterColor = new Color(0.55f, 0.34f, 0.18f);
        public static readonly Color SweetColor = new Color(0.96f, 0.47f, 0.68f);
        public static readonly Color SourColor = new Color(0.86f, 0.9f, 0.3f);
        public static readonly Color Good = new Color(0.55f, 0.95f, 0.6f);
        public static readonly Color Bad = new Color(1f, 0.55f, 0.5f);
        public static readonly Color Ink = new Color(1f, 0.97f, 0.9f);

        /// <summary>A shared unlit material in one colour (URP Unlit; it's already used by the sky, so it ships in builds).</summary>
        public static Material Flat(Color c)
        {
            if (materials.TryGetValue(c, out var m) && m) return m;
            if (!unlit) unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            m = new Material(unlit) { name = "Flat " + ColorUtility.ToHtmlStringRGB(c), enableInstancing = true };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            m.color = c;
            materials[c] = m;
            return m;
        }

        /// <summary>A primitive with its collider removed straight away, so it never pushes props around.</summary>
        public static GameObject Primitive(PrimitiveType type, Transform parent, string name, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            var col = go.GetComponent<Collider>();
            if (col) Object.DestroyImmediate(col);
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

        public static string Signed(int v) => v > 0 ? "+" + v : v.ToString();

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
