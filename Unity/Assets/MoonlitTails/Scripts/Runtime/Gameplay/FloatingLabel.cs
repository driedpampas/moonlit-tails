using System;
using System.Collections.Generic;
using MoonlitTails.Drinks;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// A label that floats above something and always faces the player: a title line, an optional message that fades
    /// out, and optional taste bars. It isn't parented to the prop, so it doesn't tip over when the prop does.
    ///
    /// Its look (text, bars, colours, the see-through dark backdrop) is the prefab
    /// <c>Resources/Moonlit/FloatingLabel.prefab</c>. Each thing that needs one makes a copy with <see cref="Create"/>
    /// and says how high above itself it floats. The backdrop is resized to fit whatever is showing.
    /// </summary>
    public class FloatingLabel : MonoBehaviour
    {
        [Header("Set in the prefab")]
        [SerializeField] Transform content;
        [SerializeField] TextMeshPro title;
        [SerializeField] TextMeshPro message;
        [SerializeField] StatBarsView bars = new StatBarsView();
        [Tooltip("A quad behind the text, resized to fit it.")]
        [SerializeField] Transform backdrop;

        [Header("Set by whoever makes the label")]
        public Transform target;
        public float height = 0.12f;
        [Tooltip("Show only while a hand is within this many metres of the thing it labels (0 = always show). A message shows anyway while it lasts.")]
        public float showNearHand = 0f;

        float messageUntil;
        bool backdropFits;
        Camera cam;

        static readonly List<Transform> hands = new List<Transform>();
        static float handsFoundAt = float.NegativeInfinity;

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

        /// <summary>Turns this label into a wrapped speech bubble for NPC dialogue.</summary>
        public void ConfigureDialogueBubble(float fontSize = 0.48f, float maxWidth = 1.05f)
        {
            title.text = "";
            message.fontSize = fontSize;
            message.fontStyle = FontStyles.Bold;
            message.textWrappingMode = TextWrappingModes.Normal;
            message.alignment = TextAlignmentOptions.Center;
            message.rectTransform.sizeDelta = new Vector2(maxWidth, 0.4f);
            message.transform.localPosition = Vector3.zero;
            backdropFits = false;
        }

        public void SetTitle(string text)
        {
            if (title.text == text) return;
            title.text = text;
            backdropFits = false;
        }

        /// <summary>A short message under the title, shown for a few seconds.</summary>
        public void Flash(string text, Color color, float seconds = 3f)
        {
            message.text = text;
            message.color = color;
            messageUntil = Time.time + seconds;
            backdropFits = false;
        }

        /// <summary>Shows the taste bars with these values, or hides them with null.</summary>
        public void ShowBars(Taste? taste, string caption = null)
        {
            if (bars.root.activeSelf != (taste != null)) backdropFits = false;
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
            }
            if (messageUntil > 0 && Time.time > messageUntil)
            {
                message.text = "";
                messageUntil = 0;
                backdropFits = false;
            }
            bool show = target.gameObject.activeInHierarchy
                        && (showNearHand <= 0f || messageUntil > 0 || HandWithin(target.position + Vector3.up * (height * 0.5f), showNearHand));
            if (content.gameObject.activeSelf != show) content.gameObject.SetActive(show);
            if (show && !backdropFits) FitBackdrop();
        }

        /// <summary>Sizes the backdrop to cover every piece of text and bar that is showing (only when something changed).</summary>
        void FitBackdrop()
        {
            backdropFits = true;
            if (!backdrop) return;
            var box = new Bounds();
            bool any = false;
            foreach (var r in content.GetComponentsInChildren<Renderer>())
            {
                if (r.transform == backdrop) continue;
                Bounds local;
                if (r.TryGetComponent<TMP_Text>(out var text))
                {
                    if (string.IsNullOrEmpty(text.text)) continue;
                    text.ForceMeshUpdate();
                    local = text.textBounds;
                }
                else if (r.TryGetComponent<MeshFilter>(out var mesh) && mesh.sharedMesh) local = mesh.sharedMesh.bounds;
                else continue;
                if (local.size.x <= 0f) continue;
                Vector3 a = content.InverseTransformPoint(r.transform.TransformPoint(local.min));
                Vector3 b = content.InverseTransformPoint(r.transform.TransformPoint(local.max));
                if (!any) { box = new Bounds(a, Vector3.zero); any = true; }
                box.Encapsulate(a);
                box.Encapsulate(b);
            }
            backdrop.gameObject.SetActive(any);
            if (!any) return;
            float pad = 0.1f * title.fontSize;
            backdrop.localPosition = new Vector3(box.center.x, box.center.y, backdrop.localPosition.z);
            backdrop.localScale = new Vector3(box.size.x + 2f * pad, box.size.y + 2f * pad, 1f);
        }

        /// <summary>Is a hand (the rig's near-far, direct or poke interactor) within this distance of a point?</summary>
        static bool HandWithin(Vector3 point, float distance)
        {
            if (Time.unscaledTime - handsFoundAt > 2f) FindHands();   // looked up again now and then, in case the rig changes
            for (int i = 0; i < hands.Count; i++)
                if (hands[i] && hands[i].gameObject.activeInHierarchy && (hands[i].position - point).sqrMagnitude < distance * distance)
                    return true;
            return false;
        }

        static void FindHands()
        {
            handsFoundAt = Time.unscaledTime;
            hands.Clear();
            foreach (var i in FindObjectsByType<XRBaseInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (i is NearFarInteractor || i is XRDirectInteractor || i is XRPokeInteractor) hands.Add(i.transform);
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
