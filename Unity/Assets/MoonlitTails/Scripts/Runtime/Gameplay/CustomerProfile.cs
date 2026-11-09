using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// What a customer says: their greetings, the story they tell one line per correct drink, and how they react.
    /// One asset per character (Assets > Create > Moonlit Tails > Customer Profile), edited in the Inspector.
    /// </summary>
    [CreateAssetMenu(menuName = "Moonlit Tails/Customer Profile", fileName = "CustomerProfile")]
    public class CustomerProfile : ScriptableObject
    {
        public string displayName = "Customer";
        [TextArea] public string[] greetings = { "Hi! Can I order?" };
        [Tooltip("Told one line per correct drink, in order.")]
        [TextArea] public string[] story = new string[0];
        [TextArea] public string[] thanks = { "Thanks!" };
        [TextArea] public string[] wrong = { "That's not what I ordered." };
        [Tooltip("Said after every story line has been told.")]
        [TextArea] public string storyDone = "Thanks!";

        /// <summary>A profile made at runtime (destroy it when done), for a seat that has none.</summary>
        public static CustomerProfile Fallback(string displayName)
        {
            var p = CreateInstance<CustomerProfile>();
            p.name = "Fallback " + displayName;
            p.displayName = string.IsNullOrEmpty(displayName) ? "Customer" : displayName;
            return p;
        }

        /// <summary>Transition only: a runtime copy of the text still in CustomerScripts. Destroy it when done.</summary>
        public static CustomerProfile FromScript(CustomerScripts.Profile s)
        {
            var p = CreateInstance<CustomerProfile>();
            p.name = s.DisplayName;
            p.displayName = s.DisplayName;
            p.greetings = s.Greetings;
            p.story = s.Story;
            p.thanks = s.Thanks;
            p.wrong = s.Wrong;
            p.storyDone = s.StoryDone;
            return p;
        }
    }
}
