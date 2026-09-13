using System.Collections;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// A dinosaur that acts as a stop on the delivery route. Owns the ground ring the child
    /// walks into, plus the celebration VFX/audio played when the orb arrives.
    /// </summary>
    public class QuestNode : MonoBehaviour
    {
        [SerializeField, Tooltip("Name used in hint/among banner text, e.g. \"Spiky Stego\".")]
        string m_FriendlyName;

        [SerializeField, Tooltip("Ground point the orb must be carried to. Defaults to this transform.")]
        Transform m_DeliveryAnchor;

        [SerializeField, Tooltip("How close the orb must get to count as delivered.")]
        float m_DeliveryRadius = 2.5f;

        [SerializeField, Tooltip("High point on the dino where energy vines attach. Defaults to the delivery anchor.")]
        Transform m_VineAnchor;

        [SerializeField, Tooltip("Glowing ring on the ground marking this node.")]
        Renderer m_Ring;

        [SerializeField]
        ParticleSystem m_CelebrationVfx;

        [SerializeField]
        AudioSource m_AudioSource;

        [SerializeField]
        Color m_IdleColor = new Color(0.25f, 0.8f, 1f);

        [SerializeField]
        Color m_CompletedColor = new Color(0.3f, 1f, 0.45f);

        [SerializeField]
        Color m_WrongColor = new Color(1f, 0.72f, 0.25f);

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock m_Block;
        Coroutine m_FlashRoutine;
        float m_PulseBoost;

        public string FriendlyName => string.IsNullOrEmpty(m_FriendlyName) ? name : m_FriendlyName;
        public Transform DeliveryAnchor => m_DeliveryAnchor != null ? m_DeliveryAnchor : transform;
        public Transform VineAnchor => m_VineAnchor != null ? m_VineAnchor : DeliveryAnchor;
        public float DeliveryRadius => m_DeliveryRadius;
        public bool IsCompleted { get; private set; }

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            ApplyRingColor(m_IdleColor, 1f);
        }

        void Update()
        {
            if (IsCompleted || m_Ring == null)
                return;

            // Gentle idle shimmer, boosted while this node is being hinted.
            var pulse = 0.75f + Mathf.Sin(Time.time * 2f) * 0.25f + m_PulseBoost;
            ApplyRingColor(m_IdleColor, pulse);
        }

        public bool IsOrbInRange(Vector3 orbPosition)
        {
            var a = orbPosition;
            var b = DeliveryAnchor.position;
            // Ignore height so a carried orb still counts next to a tall dino.
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b) <= m_DeliveryRadius;
        }

        public void PlayArrival(AudioClip clip)
        {
            IsCompleted = true;
            m_PulseBoost = 0f;
            ApplyRingColor(m_CompletedColor, 2.5f);

            if (m_CelebrationVfx != null)
                m_CelebrationVfx.Play();

            if (m_AudioSource != null && clip != null)
                m_AudioSource.PlayOneShot(clip);
        }

        public void PlayWrongNode(AudioClip clip)
        {
            if (m_AudioSource != null && clip != null)
                m_AudioSource.PlayOneShot(clip);

            if (m_FlashRoutine != null)
                StopCoroutine(m_FlashRoutine);

            m_FlashRoutine = StartCoroutine(FlashWrong());
        }

        /// <summary>Brightens this node's ring so a stuck child can spot where to go next.</summary>
        public void SetHinted(bool hinted)
        {
            m_PulseBoost = hinted ? 1.5f : 0f;
        }

        IEnumerator FlashWrong()
        {
            var elapsed = 0f;
            while (elapsed < 0.6f)
            {
                elapsed += Time.deltaTime;
                ApplyRingColor(m_WrongColor, 1.5f);
                yield return null;
            }

            m_FlashRoutine = null;
        }

        void ApplyRingColor(Color color, float intensity)
        {
            if (m_Ring == null)
                return;

            m_Ring.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_BaseColor, color);
            m_Block.SetColor(k_EmissionColor, color * intensity);
            m_Ring.SetPropertyBlock(m_Block);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(DeliveryAnchor.position, m_DeliveryRadius);
        }
    }
}
