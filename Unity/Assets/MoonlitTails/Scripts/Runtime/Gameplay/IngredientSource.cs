using System.Collections.Generic;
using MoonlitTails.Drinks;
using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>A bottle or carton of one ingredient. Tip it over the Stir Cup or Shake Mixer to pour 1 unit.</summary>
    public class IngredientSource : MonoBehaviour, IPourer
    {
        [SerializeField] string ingredientId;
        public Ingredient Ingredient { get; private set; }
        FloatingLabel label;

        public void Init(Ingredient ingredient)
        {
            Ingredient = ingredient;
            ingredientId = ingredient.Id;
            gameObject.AddComponent<PourSpout>().Init(this, false);

            var b = GameVisuals.LocalBounds(gameObject);
            var up = GameVisuals.LocalUpAxis(transform);
            float top = transform.TransformPoint(b.center + up * GameVisuals.Extent(b, up)).y - transform.position.y;
            label = FloatingLabel.Create(transform, top + 0.04f, 0.22f);
            label.showWithin = 1.2f;
            label.SetTitle(Title(ingredient));
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

        public bool HasSomethingToPour => Ingredient != null;
        public Color StreamColor => GameVisuals.LiquidColor(Ingredient.Taste);
        public bool PourInto(DrinkContainer target, out string message) => target.ReceiveIngredient(Ingredient, out message);
        public void Dump() { }
    }
}
