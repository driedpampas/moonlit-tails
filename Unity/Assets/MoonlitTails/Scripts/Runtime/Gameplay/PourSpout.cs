using UnityEngine;

namespace MoonlitTails.Gameplay
{
    /// <summary>Something that can be poured: an ingredient bottle, or a stir cup / shaker / glass with something in it.</summary>
    public interface IPourer
    {
        bool HasSomethingToPour { get; }
        Color StreamColor { get; }
        /// <summary>One pour (1 unit) lands in <paramref name="target"/>. Returns false with a reason if it's refused.</summary>
        bool PourInto(DrinkContainer target, out string message);
        /// <summary>Tipped out over nothing for a while: empty it (only called if the spout can dump).</summary>
        void Dump();
    }

    /// <summary>
    /// Tip a bottle or cup past <see cref="pourAngle"/> with its mouth over a container and, after
    /// <see cref="pourSeconds"/>, one pour (1 unit) goes in. Tilt it back up to pour again. A stream shows while pouring.
    /// "Up" is whichever local axis pointed up when the game started (props start upright).
    /// </summary>
    public class PourSpout : MonoBehaviour
    {
        public float pourAngle = 100f;
        public float rearmAngle = 60f;
        public float pourSeconds = 0.5f;
        public float dumpSeconds = 1.2f;
        public bool canDump;

        IPourer owner;
        DrinkContainer self;
        Vector3 upAxis;
        Bounds bounds;
        LineRenderer stream;
        bool armed = true;
        float progress, dumpProgress;

        public void Init(IPourer pourer, bool dumpable)
        {
            owner = pourer;
            canDump = dumpable;
            self = GetComponent<DrinkContainer>();
            upAxis = GameVisuals.LocalUpAxis(transform);
            bounds = GameVisuals.LocalBounds(gameObject);

            stream = new GameObject("Pour Stream").AddComponent<LineRenderer>();
            stream.transform.SetParent(transform, false);
            stream.useWorldSpace = true;
            stream.positionCount = 2;
            stream.widthMultiplier = 0.008f;
            stream.numCapVertices = 2;
            stream.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            stream.enabled = false;
        }

        public Vector3 UpWorld => transform.TransformDirection(upAxis);
        public float Tilt => Vector3.Angle(UpWorld, Vector3.up);
        /// <summary>The middle of the mouth, in world space.</summary>
        public Vector3 SpoutWorld => transform.TransformPoint(bounds.center + upAxis * GameVisuals.Extent(bounds, upAxis));

        void Update()
        {
            if (owner == null) return;
            float tilt = Tilt;
            if (tilt < rearmAngle) { armed = true; progress = 0; dumpProgress = 0; }

            bool tipped = tilt > pourAngle && owner.HasSomethingToPour && armed;
            Vector3 spout = SpoutWorld;
            DrinkContainer target = tipped ? DrinkContainer.FindBelow(spout, self) : null;

            if (tipped)
            {
                if (target)
                {
                    dumpProgress = 0;
                    progress += Time.deltaTime;
                    if (progress >= pourSeconds)
                    {
                        armed = false;
                        bool ok = owner.PourInto(target, out string message);
                        if (!string.IsNullOrEmpty(message)) target.Label?.Flash(message, ok ? GameVisuals.Good : GameVisuals.Bad);
                    }
                }
                else if (canDump)
                {
                    progress = 0;
                    dumpProgress += Time.deltaTime;
                    if (dumpProgress >= dumpSeconds) { armed = false; owner.Dump(); }
                }
            }

            stream.enabled = tipped && (target || canDump);
            if (stream.enabled)
            {
                Vector3 end = target ? new Vector3(spout.x, target.OpeningWorld.y, spout.z) : spout + Vector3.down * 0.35f;
                stream.SetPosition(0, spout);
                stream.SetPosition(1, end);
                stream.sharedMaterial = GameVisuals.Flat(owner.StreamColor);
            }
        }
    }
}
