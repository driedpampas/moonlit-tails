using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace MoonlitTails
{
    /// <summary>
    /// Headset settings that can only be set while the game runs, applied once when the headset's display is ready:
    /// foveated rendering (the edges of the view are drawn at a lower resolution, which the eye doesn't notice) and
    /// the physics rate (matched to the headset's refresh rate, so held props move once per drawn frame).
    ///
    /// Without a headset (the editor's simulator, the desk checks) it does nothing.
    /// Foveated rendering also needs the "Foveated Rendering" feature on in Project Settings > XR Plug-in Management > OpenXR.
    /// </summary>
    public class HeadsetDisplay : MonoBehaviour
    {
        [Range(0f, 1f), Tooltip("0 = off, 1 = strongest. Lower it if the edges of the view look blocky.")]
        public float foveation = 0.7f;
        [Tooltip("Run physics at the headset's refresh rate (72, 90 or 120 Hz) instead of the project's Fixed Timestep.")]
        public bool matchPhysicsToRefreshRate = true;
        [Tooltip("Give up waiting for the headset after this many seconds.")]
        public float waitSeconds = 10f;

        static readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();

        IEnumerator Start()
        {
            // the display reports its refresh rate a few frames after it starts
            float giveUpAt = Time.realtimeSinceStartup + waitSeconds;
            XRDisplaySubsystem display = null;
            float hz = 0f;
            while (Time.realtimeSinceStartup < giveUpAt)
            {
                SubsystemManager.GetSubsystems(displays);
                display = displays.Find(d => d.running);
                if (display != null && display.TryGetDisplayRefreshRate(out hz) && hz > 1f) break;
                yield return null;
            }
            if (display == null) yield break;   // no headset

            display.foveatedRenderingLevel = foveation;
            if (matchPhysicsToRefreshRate && hz > 1f) Time.fixedDeltaTime = 1f / hz;
            Debug.Log($"[Moonlit] Headset display: {hz:0} Hz, foveation {display.foveatedRenderingLevel:0.0}, physics step {Time.fixedDeltaTime * 1000f:0.0} ms");
        }
    }
}
