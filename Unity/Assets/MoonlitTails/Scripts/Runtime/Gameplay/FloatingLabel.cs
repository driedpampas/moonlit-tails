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
        [Tooltip("Hide unless the player looks within this many degrees of it (0 = any direction). Fewer labels on screen is fewer things to draw.")]
        public float showLookAngle = 0f;

        float messageUntil;
        Camera cam;
        GameObject backdropRoot;
        Transform backdropFill;
        Transform backdropBorder;

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

        /// <summary>Turns this label into a high-contrast wrapped speech bubble for NPC dialogue.</summary>
        public void ConfigureDialogueBubble(float fontSize = 0.48f, float maxWidth = 1.05f)
        {
            title.text = "";
            message.fontSize = fontSize;
            message.fontStyle = FontStyles.Bold;
            message.textWrappingMode = TextWrappingModes.Normal;
            message.alignment = TextAlignmentOptions.Center;
            message.rectTransform.sizeDelta = new Vector2(maxWidth, 0.4f);
            message.transform.localPosition = Vector3.zero;

            if (!backdropRoot)
            {
                backdropRoot = new GameObject("Backdrop");
                backdropRoot.transform.SetParent(content, false);
                backdropFill = MakeQuad("Fill", backdropRoot.transform, new Vector3(0, 0, 0.008f), new Color(0.08f, 0.09f, 0.14f, 1f));
                backdropBorder = MakeQuad("Border", backdropRoot.transform, new Vector3(0, 0, 0.012f), new Color(0.38f, 0.33f, 0.28f, 1f));
                backdropRoot.SetActive(false);
            }
            UpdateBackdrop();
        }

        static Transform MakeQuad(string name, Transform parent, Vector3 localPos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = GameVisuals.Flat(color);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        void UpdateBackdrop()
        {
            if (!backdropRoot) return;
            bool hasText = !string.IsNullOrEmpty(message.text) && target && target.gameObject.activeInHierarchy;
            backdropRoot.SetActive(hasText);
            if (!hasText) return;
            message.ForceMeshUpdate();
            Vector2 sz = message.GetRenderedValues(true);
            float w = Mathf.Clamp(sz.x + 0.12f, 0.28f, message.rectTransform.sizeDelta.x + 0.12f);
            float h = Mathf.Max(0.11f, sz.y + 0.08f);
            backdropFill.localScale = new Vector3(w, h, 1f);
            backdropBorder.localScale = new Vector3(w + 0.018f, h + 0.018f, 1f);
        }

        public void SetTitle(string text) { if (title.text != text) title.text = text; }

        /// <summary>A short message under the title, shown for a few seconds.</summary>
        public void Flash(string text, Color color, float seconds = 3f)
        {
            message.text = text;
            message.color = color;
            messageUntil = Time.time + seconds;
            UpdateBackdrop();
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
                bool show = (showWithin <= 0f || away.sqrMagnitude < showWithin * showWithin)
                            && (showLookAngle <= 0f || Vector3.Angle(cam.transform.forward, away) < showLookAngle);
                if (content.gameObject.activeSelf != show) content.gameObject.SetActive(show);
            }
            if (messageUntil > 0 && Time.time > messageUntil)
            {
                message.text = "";
                messageUntil = 0;
                UpdateBackdrop();
            }
            else if (backdropRoot && backdropRoot.activeSelf && !target.gameObject.activeInHierarchy)
            {
                backdropRoot.SetActive(false);
            }
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
