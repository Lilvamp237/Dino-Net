using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Plays the jungle bed as an occasional swell instead of a loop. The recording is only a few
    /// seconds long, so looping it puts the same bird call in the child's ears over and over; this
    /// fades it in now and then and leaves long stretches of quiet in between.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AmbienceCycler : MonoBehaviour
    {
        [SerializeField, Tooltip("Quiet stretch between swells, in seconds. 150-240 is roughly every three minutes.")]
        Vector2 m_SilenceSeconds = new Vector2(150f, 240f);

        [SerializeField, Tooltip("How long one swell lasts before it fades away again.")]
        Vector2 m_PlaySeconds = new Vector2(12f, 20f);

        [SerializeField, Tooltip("Volume at the height of a swell. Kept low so it sits behind the game.")]
        float m_Volume = 0.16f;

        [SerializeField]
        float m_FadeSeconds = 3f;

        [SerializeField, Tooltip("Quiet at the start of a level, so the first thing a child hears is the game.")]
        float m_InitialDelay = 45f;

        AudioSource m_Source;
        float m_Timer;
        float m_Window;
        bool m_Playing;

        void Awake()
        {
            m_Source = GetComponent<AudioSource>();
            m_Source.loop = true;          // the swell itself may outlast the short recording
            m_Source.playOnAwake = false;
            m_Source.volume = 0f;
            m_Source.Stop();

            m_Playing = false;
            m_Window = m_InitialDelay;
        }

        void Update()
        {
            m_Timer += Time.deltaTime;

            var target = 0f;
            if (m_Playing)
            {
                // Fade up at the start of the swell and back down at the end of it.
                var remaining = m_Window - m_Timer;
                target = m_Volume * Mathf.Clamp01(Mathf.Min(m_Timer, remaining) / m_FadeSeconds);
            }

            m_Source.volume = Mathf.MoveTowards(m_Source.volume, target, m_Volume / m_FadeSeconds * Time.deltaTime);

            if (m_Timer < m_Window)
                return;

            m_Timer = 0f;
            m_Playing = !m_Playing;

            if (m_Playing)
            {
                m_Window = Random.Range(m_PlaySeconds.x, m_PlaySeconds.y);
                m_Source.Play();
            }
            else
            {
                m_Window = Random.Range(m_SilenceSeconds.x, m_SilenceSeconds.y);
                m_Source.Stop();
            }
        }
    }
}
