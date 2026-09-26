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

        [SerializeField, Tooltip("Clearance the resting spot needs. If the packet would land inside a rock or a tree it is stepped back toward the child until it is in the open.")]
        float m_ClearRadius = 0.22f;

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

        static readonly RaycastHit[] s_GroundHits = new RaycastHit[16];
        static readonly Collider[] s_Overlap = new Collider[8];

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
            var head = Head();
            var point = transform.position;

            if (head == null)
            {
                m_RestPoint = new Vector3(point.x, GroundHeight(point) + m_RestHeight, point.z);
                Settle();
                return;
            }

            var away = point - head.position;
            away.y = 0f;
            var direction = away.sqrMagnitude > 0.0001f ? away.normalized : Flat(head.forward);

            // Too far, or fallen through the floor: bring it back in front of the child.
            var distance = away.magnitude > m_MaxDropDistance || point.y < m_RescueBelowY
                ? m_MaxDropDistance
                : away.magnitude;

            // Step in toward the child until the packet has somewhere clear to sit. Dropping it
            // against a tree or a rock used to leave it inside the scenery, which reads as the
            // packet simply vanishing.
            m_RestPoint = RestingSpot(head, direction, distance);
            Settle();
        }

        /// <summary>
        /// The nearest clear spot on the floor, starting at <paramref name="distance"/> out from
        /// the child along <paramref name="direction"/> and walking back in if it is blocked.
        /// </summary>
        Vector3 RestingSpot(Transform head, Vector3 direction, float distance)
        {
            var fallback = Vector3.zero;

            for (var step = distance; step >= 0.35f; step -= 0.3f)
            {
                var flat = head.position + direction * step;
                var candidate = new Vector3(flat.x, GroundHeight(flat, head) + m_RestHeight, flat.z);

                if (fallback == Vector3.zero)
                    fallback = candidate;

                if (IsClear(candidate))
                    return candidate;
            }

            // Everything around them is blocked - put it right in front of their feet, which is
            // always somewhere they can see and reach.
            var last = head.position + Flat(head.forward) * 0.6f;
            var floor = new Vector3(last.x, GroundHeight(last, head) + m_RestHeight, last.z);
            return IsClear(floor) || fallback == Vector3.zero ? floor : fallback;
        }

        /// <summary>True when nothing solid is already occupying this spot.</summary>
        bool IsClear(Vector3 point)
        {
            var count = Physics.OverlapSphereNonAlloc(point, m_ClearRadius, s_Overlap, m_GroundMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                if (s_Overlap[i] != null && !s_Overlap[i].transform.IsChildOf(transform))
                    return false;
            }

            return true;
        }

        /// <summary>Freezes the packet at the spot that was chosen for it and leaves it hovering.</summary>
        void Settle()
        {
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

        /// <summary>
        /// The floor under a point, ignoring the packet's own colliders. Only surfaces at or below
        /// the child's own feet count: searching from high up and taking the topmost hit would
        /// perch the packet on a tree canopy, well out of sight.
        /// </summary>
        float GroundHeight(Vector3 point, Transform head = null)
        {
            if (head == null)
                head = Head();

            var feet = head != null ? head.position.y - 1.5f : point.y;
            var ceiling = feet + 0.6f;
            var origin = new Vector3(point.x, ceiling, point.z);
            var count = Physics.RaycastNonAlloc(origin, Vector3.down, s_GroundHits, 8f, m_GroundMask, QueryTriggerInteraction.Ignore);

            var best = float.NegativeInfinity;
            for (var i = 0; i < count; i++)
            {
                if (s_GroundHits[i].collider.transform.IsChildOf(transform))
                    continue;

                // Highest surface that is still underfoot.
                if (s_GroundHits[i].point.y > best && s_GroundHits[i].point.y <= ceiling)
                    best = s_GroundHits[i].point.y;
            }

            if (!float.IsNegativeInfinity(best))
                return best;

            // No floor found - sit it at the child's feet rather than leaving it in mid-air.
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
