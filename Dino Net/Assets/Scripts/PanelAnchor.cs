using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Places a world-space UI panel in front of the headset and leaves it there, so it stays
    /// still enough to point a controller ray at instead of drifting with every head movement.
    /// Panels are built as a plain Transform whose canvas child carries the forward offset - a
    /// RectTransform's position setter routes through anchoredPosition and silently drops the
    /// vertical offset, which puts the panel hundreds of metres away.
    /// </summary>
    public static class PanelAnchor
    {
        public static void PlaceInFront(GameObject panel, float lift = -0.1f)
        {
            if (panel == null)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            panel.transform.SetParent(null, true);
            panel.transform.position = cam.transform.position + Vector3.up * lift;
            panel.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>
        /// True when the player has turned or walked far enough that the panel is no longer
        /// comfortably in view and should be re-placed.
        /// </summary>
        public static bool HasDrifted(GameObject panel, float maxAngle = 55f, float maxDistance = 4.5f)
        {
            if (panel == null)
                return false;

            var cam = Camera.main;
            if (cam == null)
                return false;

            var toPanel = panel.transform.position - cam.transform.position;
            toPanel.y = 0f;
            if (toPanel.magnitude > maxDistance)
                return true;

            var forward = cam.transform.forward;
            forward.y = 0f;
            return Vector3.Angle(forward, panel.transform.forward) > maxAngle;
        }
    }
}
