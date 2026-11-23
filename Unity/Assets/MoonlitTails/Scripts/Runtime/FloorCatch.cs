using UnityEngine;

namespace MoonlitTails
{
    /// <summary>
    /// Invisible floor trigger covering the room. Any released prop that touches it goes back to its Default Slot
    /// (or to the front of the bar counter if it is a glass or mixing vessel with liquid inside).
    /// </summary>
    public class FloorCatch : MonoBehaviour
    {
        void OnTriggerEnter(Collider other) => Catch(other);
        void OnTriggerStay(Collider other) => Catch(other);

        void Catch(Collider other)
        {
            var prop = other.GetComponentInParent<PropRespawn>();
            if (!prop || prop.Attached || prop.IsHeld) return;
            prop.ResetToHome();
        }
    }
}
