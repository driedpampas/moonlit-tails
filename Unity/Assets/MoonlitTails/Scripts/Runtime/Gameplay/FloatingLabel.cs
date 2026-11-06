using MoonlitTails.Drinks;
using TMPro;
using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// A label that floats above something and always faces the player: a title line, an optional message that fades
    /// out, and optional taste bars. It isn't parented to the prop, so it doesn't tip over when the prop does.
    /// </summary>
    public class FloatingLabel : MonoBehaviour
    {
        public Transform target;
        public float height = 0.12f;
        [Tooltip("Hide when the player's head is further away than this (0 = always show).")]
        public float showWithin = 0f;

        TextMeshPro title;
        TextMeshPro message;
        StatBars bars;
        Transform content;
        float messageUntil;
        Camera cam;

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

        public void SetTitle(string text) { if (title.text != text) title.text = text; }

        /// <summary>A short message under the title, shown for a few seconds.</summary>
        public void Flash(string text, Color color, float seconds = 3f)
        {
            message.text = text;
            message.color = color;
            messageUntil = Time.time + seconds;
        }

        public void ShowBars(Taste? taste, string caption = null)
        {
            if (taste == null) { if (bars) bars.gameObject.SetActive(false); return; }
            if (!bars)
            {
                bars = StatBars.Create(content, 0.12f);
                bars.transform.localPosition = new Vector3(0, 0.075f, 0);
            }
            bars.gameObject.SetActive(true);
            bars.Show(taste.Value, caption);
        }

        void LateUpdate()
        {
            if (!target) { Destroy(gameObject); return; }
            if (!cam) cam = Camera.main;
            transform.position = target.position + Vector3.up * height;
            if (cam)
            {
                Vector3 away = transform.position - cam.transform.position;
                if (away.sqrMagnitude > 1e-6f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
                bool show = showWithin <= 0f || away.magnitude < showWithin;
                if (content.gameObject.activeSelf != show) content.gameObject.SetActive(show);
            }
            if (messageUntil > 0 && Time.time > messageUntil) { message.text = ""; messageUntil = 0; }
        }
    }
}
