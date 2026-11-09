using System;
using System.Collections.Generic;
using UnityEngine;

namespace MoonlitTails
{
    /// <summary>
    /// Every size in the bar scene lives here. It's an asset, so changes made while playing in the headset are kept
    /// when you stop Play mode. BarLayout reads it and applies it live.
    /// </summary>
    [CreateAssetMenu(menuName = "Moonlit Tails/Bar Tuning", fileName = "BarTuning")]
    public class BarTuning : ScriptableObject
    {
        [Header("World")]
        [Tooltip("Uniform scale of the whole bar: room, counters, furniture, props and the spots characters stand or sit on. 1 = as modelled.")]
        [Range(0.6f, 1.6f)] public float environmentScale = 1f;

        [Tooltip("Raises (+) or lowers (-) the floor the player stands on. Makes the counter feel lower or higher without rescaling anything.")]
        [Range(-0.4f, 0.4f)] public float playerFloorOffset = 0f;

        [Header("Props (on top of the world scale)")]
        [Range(0.5f, 2f)] public float propScale = 1f;

        [Tooltip("Extra scale for props whose name contains the text, e.g. 'Glass_' or 'Bottle_'. Rules stack.")]
        public List<ScaleRule> propScaleRules = new List<ScaleRule>
        {
            new ScaleRule("Glass_", 1f), new ScaleRule("Bottle_", 1f), new ScaleRule("Garnish_", 1f),
            new ScaleRule("Fridge_", 1f), new ScaleRule("Shaker", 1f),
        };

        [Header("Characters")]
        public List<CharacterTuning> characters = new List<CharacterTuning>();

        /// <summary>Bumped on every inspector change so BarLayout knows to re-apply while playing.</summary>
        public int Version { get; private set; }
        void OnValidate()
        {
            Version++;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();   // so BarLayout re-applies right away in edit mode
#endif
        }

        public float PropMultiplier(string propName)
        {
            float m = propScale;
            foreach (var r in propScaleRules)
                if (!string.IsNullOrEmpty(r.nameContains) && propName.Contains(r.nameContains)) m *= r.scale;
            return m;
        }

        public CharacterTuning Character(string id)
        {
            var c = characters.Find(x => x.id == id);
            if (c == null) { c = new CharacterTuning { id = id }; characters.Add(c); }
            return c;
        }
    }

    [Serializable]
    public class ScaleRule
    {
        public string nameContains;
        [Range(0.3f, 3f)] public float scale = 1f;
        public ScaleRule() { }
        public ScaleRule(string name, float s) { nameContains = name; scale = s; }
    }

    [Serializable]
    public class CharacterTuning
    {
        public string id;
        [Tooltip("Standing height in metres. All characters are modelled 1.0 m tall.")]
        [Range(0.5f, 1.8f)] public float height = 1f;
        [Tooltip("Nudges the character up (+) or down (-) in metres, e.g. to sit deeper in a seat.")]
        [Range(-0.3f, 0.3f)] public float heightOffset = 0f;
        [Tooltip("Turns the character left/right from facing their target, in degrees.")]
        [Range(-90f, 90f)] public float turn = 0f;
    }
}
