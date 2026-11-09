using System.Collections.Generic;
using System.Linq;
using MoonlitTails.Drinks;
using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// The customers' orders: one ticket per customer on the ticket rail, a speech bubble over each customer, and
    /// the check when a drink is served. A correct drink tells the next line of that customer's story.
    /// </summary>
    public class OrderBook : MonoBehaviour
    {
        public float newOrderDelay = 5f;

        public class Customer
        {
            public string id;
            public Transform body;
            public CustomerScripts.Profile profile;
            public FloatingLabel bubble;
            public FloatingLabel ticket;
            public DrinkOrder order;
            public float nextOrderAt;
            public int storyIndex;
            public bool missed;
        }

        readonly List<Customer> customers = new List<Customer>();
        public IReadOnlyList<Customer> Customers => customers;
        IReadOnlyList<GlassVariant> glasses;
        IReadOnlyList<string> garnishColors;
        System.Random rng;
        public int Served { get; private set; }

        public void Init(IEnumerable<(string id, Transform body)> people, IList<Transform> tickets, IReadOnlyList<GlassVariant> glassTypes, IReadOnlyList<string> colors)
        {
            glasses = glassTypes;
            garnishColors = colors;
            rng = new System.Random();
            int i = 0;
            foreach (var (id, body) in people)
            {
                var c = new Customer { id = id, body = body, profile = CustomerScripts.For(id) };
                c.bubble = FloatingLabel.Create(body, HeadHeight(body) + 0.12f, 0.28f);
                Transform ticketSpot = i < tickets.Count ? tickets[i] : null;
                if (ticketSpot)
                {
                    c.ticket = FloatingLabel.Create(ticketSpot, 0.06f, 0.2f);
                }
                c.nextOrderAt = Time.time + 1.5f + i * 2.5f;   // customers order one after another
                customers.Add(c);
                ShowTicket(c);
                i++;
            }
        }

        static float HeadHeight(Transform body)
        {
            float top = 1f;
            foreach (var r in body.GetComponentsInChildren<Renderer>())
                top = Mathf.Max(top, r.bounds.max.y - body.position.y);
            return top;
        }

        void Update()
        {
            foreach (var c in customers)
            {
                if (c.order != null || Time.time < c.nextOrderAt) continue;
                c.order = OrderGenerator.Generate(rng, glasses, garnishColors);
                c.missed = false;
                Say(c, Pick(c.profile.Greetings), GameVisuals.Ink, 6f);
                ShowTicket(c);
            }
        }

        /// <summary>Redraws a customer's ticket from their current order. Public so the demo recorder can set orders.</summary>
        public void ShowTicket(Customer c)
        {
            if (!c.ticket) return;
            if (c.order == null) { c.ticket.SetTitle($"<b>{c.profile.DisplayName}</b>\n<size=75%>...</size>"); c.ticket.ShowBars(null); return; }
            var o = c.order;
            var extras = new List<string> { o.Method.ToString(), o.GlassVariant };
            if (o.Cold) extras.Add("Cold");
            if (o.Color != null) extras.Add(o.Color + " garnish");
            string hint = c.missed ? $"\n<size=60%><color=#BFE3FF>Tip: {o.Hint}</color></size>" : "";
            c.ticket.SetTitle($"<b>{c.profile.DisplayName}</b>\n<size=75%>{string.Join(" · ", extras)}</size>{hint}");
            c.ticket.ShowBars(o.Target);
        }

        /// <summary>Hands a drink over. Picks the open order it matches best and says how it went.</summary>
        public bool Serve(ServedDrink drink, out string summary)
        {
            Customer best = null; int bestScore = int.MaxValue;
            foreach (var c in customers)
            {
                if (c.order == null) continue;
                int score = Score(Finishing.Check(c.order, drink));
                if (score < bestScore) { bestScore = score; best = c; }
            }
            if (best == null) { summary = "Nobody's waiting for a drink"; return false; }

            var miss = Finishing.Check(best.order, drink);
            if (miss == Mismatch.None)
            {
                Served++;
                string line = best.storyIndex < best.profile.Story.Length ? best.profile.Story[best.storyIndex++] : best.profile.StoryDone;
                Say(best, Pick(best.profile.Thanks) + "\n<size=85%>" + line + "</size>", GameVisuals.Good, 10f);
                best.order = null;
                best.nextOrderAt = Time.time + newOrderDelay + 8f;
                ShowTicket(best);
                summary = $"{best.profile.DisplayName} loved it!";
                return true;
            }

            best.missed = true;
            Say(best, Pick(best.profile.Wrong) + "\n<size=80%>" + Explain(best.order, drink, miss) + "</size>", GameVisuals.Bad, 8f);
            ShowTicket(best);
            summary = $"Not quite right for {best.profile.DisplayName}";
            return false;
        }

        static int Score(Mismatch m)
        {
            int s = 0;
            if ((m & Mismatch.Taste) != 0) s += 10;
            if ((m & Mismatch.Method) != 0) s += 4;
            if ((m & Mismatch.Glass) != 0) s += 3;
            if ((m & Mismatch.Cold) != 0) s += 1;
            if ((m & Mismatch.Color) != 0) s += 1;
            return s;
        }

        static string Explain(DrinkOrder o, ServedDrink d, Mismatch m)
        {
            var bits = new List<string>();
            if ((m & Mismatch.Taste) != 0)
            {
                if (d.Stats.Bitter != o.Target.Bitter) bits.Add(d.Stats.Bitter > o.Target.Bitter ? "too bitter" : "not bitter enough");
                if (d.Stats.Sweet != o.Target.Sweet) bits.Add(d.Stats.Sweet > o.Target.Sweet ? "too sweet" : "not sweet enough");
                if (d.Stats.Sour != o.Target.Sour) bits.Add(d.Stats.Sour > o.Target.Sour ? "too sour" : "not sour enough");
            }
            if ((m & Mismatch.Method) != 0) bits.Add(o.Method == PrepMethod.Layered ? "I wanted it layered" : "I wanted it mixed");
            if ((m & Mismatch.Glass) != 0) bits.Add($"in a {o.GlassVariant} glass, please");
            if ((m & Mismatch.Cold) != 0) bits.Add("with ice");
            if ((m & Mismatch.Color) != 0) bits.Add($"with a {o.Color} garnish");
            return "(" + string.Join(", ", bits) + ")";
        }

        /// <summary>Shows a line in a customer's speech bubble. Public so the demo recorder can script greetings.</summary>
        public void Say(Customer c, string text, Color color, float seconds)
        {
            if (!c.bubble) return;
            c.bubble.SetTitle("");
            c.bubble.Flash(text, color, seconds);
        }

        string Pick(string[] lines) => lines == null || lines.Length == 0 ? "" : lines[rng.Next(lines.Length)];
    }
}
