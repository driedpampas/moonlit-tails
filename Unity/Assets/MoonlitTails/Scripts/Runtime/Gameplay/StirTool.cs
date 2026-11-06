using MoonlitTails.Drinks;
using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>The bar spoon. Swirl its tip around inside the Stir Cup or Shake Mixer to stir.</summary>
    public class StirTool : MonoBehaviour
    {
        [Tooltip("How far the tip has to travel inside a vessel (metres) to count as a stir.")]
        public float stirDistance = 0.35f;

        Vector3 axis;
        float half;
        DrinkContainer current;
        Vector3 lastTip;
        float travelled;

        public void Init()
        {
            var b = GameVisuals.LocalBounds(gameObject);
            Vector3 e = b.extents;
            axis = e.x >= e.y && e.x >= e.z ? Vector3.right : e.y >= e.z ? Vector3.up : Vector3.forward;
            half = GameVisuals.Extent(b, axis);
            center = b.center;
        }

        Vector3 center;

        Vector3 Tip()
        {
            Vector3 a = transform.TransformPoint(center + axis * half * 0.9f);
            Vector3 c = transform.TransformPoint(center - axis * half * 0.9f);
            return a.y < c.y ? a : c;   // the end that's lower is the one in the drink
        }

        void Update()
        {
            Vector3 tip = Tip();
            var vessel = DrinkContainer.FindVesselContaining(tip);
            if (vessel != current) { current = vessel; travelled = 0; lastTip = tip; return; }
            if (!vessel) return;
            Vector3 d = tip - lastTip; d.y = 0;
            travelled += d.magnitude;
            lastTip = tip;
            if (travelled >= stirDistance)
            {
                travelled = 0;
                vessel.TryMix(MixMethod.Stir);
            }
        }
    }
}
