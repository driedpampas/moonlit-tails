using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails.Gameplay
{
    /// <summary>Dip the scoop in the ice well to fill it, then bring it to a glass's rim: the drink becomes Cold.</summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class IceScoop : MonoBehaviour
    {
        [Tooltip("The ice well the scoop is filled in (ENV_IceWell in the bar).")]
        [SerializeField] Transform iceWell;
        [Tooltip("How close to a glass's rim (m) the scoop has to come.")]
        public float reach = 0.08f;

        public Transform IceWell => iceWell;

        Renderer self;
        bool loaded;
        GameObject cubes;
        FloatingLabel label;
        Bounds wellBounds;
        Transform wellBoundsOf;   // the well the cached bounds belong to
        DrinkContainer refused;   // last glass that said "Already has ice", so it's said once, not every frame

        void Awake()
        {
            self = GetComponent<Renderer>();
            if (!iceWell || !self)
            {
                Debug.LogError($"[Moonlit] {name}: IceScoop needs its ice well set and a Renderer on the same object", this);
                enabled = false;
                return;
            }
            label = FloatingLabel.Create(transform, 0.12f, 0.22f);
            label.showWithin = 1.0f;
            SetLoaded(false);
        }

        /// <summary>The well doesn't move, so its bounds are measured once (again only if the well is changed).</summary>
        Bounds WellBounds()
        {
            if (wellBoundsOf != iceWell)
            {
                wellBoundsOf = iceWell;
                wellBounds = GameVisuals.WorldBounds(iceWell);
                wellBounds.Expand(new Vector3(0.02f, 0.06f, 0.02f));
            }
            return wellBounds;
        }

        void Update()
        {
            Vector3 p = self.bounds.center;
            if (!loaded)
            {
                if (WellBounds().Contains(p)) { SetLoaded(true); label.Flash("Got ice", GameVisuals.Good, 1.5f); }
                return;
            }
            var glass = DrinkContainer.FindGlassNear(p, reach);
            if (!glass || p.y <= glass.OpeningWorld.y - 0.02f) { refused = null; return; }
            if (glass == refused) return;
            bool ok = glass.AddIce(out string msg);
            glass.Label.Flash(msg, ok ? GameVisuals.Good : GameVisuals.Bad, 2f);
            if (ok) { SetLoaded(false); refused = null; }
            else refused = glass;
        }

        void SetLoaded(bool on)
        {
            loaded = on;
            label.SetTitle(on ? "<b>Ice Scoop</b> <size=75%>full: bring it to a glass</size>" : "<b>Ice Scoop</b> <size=75%>dip it in the ice well</size>");
            if (on && !cubes)
            {
                // the Ice prefab is three cubes 0.55 wide: shrink it so each is about 1.4 cm, whatever the scoop's own scale
                float scale = 0.025f / Mathf.Max(transform.lossyScale.x, 1e-4f);
                cubes = GameVisuals.Spawn<GameObject>("Ice", transform);
                cubes.name = "Scooped Ice";
                cubes.transform.localPosition = transform.InverseTransformPoint(self.bounds.center + Vector3.up * 0.008f);
                cubes.transform.localScale = Vector3.one * scale;
            }
            if (cubes) cubes.SetActive(on);
        }
    }
}
