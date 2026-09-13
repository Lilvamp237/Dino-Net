using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Warns children away from the volcano. When the player's head crosses into the zone a low
    /// drone fades in and a warning banner appears; both fade back out once they retreat.
    /// Nothing is taken away from the player - this only nudges them back toward the village.
    /// </summary>
    public class DangerZone : MonoBehaviour
    {
        [SerializeField, Tooltip("Centre of the danger area. Defaults to this transform.")]
        Transform m_Center;

        [SerializeField]
        float m_Radius = 8.5f;

        [SerializeField, Tooltip("Extra distance beyond the radius before the warning clears, so it can't flicker on the boundary.")]
        float m_ExitBuffer = 1.5f;

        [SerializeField]
        AudioSource m_WarningAudio;

        [SerializeField]
        GameObject m_WarningBanner;

        [SerializeField, Tooltip("Glowing hazard ring on the ground.")]
        Renderer m_Ring;

        [SerializeField]
        float m_MaxVolume = 0.65f;

        [SerializeField, Tooltip("How quickly the drone fades in and out.")]
        float m_FadeSpeed = 1.5f;

        [SerializeField]
        Color m_RingColor = new Color(1f, 0.35f, 0.15f, 0.35f);

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock m_Block;
        Transform m_Head;
        bool m_Inside;

        public bool PlayerInside => m_Inside;

        Transform Center => m_Center != null ? m_Center : transform;

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();

            if (m_WarningAudio != null)
            {
                m_WarningAudio.loop = true;
                m_WarningAudio.playOnAwake = false;
                m_WarningAudio.volume = 0f;
            }

            if (m_WarningBanner != null)
                m_WarningBanner.SetActive(false);
        }

        void Update()
        {
            if (m_Head == null)
            {
                var cam = Camera.main;
                if (cam == null)
                    return;

                m_Head = cam.transform;
            }

            var head = m_Head.position;
            var centre = Center.position;
            head.y = 0f;
            centre.y = 0f;
            var distance = Vector3.Distance(head, centre);

            if (!m_Inside && distance <= m_Radius)
                SetInside(true);
            else if (m_Inside && distance > m_Radius + m_ExitBuffer)
                SetInside(false);

            if (m_WarningAudio != null)
            {
                var target = m_Inside ? m_MaxVolume : 0f;
                m_WarningAudio.volume = Mathf.MoveTowards(m_WarningAudio.volume, target, m_FadeSpeed * Time.deltaTime);

                if (m_WarningAudio.volume <= 0.001f && m_WarningAudio.isPlaying && !m_Inside)
                    m_WarningAudio.Stop();
            }

            PulseRing();
        }

        void SetInside(bool inside)
        {
            m_Inside = inside;

            if (inside && m_WarningAudio != null && !m_WarningAudio.isPlaying)
                m_WarningAudio.Play();

            if (m_WarningBanner != null)
                m_WarningBanner.SetActive(inside);
        }

        void PulseRing()
        {
            if (m_Ring == null)
                return;

            // Beats faster once the child is actually inside the hazard.
            var speed = m_Inside ? 6f : 2f;
            var intensity = (m_Inside ? 1.6f : 0.8f) + Mathf.Sin(Time.time * speed) * 0.4f;

            m_Ring.GetPropertyBlock(m_Block);
            m_Block.SetColor(k_BaseColor, m_RingColor);
            m_Block.SetColor(k_EmissionColor, (Color)m_RingColor * intensity);
            m_Ring.SetPropertyBlock(m_Block);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.4f);
            Gizmos.DrawWireSphere(Center.position, m_Radius);
        }
    }
}
