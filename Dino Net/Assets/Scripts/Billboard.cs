using UnityEngine;

namespace DinoNet
{
    /// <summary>Keeps world-space UI turned toward the player's headset.</summary>
    public class Billboard : MonoBehaviour
    {
        [SerializeField, Tooltip("Keeps the object upright instead of tipping to match the head's pitch.")]
        bool m_LockVerticalTilt = true;

        Transform m_Target;

        void LateUpdate()
        {
            if (m_Target == null)
            {
                var cam = Camera.main;
                if (cam == null)
                    return;

                m_Target = cam.transform;
            }

            var direction = transform.position - m_Target.position;
            if (m_LockVerticalTilt)
                direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction);
        }
    }
}
