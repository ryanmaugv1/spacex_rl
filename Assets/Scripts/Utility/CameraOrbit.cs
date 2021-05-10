using UnityEngine;

namespace Utility
{
    public class CameraOrbit : MonoBehaviour
    {
        /// Target for camera to follow.
        public Transform Target;
        /// Camera anchor which we rotate to simulate camera orbit.
        public Transform CameraAnchor;
        /// Camera anchor offset relative to target position.
        public Vector3 AnchorOffset;
        /// Camera position offset locally within camera anchor.
        public Vector3 CameraOffset;
        /// Speed/sensitivity of camera orbit (m/s).
        public float OrbitSpeed;


        private void Start() {
            // Hide and confines cursor to center of game window.
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;

            // Locally offset camera position within anchor.
            transform.localPosition = CameraOffset;
        }


        private void FixedUpdate()
        {
            // Set camera anchor position with offset relative to target position.
            CameraAnchor.position = Target.position + AnchorOffset;

            // Use mouse x, y input to rotate camera anchor (which acts as target).
            float horizontal = Input.GetAxis("Mouse X") * OrbitSpeed * Time.deltaTime;
            float vertical = Input.GetAxis("Mouse Y") * OrbitSpeed * Time.deltaTime;

            // Rotate horizontally in world space to prevent rotating around local axis.
            CameraAnchor.Rotate(0, horizontal, 0, Space.World);

            // Rotate vertically in local space.
            CameraAnchor.Rotate(vertical, 0, 0, Space.Self);

            // Ensure that we always look at target (prevents needed to clamp vertical angle).
            transform.LookAt(Target.position);
        }
    }
}
