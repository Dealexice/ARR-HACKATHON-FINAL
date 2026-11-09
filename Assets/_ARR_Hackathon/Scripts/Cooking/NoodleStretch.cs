using UnityEngine;

namespace YataiTogether
{
    /// <summary>
    /// Tag and marker component for stretchable noodles.
    /// Enables collision detection and snapping by BoilerPotSnapReceiver.
    /// </summary>
    [DisallowMultipleComponent]
    public class NoodleStretch : MonoBehaviour
    {
        [Tooltip("True when the noodle has finished its stretch phase and is ready for the boiler.")]
        public bool isStretched = false;
    }
}
