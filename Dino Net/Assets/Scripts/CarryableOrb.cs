using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DinoNet
{
    /// <summary>
    /// The glowing data packet the child carries between dino nodes. Hovers at its home anchor
    /// until it is first picked up, and returns there if it is ever dropped out of the world.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class CarryableOrb : MonoBehaviour
    {
        [SerializeField, Tooltip("Where the orb waits before the child picks it up. Also where it respawns if dropped out of the world.")]
        Transform m_HomeAnchor;

        [SerializeField]
        float m_BobHeight = 0.1f;

        [SerializeField]
        float m_BobSpeed = 1.5f;

        [SerializeField]
        float m_SpinSpeed = 45f;

        [SerializeField, Tooltip("If the orb falls below this world height it is returned to its home anchor.")]
        float m_RespawnBelowY = -3f;

        [SerializeField]
        Light m_GlowLight;

        [SerializeField, Tooltip("Min/max intensity of the orb's pulsing glow.")]
        Vector2 m_GlowIntensityRange = new Vector2(1.5f, 3.5f);

        [SerializeField]
        float m_GlowPulseSpeed = 2f;

        public event Action FirstPickedUp;

        XRGrabInteractable m_Interactable;
        Rigidbody m_Rigidbody;
        bool m_Docked = true;
        bool m_EverPickedUp;

        public bool IsHeld => m_Interactable != null && m_Interactable.isSelected;

        void Awake()
        {
            m_Interactable = GetComponent<XRGrabInteractable>();
            m_Rigidbody = GetComponent<Rigidbody>();
            Dock();
        }

        void OnEnable()
        {
            m_Interactable.selectEntered.AddListener(OnSelectEntered);
        }

        void OnDisable()
        {
            m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
        }

        void Update()
        {
            if (m_GlowLight != null)
            {
                var t = (Mathf.Sin(Time.time * m_GlowPulseSpeed) + 1f) * 0.5f;
                m_GlowLight.intensity = Mathf.Lerp(m_GlowIntensityRange.x, m_GlowIntensityRange.y, t);
            }

            if (m_Docked && m_HomeAnchor != null)
            {
                var bob = Mathf.Sin(Time.time * m_BobSpeed) * m_BobHeight;
                transform.position = m_HomeAnchor.position + Vector3.up * bob;
                transform.Rotate(Vector3.up, m_SpinSpeed * Time.deltaTime, Space.World);
            }
            else if (!IsHeld && transform.position.y < m_RespawnBelowY)
            {
                Dock();
            }
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            m_Docked = false;
            m_Rigidbody.isKinematic = false;
            m_Rigidbody.useGravity = true;

            if (m_EverPickedUp)
                return;

            m_EverPickedUp = true;
            FirstPickedUp?.Invoke();
        }

        /// <summary>Parks the orb back at its home anchor, hovering and waiting to be grabbed.</summary>
        public void Dock()
        {
            m_Docked = true;

            // Velocities can only be cleared while the body is still dynamic.
            if (!m_Rigidbody.isKinematic)
            {
                m_Rigidbody.linearVelocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }

            m_Rigidbody.isKinematic = true;
            m_Rigidbody.useGravity = false;

            if (m_HomeAnchor != null)
                transform.position = m_HomeAnchor.position;
        }

        public void SetHomeAnchor(Transform anchor) => m_HomeAnchor = anchor;
    }
}
