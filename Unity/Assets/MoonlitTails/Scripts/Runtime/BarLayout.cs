using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace MoonlitTails
{
    /// <summary>
    /// Applies the numbers in <see cref="BarTuning"/> to the scene: room scale, prop scale, where the player stands,
    /// and where each character stands or sits. Runs in edit mode too, so you see changes immediately.
    /// Positions come from the MK_ markers inside the bar model, so they follow any rescale.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-100)]
    public class BarLayout : MonoBehaviour
    {
        public BarTuning tuning;

        [Header("Scene references (filled by Tools > Moonlit Tails > Rebuild VR Scene)")]
        public Transform environment;
        public Transform xrOrigin;
        [Tooltip("Where the player's feet go (MK_PlayerStart).")] public Transform playerSpot;
        [Tooltip("What the player faces when the scene starts (MK_ServeSpot).")] public Transform playerFacing;
        public List<CharacterSlot> characters = new List<CharacterSlot>();
        public List<PropRecord> props = new List<PropRecord>();

        int appliedVersion = -1;

        [Serializable]
        public class CharacterSlot
        {
            public string id;
            public Transform character;
            [Tooltip("Where the character stands, or the top of the seat they sit on.")] public Transform spot;
            [Tooltip("What the character faces.")] public Transform faceTowards;
            public bool seated;
            [Tooltip("Hip height above the feet in the sitting pose for a 1 m character (measured by the scene builder).")]
            public float seatedHipHeight = 0.3f;
            [Tooltip("How far the hip joint sits above the seat surface for a 1 m character.")]
            public float hipAboveSeat = 0.07f;
        }

        [Serializable]
        public class PropRecord
        {
            public Transform prop;
            public Vector3 baseScale = Vector3.one;
        }

        void OnEnable() => appliedVersion = -1;

        int playFrames;
        void LateUpdate()
        {
            // a few frames into Play mode the sitting animation is running: measure the real hip height once
            // and re-seat, so characters sit on the seat whatever their proportions
            if (!Application.isPlaying || ++playFrames != 3) return;
            foreach (var c in characters)
            {
                if (!c.seated || !c.character || !c.character.TryGetComponent<Animator>(out var anim)) continue;
                var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                if (hips) c.seatedHipHeight = c.character.InverseTransformPoint(hips.position).y;
                PlaceCharacter(c);
            }
        }

        void Update()
        {
            if (tuning == null) return;
            // edit mode: always (cheap, and keeps the scene in sync while you drag sliders)
            // play mode: only when the tuning asset changed, so thrown props aren't disturbed
            if (Application.isPlaying && appliedVersion == tuning.Version) return;
            appliedVersion = tuning.Version;
            Apply();
        }

        public void Apply()
        {
            if (tuning == null) return;
            if (environment) SetScale(environment, Vector3.one * tuning.environmentScale);
            foreach (var p in props)
                if (p.prop) SetScale(p.prop, p.baseScale * tuning.PropMultiplier(p.prop.name));
            PlacePlayer();
            foreach (var c in characters) PlaceCharacter(c);
            ApplyHandSettings();
        }

        void PlacePlayer()
        {
            if (!xrOrigin || !playerSpot) return;
            SetPosition(xrOrigin, playerSpot.position + Vector3.up * tuning.playerFloorOffset);
            if (!playerFacing) return;
            Vector3 d = playerFacing.position - playerSpot.position; d.y = 0;
            if (d.sqrMagnitude > 1e-4f) SetRotation(xrOrigin, Quaternion.LookRotation(d));
        }

        void PlaceCharacter(CharacterSlot c)
        {
            if (!c.character || !c.spot) return;
            var t = tuning.Character(c.id);
            float h = t.height;                       // characters are modelled 1 m tall, so height == scale
            SetScale(c.character, Vector3.one * h);

            Vector3 pos = c.spot.position;
            if (c.seated) pos.y += (c.hipAboveSeat - c.seatedHipHeight) * h;
            pos.y += t.heightOffset;
            SetPosition(c.character, pos);

            Vector3 look = c.faceTowards ? c.faceTowards.position - c.spot.position : c.spot.forward;
            look.y = 0;
            if (look.sqrMagnitude > 1e-4f) SetRotation(c.character, Quaternion.LookRotation(look) * Quaternion.Euler(0, t.turn, 0));
        }

        void ApplyHandSettings()
        {
            if (Application.isPlaying) Time.fixedDeltaTime = 1f / tuning.physicsRate;
            foreach (var p in props)
                if (p.prop && p.prop.TryGetComponent<XRGrabInteractable>(out var g)) g.throwVelocityScale = tuning.throwStrength;
            if (xrOrigin)
                foreach (var i in xrOrigin.GetComponentsInChildren<NearFarInteractor>(true)) i.enableFarCasting = tuning.allowDistanceGrab;
        }

        // only write when the value really changes, so edit mode doesn't keep dirtying the scene
        static void SetScale(Transform t, Vector3 s) { if ((t.localScale - s).sqrMagnitude > 1e-10f) t.localScale = s; }
        static void SetPosition(Transform t, Vector3 p) { if ((t.position - p).sqrMagnitude > 1e-10f) t.position = p; }
        static void SetRotation(Transform t, Quaternion r) { if (Quaternion.Angle(t.rotation, r) > 0.01f) t.rotation = r; }
    }
}
