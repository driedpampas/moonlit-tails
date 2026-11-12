using System.Collections.Generic;
using MoonlitTails.Drinks;
using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>A bottle or carton of one ingredient. Tip it over the Stir Cup or Shake Mixer to pour 1 unit.</summary>
    public class IngredientSource : Pourer
    {
        [Tooltip("What this bottle or carton pours.")]
        [SerializeField] IngredientId ingredient;
        [Tooltip("How far above the top of the bottle (m) its label floats.")]
        [SerializeField] float labelHeight = 0.04f;
        [SerializeField] float labelSize = 0.22f;
        [Tooltip("The label shows when the player's head is closer than this (m).")]
        [SerializeField] float showWithin = 1.2f;

        public Ingredient Ingredient { get; private set; }
        public IngredientId Id => ingredient;
        FloatingLabel label;

        void Awake()
        {
            Ingredient = Ingredients.Get(ingredient);
            if (Ingredient == null)
            {
                Debug.LogError($"[Moonlit] {name}: IngredientSource has no ingredient set", this);
                enabled = false;
                return;
            }
            MeasureShape();

            label = FloatingLabel.Create(transform, SpoutWorld.y - transform.position.y + labelHeight, labelSize);
            label.showWithin = showWithin;
            label.SetTitle(Title(Ingredient));
        }

        public static string Title(Ingredient i)
        {
            var parts = new List<string>();
            var t = i.Taste;
            if (t.Bitter != 0) parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(GameVisuals.BitterColor * 1.5f)}>B{GameVisuals.Signed(t.Bitter)}</color>");
            if (t.Sweet != 0) parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(GameVisuals.SweetColor)}>Sw{GameVisuals.Signed(t.Sweet)}</color>");
            if (t.Sour != 0) parts.Add($"<color=#{ColorUtility.ToHtmlStringRGB(GameVisuals.SourColor)}>So{GameVisuals.Signed(t.Sour)}</color>");
            string kind = i.Kind == IngredientKind.Base ? " <size=70%>(base)</size>" : "";
            return $"<b>{i.DisplayName}</b>{kind} <size=80%>{string.Join(" ", parts)}</size>";
        }

        public override bool HasSomethingToPour => true;
        public override Color StreamColor => GameVisuals.LiquidColor(Ingredient.Taste);
        public override bool PourInto(DrinkContainer target, out string message) => target.ReceiveIngredient(Ingredient, out message);
    }
}
