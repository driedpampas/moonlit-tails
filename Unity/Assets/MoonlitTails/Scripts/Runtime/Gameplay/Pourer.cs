using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace MoonlitTails.Gameplay
{
    /// <summary>
    /// Something that can be poured: an ingredient bottle (<see cref="IngredientSource"/>), or a stir cup, shaker or
    /// glass with something in it (<see cref="DrinkContainer"/>).
    /// Tip it past <see cref="pourAngle"/> with its mouth over a container and, after <see cref="pourSeconds"/>, one
    /// pour (1 unit) goes in. Tilt it back up to pour again. A stream shows while pouring.
    /// "Up" is whichever local axis pointed up when the game started (props start upright).
    ///
    /// Subclasses that need a Unity message this class uses (Update) override it and call the base version.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public abstract class Pourer : MonoBehaviour
    {
        [Header("Pouring")]
        public float pourAngle = 100f;
        public float rearmAngle = 60f;
        public float pourSeconds = 0.5f;
        public float dumpSeconds = 1.2f;

        protected Vector3 upAxis;
        protected Bounds bounds;
        LineRenderer stream;
        Color streamColor;
        bool armed = true;
        float progress, dumpProgress;

        public abstract bool HasSomethingToPour { get; }
        public abstract Color StreamColor { get; }
        /// <summary>One pour (1 unit) lands in <paramref name="target"/>. Returns false with a reason if it's refused.</summary>
        public abstract bool PourInto(DrinkContainer target, out string message);
        /// <summary>Tipped out over nothing for a while: empty it. Only called when <see cref="CanDump"/>.</summary>
        public virtual void Dump() { }
        protected virtual bool CanDump => false;

        /// <summary>Works out the up axis and the mouth, and makes the pour stream. Call once from the subclass's Awake.</summary>
        protected void MeasureShape()
        {
            upAxis = GameVisuals.LocalUpAxis(transform);
            bounds = GameVisuals.LocalBounds(gameObject);

            stream = GameVisuals.Spawn<LineRenderer>("PourStream", transform);
            stream.enabled = false;
        }

        public Vector3 UpWorld => transform.TransformDirection(upAxis);
        public float Tilt => Vector3.Angle(UpWorld, Vector3.up);
        /// <summary>The middle of the mouth, in world space.</summary>
        public Vector3 SpoutWorld => transform.TransformPoint(bounds.center + upAxis * GameVisuals.Extent(bounds, upAxis));

        protected virtual void Update()
        {
            float tilt = Tilt;
            if (tilt < rearmAngle) { armed = true; progress = 0; dumpProgress = 0; }

            bool tipped = tilt > pourAngle && armed && HasSomethingToPour;
            if (!tipped)
            {
                if (stream.enabled) stream.enabled = false;
                return;
            }

            Vector3 spout = SpoutWorld;
            DrinkContainer target = DrinkContainer.FindBelow(spout, this as DrinkContainer);
            if (target)
            {
                dumpProgress = 0;
                progress += Time.deltaTime;
                if (progress >= pourSeconds)
                {
                    armed = false;
                    bool ok = PourInto(target, out string message);
                    if (!string.IsNullOrEmpty(message)) target.Label.Flash(message, ok ? GameVisuals.Good : GameVisuals.Bad);
                }
            }
            else if (CanDump)
            {
                progress = 0;
                dumpProgress += Time.deltaTime;
                if (dumpProgress >= dumpSeconds) { armed = false; Dump(); }
            }

            stream.enabled = target || CanDump;
            if (!stream.enabled) return;
            Vector3 end = target ? new Vector3(spout.x, target.OpeningWorld.y, spout.z) : spout + Vector3.down * 0.35f;
            stream.SetPosition(0, spout);
            stream.SetPosition(1, end);
            var c = StreamColor;
            if (c != streamColor || !stream.sharedMaterial) { streamColor = c; stream.sharedMaterial = GameVisuals.Flat(c); }
        }
    }
}
