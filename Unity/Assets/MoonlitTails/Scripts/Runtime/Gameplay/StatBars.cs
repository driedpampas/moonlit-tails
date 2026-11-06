using MoonlitTails.Drinks;
using TMPro;
using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// The three taste bars from the guide: 0 in the middle, positive fills right, negative fills left.
    /// Built from quads so it needs no UI canvas. Faces -Z (like TextMeshPro), so put it under something that faces the player.
    /// </summary>
    public class StatBars : MonoBehaviour
    {
        public const int Range = 8;   // a bar is full at +/- this many points

        float width;
        Transform[] fills;
        TextMeshPro[] values;
        TextMeshPro caption;

        public static StatBars Create(Transform parent, float width)
        {
            var go = new GameObject("Taste Bars");
            go.transform.SetParent(parent, false);
            var bars = go.AddComponent<StatBars>();
            bars.Build(width);
            return bars;
        }

        void Build(float w)
        {
            width = w;
            fills = new Transform[3];
            values = new TextMeshPro[3];
            Color[] colors = { GameVisuals.BitterColor, GameVisuals.SweetColor, GameVisuals.SourColor };
            string[] names = { "Bitter", "Sweet", "Sour" };
            const float row = 0.018f, barH = 0.011f;
            for (int i = 0; i < 3; i++)
            {
                float y = -i * row;
                var bg = GameVisuals.Primitive(PrimitiveType.Quad, transform, names[i] + " Bar", new Color(0.12f, 0.1f, 0.22f));
                bg.transform.localPosition = new Vector3(0, y, 0);
                bg.transform.localScale = new Vector3(w, barH, 1);

                var fill = GameVisuals.Primitive(PrimitiveType.Quad, transform, names[i] + " Fill", colors[i]);
                fill.transform.localPosition = new Vector3(0, y, -0.001f);
                fill.transform.localScale = new Vector3(0, barH * 0.8f, 1);
                fills[i] = fill.transform;

                var tick = GameVisuals.Primitive(PrimitiveType.Quad, transform, names[i] + " Zero", Color.white);
                tick.transform.localPosition = new Vector3(0, y, -0.002f);
                tick.transform.localScale = new Vector3(0.0012f, barH * 1.3f, 1);

                var label = GameVisuals.Text(transform, names[i] + " Name", 0.09f, TextAlignmentOptions.Right);
                label.text = names[i];
                label.rectTransform.sizeDelta = new Vector2(0.05f, 0.012f);
                label.transform.localPosition = new Vector3(-w * 0.5f - 0.03f, y, -0.002f);

                values[i] = GameVisuals.Text(transform, names[i] + " Value", 0.1f, TextAlignmentOptions.Left);
                values[i].rectTransform.sizeDelta = new Vector2(0.03f, 0.012f);
                values[i].transform.localPosition = new Vector3(w * 0.5f + 0.02f, y, -0.002f);
            }
            caption = GameVisuals.Text(transform, "Caption", 0.1f);
            caption.transform.localPosition = new Vector3(0, 0.017f, -0.002f);
        }

        public void Show(Taste t, string text = null)
        {
            int[] v = { t.Bitter, t.Sweet, t.Sour };
            for (int i = 0; i < 3; i++)
            {
                float f = Mathf.Clamp(v[i], -Range, Range) / (float)Range;
                float len = Mathf.Abs(f) * width * 0.5f;
                var p = fills[i].localPosition;
                fills[i].localPosition = new Vector3(Mathf.Sign(f) * len * 0.5f, p.y, p.z);
                fills[i].localScale = new Vector3(Mathf.Max(len, 0.0001f), fills[i].localScale.y, 1);
                values[i].text = GameVisuals.Signed(v[i]);
            }
            caption.text = text ?? "";
        }
    }
}
