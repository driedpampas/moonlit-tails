using System;
using MoonlitTails.Drinks;
using TMPro;
using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// A label that floats above something and always faces the player: a title line, an optional message that fades
    /// out, and optional taste bars. It isn't parented to the prop, so it doesn't tip over when the prop does.
    ///
    /// Its look (text, bars, colours) is the prefab <c>Resources/Moonlit/FloatingLabel.prefab</c>. Each thing that
    /// needs one makes a copy with <see cref="Create"/> and says how high above itself it floats.
    /// </summary>
    public class FloatingLabel : MonoBehaviour
    {
        [Header("Set in the prefab")]
        [SerializeField] Transform content;
        [SerializeField] TextMeshPro title;
        [SerializeField] TextMeshPro message;
        [SerializeField] StatBarsView bars = new StatBarsView();

        [Header("Set by whoever makes the label")]
        public Transform target;
        public float height = 0.12f;
        [Tooltip("Hide when the player's head is further away than this (0 = always show).")]
        public float showWithin = 0f;

        float messageUntil;
        Camera cam;

        /// <summary>The camera this label turns to face. Defaults to <see cref="Camera.main"/>; the demo recorder sets its own.</summary>
        public Camera ViewCamera { get => cam; set => cam = value; }

        /// <summary>A copy of the label prefab that floats <paramref name="height"/> metres above <paramref name="target"/>.</summary>
        public static FloatingLabel Create(Transform target, float height, float titleSize = 0.3f)
        {
            var label = GameVisuals.Spawn<FloatingLabel>("FloatingLabel", null);
            label.name = "Label " + target.name;
            label.target = target;
            label.height = height;
            label.title.fontSize = titleSize;
            label.message.fontSize = titleSize * 0.85f;
            label.message.transform.localPosition = new Vector3(0, -0.035f * titleSize / 0.22f, 0);
            return label;
        }

        public void SetTitle(string text) { if (title.text != text) title.text = text; }

        /// <summary>A short message under the title, shown for a few seconds.</summary>
        public void Flash(string text, Color color, float seconds = 3f)
        {
            message.text = text;
            message.color = color;
            messageUntil = Time.time + seconds;
        }

        /// <summary>Shows the taste bars with these values, or hides them with null.</summary>
        public void ShowBars(Taste? taste, string caption = null)
        {
            bars.SetVisible(taste != null);
            if (taste != null) bars.Show(taste.Value, caption);
        }

        void LateUpdate()
        {
            if (!target) { Destroy(gameObject); return; }   // the thing it labelled is gone
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
    /// A plain class, not a component: it has no Update of its own. The serve panel and the floating label each hold
    /// one, set up in their prefabs. Faces -Z, like TextMeshPro.
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

        public void SetVisible(bool on) { if (root.activeSelf != on) root.SetActive(on); }

        public void Show(Taste t, string text = null)
        {
            text ??= "";
            if (shown && t == last && text == lastCaption) return;   // nothing changed: no new strings, no text rebuild
            shown = true; last = t; lastCaption = text;
            for (int i = 0; i < 3; i++)
            {
                int v = i == 0 ? t.Bitter : i == 1 ? t.Sweet : t.Sour;
                float f = Mathf.Clamp(v, -Range, Range) / (float)Range;
                float len = Mathf.Abs(f) * width * 0.5f;
                var p = fills[i].localPosition;
                fills[i].localPosition = new Vector3(Mathf.Sign(f) * len * 0.5f, p.y, p.z);
                fills[i].localScale = new Vector3(Mathf.Max(len, 0.0001f), fills[i].localScale.y, 1);
                values[i].text = GameVisuals.Signed(v);
            }
            caption.text = text;
        }
    }
}
