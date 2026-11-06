using MoonlitTails.Drinks;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// The serve spot: put a finished glass on it, use the end-of-drink stat adjustment on the panel (1 point for a
    /// mixed drink, 2 for a layered one), then ring the bell (or press Serve) to hand it over.
    /// </summary>
    public class ServeStation : MonoBehaviour
    {
        public Transform spot;
        public float radius = 0.15f;

        OrderBook orders;
        DrinkContainer glass;
        StatBars bars;
        TextMeshPro title, info;
        Transform panel;

        public void Init(Transform serveSpot, Transform player, Transform bell, OrderBook book)
        {
            spot = serveSpot;
            orders = book;

            // panel beside the serve spot, facing the player
            Vector3 toPlayer = player.position - serveSpot.position; toPlayer.y = 0;
            if (toPlayer.sqrMagnitude < 1e-4f) toPlayer = Vector3.back;
            toPlayer.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toPlayer);
            panel = new GameObject("Serve Panel").transform;
            panel.SetParent(transform, false);
            panel.position = serveSpot.position + toPlayer * 0.1f + side * 0.28f + Vector3.up * 0.22f;
            panel.rotation = Quaternion.LookRotation(-toPlayer, Vector3.up);

            title = GameVisuals.Text(panel, "Title", 0.3f);
            title.transform.localPosition = new Vector3(0, 0.1f, 0);
            info = GameVisuals.Text(panel, "Info", 0.2f);
            info.transform.localPosition = new Vector3(0, -0.075f, 0);
            bars = StatBars.Create(panel, 0.14f);
            bars.transform.localPosition = new Vector3(0, 0.035f, 0);

            string[] names = { "Bitter", "Sweet", "Sour" };
            for (int i = 0; i < 3; i++)
            {
                float y = 0.035f - i * 0.018f;
                int stat = i;
                var minus = PokeButton.Create(panel, "-", new Vector3(-0.155f, y, 0), 0.016f, new Color(0.35f, 0.3f, 0.6f));
                var plus = PokeButton.Create(panel, "+", new Vector3(0.155f, y, 0), 0.016f, new Color(0.35f, 0.3f, 0.6f));
                minus.radius = plus.radius = 0.014f;
                minus.Pressed += () => Adjust(stat, -1);
                plus.Pressed += () => Adjust(stat, +1);
            }
            var serve = PokeButton.Create(panel, "Serve", new Vector3(0, -0.15f, 0), 0.05f, new Color(0.25f, 0.55f, 0.4f));
            serve.Pressed += Serve;

            if (bell)
            {
                var ring = bell.gameObject.AddComponent<PokeButton>();
                ring.radius = 0.06f;
                ring.Pressed += Serve;
                if (bell.TryGetComponent<XRGrabInteractable>(out var g)) g.selectEntered.AddListener((SelectEnterEventArgs _) => Serve());
            }
        }

        void Update()
        {
            glass = FindGlass();
            if (!glass)
            {
                title.text = "<b>Serve here</b>";
                info.text = $"Put a glass on the coaster\n<size=80%>Served tonight: {orders.Served}</size>";
                bars.Show(Taste.Zero, "");
                return;
            }
            title.text = $"<b>{glass.variant}</b> <size=70%>{(glass.IsEmpty ? "empty" : glass.Method.ToString())}</size>";
            string extras = (glass.Cold ? "Cold" : "not cold") + " · " + (glass.Garnish ? glass.Garnish.color + " garnish" : "no garnish");
            info.text = $"{extras}\n<size=80%>Adjustment points left: {glass.AdjustmentLeft} · then ring the bell</size>";
            bars.Show(glass.FinalStats, "");
        }

        DrinkContainer FindGlass()
        {
            DrinkContainer best = null; float bestD = radius;
            foreach (var c in DrinkContainer.All)
            {
                if (!c.IsGlass || c.IsHeld) continue;
                Vector3 d = c.transform.position - spot.position;
                if (Mathf.Abs(d.y) > 0.2f) continue;
                d.y = 0;
                if (d.magnitude < bestD) { bestD = d.magnitude; best = c; }
            }
            return best;
        }

        void Adjust(int stat, int delta)
        {
            if (!glass) return;
            var d = stat == 0 ? new Taste(delta, 0, 0) : stat == 1 ? new Taste(0, delta, 0) : new Taste(0, 0, delta);
            if (!glass.TryAdjust(d, out var msg) && msg != null) glass.Label.Flash(msg, GameVisuals.Bad);
        }

        public void Serve()
        {
            if (!glass || glass.IsEmpty)
            {
                info.text = "Put a filled glass on the coaster first";
                return;
            }
            bool ok = orders.Serve(glass.ToServed(), out string summary);
            glass.Label.Flash(summary, ok ? GameVisuals.Good : GameVisuals.Bad);
            if (ok) glass.ServedAndReturned();
        }
    }
}
