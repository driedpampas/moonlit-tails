using System.Collections.Generic;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// What each customer says. PLACEHOLDER WRITING: replace these with the real stories.
    /// Story lines are told one per correct drink, in order, and continue across nights later.
    /// </summary>
    public static class CustomerScripts
    {
        public sealed class Profile
        {
            public string DisplayName;
            public string[] Greetings;
            public string[] Story;
            public string[] Thanks;
            public string[] Wrong;
            public string StoryDone;
        }

        static readonly Dictionary<string, Profile> profiles = new Dictionary<string, Profile>
        {
            ["Kintsu"] = new Profile
            {
                DisplayName = "Kintsu",
                Greetings = new[] { "Evening! Could I get something for me?", "Hi again. I've been thinking about what I'd like..." },
                Story = new[]
                {
                    "My grandmother mended broken cups with gold. She said the cracks are the best part.",
                    "I dropped her favourite teacup last winter. I still have all the pieces.",
                    "I think I'm finally ready to fix it. Gold seams and all.",
                },
                Thanks = new[] { "Oh, that's perfect.", "Exactly what I wanted, thank you!" },
                Wrong = new[] { "Hmm, not quite what I asked for...", "That's lovely, but it's not my order." },
                StoryDone = "Thanks for listening tonight. Same time tomorrow?",
            },
            ["Character03"] = new Profile
            {
                DisplayName = "Fox",
                Greetings = new[] { "Yo, bartender. Make it count.", "Back again. Don't go easy on me." },
                Story = new[]
                {
                    "Band practice ran late. Our drummer quit. Again.",
                    "Found a new drummer. She plays like a thunderstorm in a teacup.",
                    "We've got a gig on Friday. You should come. Bring the good glasses.",
                },
                Thanks = new[] { "Now that's a drink.", "Respect." },
                Wrong = new[] { "Nah, that's not it.", "Close, but no encore." },
                StoryDone = "Alright, that's my set. Later.",
            },
            ["Character02"] = new Profile
            {
                DisplayName = "Guest",
                Greetings = new[] { "Good evening. I'll have the usual... well, today's usual.", "Hello. May I order?" },
                Story = new[]
                {
                    "I used to come here with someone. They moved to the city by the sea.",
                    "They wrote to me today. First letter in a year.",
                    "I think I'll write back. Tonight, maybe. With your drink for courage.",
                },
                Thanks = new[] { "Wonderful, thank you.", "Just right." },
                Wrong = new[] { "Oh... this isn't quite it.", "I think something's different from what I asked." },
                StoryDone = "That's enough remembering for one night.",
            },
        };

        public static Profile For(string id)
        {
            if (id != null && profiles.TryGetValue(id, out var p)) return p;
            return new Profile
            {
                DisplayName = id ?? "Customer",
                Greetings = new[] { "Hi! Can I order?" },
                Story = new string[0],
                Thanks = new[] { "Thanks!" },
                Wrong = new[] { "That's not what I ordered." },
                StoryDone = "Thanks!",
            };
        }
    }
}
