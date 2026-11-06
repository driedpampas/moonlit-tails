using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>Dip the scoop in the ice well to fill it, then bring it to a glass's rim: the drink becomes Cold.</summary>
    public class IceScoop : MonoBehaviour
    {
        public Transform iceWell;
        public float reach = 0.08f;

        Renderer self;
        bool loaded;
        GameObject cubes;
        FloatingLabel label;

        public void Init(Transform well)
        {
            iceWell = well;
            self = GetComponent<Renderer>();
            label = FloatingLabel.Create(transform, 0.12f, 0.22f);
            label.showWithin = 1.0f;
            SetLoaded(false);
        }

        void Update()
        {
            if (!self) return;
            Vector3 p = self.bounds.center;
            if (!loaded && iceWell)
            {
                var b = GameVisuals.WorldBounds(iceWell);
                b.Expand(new Vector3(0.02f, 0.06f, 0.02f));
                if (b.Contains(p)) { SetLoaded(true); label.Flash("Got ice", GameVisuals.Good, 1.5f); }
            }
            else if (loaded)
            {
                var glass = DrinkContainer.FindGlassNear(p, reach);
                if (glass && p.y > glass.OpeningWorld.y - 0.02f)
                {
                    bool ok = glass.AddIce(out string msg);
                    glass.Label.Flash(msg, ok ? GameVisuals.Good : GameVisuals.Bad, 2f);
                    if (ok) SetLoaded(false);
                }
            }
        }

        void SetLoaded(bool on)
        {
            loaded = on;
            label.SetTitle(on ? "<b>Ice Scoop</b> <size=75%>full: bring it to a glass</size>" : "<b>Ice Scoop</b> <size=75%>dip it in the ice well</size>");
            if (on && !cubes)
            {
                cubes = new GameObject("Scooped Ice");
                cubes.transform.SetParent(transform, false);
                cubes.transform.localPosition = transform.InverseTransformPoint(self.bounds.center);
                for (int i = 0; i < 3; i++)
                {
                    var c = GameVisuals.Primitive(PrimitiveType.Cube, cubes.transform, "Ice", new Color(0.85f, 0.95f, 1f));
                    c.transform.localPosition = new Vector3((i - 1) * 0.012f, 0.008f, 0) / Mathf.Max(transform.lossyScale.x, 1e-4f);
                    c.transform.localRotation = Quaternion.Euler(15 * i, 30 * i, 0);
                    c.transform.localScale = Vector3.one * 0.014f / Mathf.Max(transform.lossyScale.x, 1e-4f);
                }
            }
            if (cubes) cubes.SetActive(on);
        }
    }
}
