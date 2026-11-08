using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// The big red DO NOT PUSH button on the counter. Pressing it puts every prop (glasses, bottles, cups, tools,
    /// fruit) back where it started, including ones dropped behind the bar. What's in the glasses and cups stays.
    /// R on the keyboard does the same.
    /// </summary>
    public static class ResetButton
    {
        public const string Name = "Reset Button";

        // where it sits, measured from the player spot in the bar model: on the front counter, at the left corner
        // next to the glass rack, out of the way of the ice well and the work mat
        const float ModelPlayerToServe = 1.18f, Forward = 0.67f, Left = 1.05f;

        public static PokeButton Create(Transform parent, Transform player, Transform serveSpot)
        {
            Vector3 forward = serveSpot.position - player.position; forward.y = 0;
            if (forward.sqrMagnitude < 1e-4f) return null;
            float k = forward.magnitude / ModelPlayerToServe;   // follows the bar's scale
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);

            var root = new GameObject(Name).transform;
            root.SetParent(parent, false);
            Vector3 pos = player.position + (forward * Forward - right * Left) * k;
            pos.y = serveSpot.position.y - 0.006f * k;   // the serve marker sits on the coaster, just above the counter
            root.position = pos;
            Vector3 toPlayer = player.position - pos; toPlayer.y = 0;
            root.rotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
            root.localScale = Vector3.one * k;

            var plate = GameVisuals.Primitive(PrimitiveType.Cylinder, root, "Base", new Color(0.12f, 0.12f, 0.14f));
            plate.transform.localScale = new Vector3(0.13f, 0.012f, 0.13f);
            plate.transform.localPosition = new Vector3(0, 0.012f, 0);

            var button = PokeButton.Create(root, "", new Vector3(0, 0.026f, 0), 0.09f, new Color(0.85f, 0.08f, 0.08f), PrimitiveType.Sphere);
            button.Pressed += PropRespawn.ResetAll;

            var caption = GameVisuals.Text(root, "Caption", 0.32f);
            caption.text = "<b>DO NOT PUSH</b>\n<size=55%>puts every object back</size>";
            caption.transform.localPosition = new Vector3(0, 0.14f, 0);
            return button;
        }
    }
}
