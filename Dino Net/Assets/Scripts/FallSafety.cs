using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Last line of defence: if the player rig ever ends up below the world, put them back at
    /// the respawn point instead of letting them fall forever.
    /// </summary>
    public class FallSafety : MonoBehaviour
    {
        [SerializeField, Tooltip("Root of the XR rig (the object carrying the CharacterController).")]
        Transform m_Rig;

        [SerializeField]
        Transform m_RespawnPoint;

        [SerializeField, Tooltip("World height below which the player is considered lost.")]
        float m_KillHeight = -5f;

        CharacterController m_Controller;

        void Awake()
        {
            if (m_Rig != null)
                m_Controller = m_Rig.GetComponent<CharacterController>();
        }

        void LateUpdate()
        {
            if (m_Rig == null || m_RespawnPoint == null || m_Rig.position.y >= m_KillHeight)
                return;

            // A CharacterController overrides direct position writes unless it's briefly disabled.
            if (m_Controller != null)
                m_Controller.enabled = false;

            m_Rig.SetPositionAndRotation(m_RespawnPoint.position, m_RespawnPoint.rotation);

            if (m_Controller != null)
                m_Controller.enabled = true;
        }
    }
}
