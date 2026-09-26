using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace DinoNet
{
    /// <summary>
    /// The glowing data packet the child carries between dino nodes. Hovers at its home anchor
    /// until it is first picked up. When it is let go - including on purpose, to point at a
    /// question - it drops to the ground within reach and waits there to be picked up again,
    /// rather than rolling away or vanishing back to the podium.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class CarryableOrb : MonoBehaviour
    {
        [SerializeField, Tooltip("Where the packet waits before the child first picks it up.")]
        Transform m_HomeAnchor;

        [SerializeField]
        float m_BobHeight = 0.1f;

        [SerializeField]
        float m_BobSpeed = 1.5f;

        [SerializeField]
        float m_SpinSpeed = 45f;

        [Header("Dropping")]
        [SerializeField, Tooltip("How far from the child the packet is allowed to come to rest. Anything further is pulled back in, so it is always within a few steps.")]
        float m_MaxDropDistance = 2.2f;

        [SerializeField, Tooltip("How high above the ground the dropped packet hovers, so it never sinks into the grass.")]
        float m_RestHeight = 0.35f;

        [SerializeField, Tooltip("Longest it is allowed to tumble before it is parked. It usually settles well before this.")]
        float m_MaxFallSeconds = 1.6f;

        [SerializeField, Tooltip("What counts as ground when working out where the packet lands.")]
        LayerMask m_GroundMask = ~0;

        [SerializeField, Tooltip("Below this height the packet has fallen out of the world and is brought back to the child's feet.")]
        float m_RescueBelowY = -3f;

        [Header("Glow")]
        [SerializeField]
        Light m_GlowLight;

        [SerializeField, Tooltip("Min/max intensity of the packet's pulsing glow.")]
        Vector2 m_GlowIntensityRange = new Vector2(1.5f, 3.5f);

        [SerializeField]
        float m_GlowPulseSpeed = 2f;

        [SerializeField, Tooltip("Extra glow while the packet is sitting on the ground, so it is easy to spot again.")]
        float m_RestingGlowBoost = 1.4f;

        public event Action FirstPickedUp;

        /// <summary>Raised when the packet comes to rest on the ground after being let go.</summary>
        public event Action Dropped;

        static readonly RaycastHit[] s_GroundHits = new RaycastHit[8];

        XRGrabInteractable m_Interactable;
        Rigidbody m_Rigidbody;
        Transform m_Head;
        bool m_Docked = true;
        bool m_Resting;
        bool m_EverPickedUp;
        float m_FallTimer;
        Vector3 m_RestPoint;

        public bool IsHeld => m_Interactable != null && m_Interactable.isSelected;

        /// <summary>True while the packet is sitting on the ground waiting to be picked back up.</summary>
        public bool IsResting => m_Resting;

        void Awake()
        {
            m_Interactable = GetComponent<XRGrabInteractable>();
            m_Rigidbody = GetComponent<Rigidbody>();
            Dock();
        }

        void OnEnable()
        {
            m_Interactable.selectEntered.AddListener(OnSelectEntered);
            m_Interactable.selectExited.AddListener(OnSelectExited);
        }

        void OnDisable()
        {
            m_Interactable.selectEntered.RemoveListener(OnSelectEntered);
            m_Interactable.selectExited.RemoveListener(OnSelectExited);
        }

        void Update()
        {
            Glow();

            if (IsHeld)
                return;

            if (m_Docked)
            {
                if (m_HomeAnchor != null)
                    Hover(m_HomeAnchor.position);

                return;
            }

            if (m_Resting)
            {
                Hover(m_RestPoint);
                return;
            }

            // In the air after being let go. Park it as soon as it has settled, so it can never
            // roll off somewhere a child cannot follow.
            m_FallTimer += Time.deltaTime;

            var stopped = m_Rigidbody.linearVelocity.sqrMagnitude < 0.09f && m_FallTimer > 0.3f;
            if (stopped || m_FallTimer > m_MaxFallSeconds || transform.position.y < m_RescueBelowY)
                RestNearPlayer();
        }

        void Hover(Vector3 around)
        {
            var bob = Mathf.Sin(Time.time * m_BobSpeed) * m_BobHeight;
            transform.position = around + Vector3.up * bob;
            transform.Rotate(Vector3.up, m_SpinSpeed * Time.deltaTime, Space.World);
        }

        void Glow()
        {
            if (m_GlowLight == null)
                return;

            var t = (Mathf.Sin(Time.time * m_GlowPulseSpeed) + 1f) * 0.5f;
            var intensity = Mathf.Lerp(m_GlowIntensityRange.x, m_GlowIntensityRange.y, t);
            m_GlowLight.intensity = m_Resting ? intensity * m_RestingGlowBoost : intensity;
        }

        void OnSelectEntered(SelectEnterEventArgs args)
        {
            m_Docked = false;
            m_Resting = false;
            m_FallTimer = 0f;
            m_Rigidbody.isKinematic = false;
            m_Rigidbody.useGravity = true;

            if (m_EverPickedUp)
                return;

            m_EverPickedUp = true;
            FirstPickedUp?.Invoke();
        }

        void OnSelectExited(SelectExitEventArgs args)
        {
            // Let it drop naturally for a moment; Update parks it once it has settled.
            m_Docked = false;
            m_Resting = false;
            m_FallTimer = 0f;
        }

        /// <summary>
        /// Parks the packet on the ground within a couple of steps of the child and leaves it
        /// hovering there. Used whenever it is let go, so putting it down to answer a question
        /// never costs you the packet.
        /// </summary>
        public void RestNearPlayer()
        {
            var point = transform.position;
            var head = Head();

            if (head != null)
            {
                var away = point - head.position;
                away.y = 0f;

                // Too far, or fallen through the floor: bring it back in front of the child.
                if (away.magnitude > m_MaxDropDistance || point.y < m_RescueBelowY)
                {
                    var direction = away.sqrMagnitude > 0.0001f ? away.normalized : Flat(head.forward);
                    point = head.position + direction * m_MaxDropDistance;
                }

                point.y = head.position.y;
            }

            m_RestPoint = new Vector3(point.x, GroundHeight(point) + m_RestHeight, point.z);

            if (!m_Rigidbody.isKinematic)
            {
                m_Rigidbody.linearVelocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
            }

            m_Rigidbody.isKinematic = true;
            m_Rigidbody.useGravity = false;
            transform.position = m_RestPoint;

            m_Docked = false;
            m_Resting = true;
            m_FallTimer = 0f;
            Dropped?.Invoke();
        }

        /// <summary>Finds the floor under a point, ignoring the packet's own colliders.</summary>
        float GroundHeight(Vector3 point)
        {
            var origin = new Vector3(point.x, point.y + 3f, point.z);
            var count = Physics.RaycastNonAlloc(origin, Vector3.down, s_GroundHits, 20f, m_GroundMask, QueryTriggerInteraction.Ignore);

            var best = float.NegativeInfinity;
            for (var i = 0; i < count; i++)
            {
                if (s_GroundHits[i].collider.transform.IsChildOf(transform))
                    continue;

                if (s_GroundHits[i].point.y > best)
                    best = s_GroundHits[i].point.y;
            }

            if (!float.IsNegativeInfinity(best))
                return best;

            // No floor found - sit it at the child's feet rather than leaving it in mid-air.
            var head = Head();
            return head != null ? head.position.y - 1.4f : 0f;
        }

        Transform Head()
        {
            if (m_Head == null && Camera.main != null)
                m_Head = Camera.main.transform;

            return m_Head;
        }

        static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        }

        /// <summary>Parks the packet back at its home anchor, hovering and waiting to be grabbed.</summary>
        public void Dock()
        {
            m_Docked = true;
            m_Resting = false;
            m_FallTimer = 0f;

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
