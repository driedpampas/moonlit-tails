using System;
using MoonlitTails.Drinks;
using TMPro;
using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// A label that floats above something and always faces the player: a title line, an optional message that fades
    /// out, and optional taste bars. It isn't parented to the prop, so it doesn't tip over when the prop does.
    /// These are made from code on purpose: they're live overlays whose text changes as you play.
    /// </summary>
    public class FloatingLabel : MonoBehaviour
    {
        public Transform target;
        public float height = 0.12f;
        [Tooltip("Hide when the player's head is further away than this (0 = always show).")]
        public float showWithin = 0f;

        TextMeshPro title;
        TextMeshPro message;
        StatBarsView bars;   // built on first use; not serialized
        Transform content;
        float messageUntil;
        Camera cam;

        /// <summary>The camera this label turns to face. Defaults to <see cref="Camera.main"/>; the demo recorder sets its own.</summary>
        public Camera ViewCamera { get => cam; set => cam = value; }

        public static FloatingLabel Create(Transform target, float height, float titleSize = 0.3f)
        {
            var go = new GameObject("Label " + target.name);
            var label = go.AddComponent<FloatingLabel>();
            label.target = target;
            label.height = height;
            label.content = new GameObject("Content").transform;
            label.content.SetParent(go.transform, false);
            label.title = GameVisuals.Text(label.content, "Title", titleSize);
            label.message = GameVisuals.Text(label.content, "Message", titleSize * 0.85f);
            label.message.transform.localPosition = new Vector3(0, -0.035f * titleSize / 0.22f, 0);
            label.message.text = "";
            return label;
        }

        public void SetTitle(string text) { if (title && title.text != text) title.text = text; }

        /// <summary>A short message under the title, shown for a few seconds.</summary>
        public void Flash(string text, Color color, float seconds = 3f)
        {
            if (!message) return;
            message.text = text;
            message.color = color;
            messageUntil = Time.time + seconds;
        }

        public void ShowBars(Taste? taste, string caption = null)
        {
            if (taste == null) { bars?.SetVisible(false); return; }
            if (!content) return;
            if (bars == null || !bars.IsBuilt)
            {
                bars = GameVisuals.BuildStatBars(content, 0.12f);
                bars.root.transform.localPosition = new Vector3(0, 0.075f, 0);
            }
            bars.SetVisible(true);
            bars.Show(taste.Value, caption);
        }

        void LateUpdate()
        {
            if (!target) { Destroy(gameObject); return; }
            if (!content) return;
            if (!cam) cam = Camera.main;
            transform.position = target.position + Vector3.up * height;
            if (cam)
            {
                Vector3 away = transform.position - cam.transform.position;
                if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
                bool show = showWithin <= 0f || away.sqrMagnitude < showWithin * showWithin;
                if (content.gameObject.activeSelf != show) content.gameObject.SetActive(show);
            }
            if (messageUntil > 0 && Time.time > messageUntil) { message.text = ""; messageUntil = 0; }
        }
    }

    /// <summary>
    /// The three taste bars from the guide: 0 in the middle, positive fills right, negative fills left.
    /// A plain class, not a component: it has no Update of its own. The serve panel holds one set up in its prefab;
    /// floating labels build one from code (<see cref="GameVisuals.BuildStatBars"/>). Faces -Z, like TextMeshPro.
    /// Unity creates a non-null instance for a serialized field, so check <see cref="IsBuilt"/>, not null.
    /// </summary>
    [Serializable]
    public class StatBarsView
    {
        public const int Range = 8;   // a bar is full at +/- this many points

        public GameObject root;
        public float width = 0.12f;
        public Transform[] fills = new Transform[3];
        public TMP_Text[] values = new TMP_Text[3];
        public TMP_Text caption;

        [NonSerialized] bool shown;
        [NonSerialized] Taste last;
        [NonSerialized] string lastCaption;

        public bool IsBuilt => root != null;

        public void SetVisible(bool on) { if (IsBuilt && root.activeSelf != on) root.SetActive(on); }

        public void Show(Taste t, string text = null)
        {
            if (!IsBuilt) return;
            text ??= "";
            if (shown && t == last && text == lastCaption) return;   // nothing changed: no new strings, no text rebuild
            shown = true; last = t; lastCaption = text;
            int[] v = { t.Bitter, t.Sweet, t.Sour };
            for (int i = 0; i < 3; i++)
            {
                if (!fills[i] || !values[i]) continue;
                float f = Mathf.Clamp(v[i], -Range, Range) / (float)Range;
                float len = Mathf.Abs(f) * width * 0.5f;
                var p = fills[i].localPosition;
                fills[i].localPosition = new Vector3(Mathf.Sign(f) * len * 0.5f, p.y, p.z);
                fills[i].localScale = new Vector3(Mathf.Max(len, 0.0001f), fills[i].localScale.y, 1);
                values[i].text = GameVisuals.Signed(v[i]);
            }
            if (caption) caption.text = text;
        }
    }
}
