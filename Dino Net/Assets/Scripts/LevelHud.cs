using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DinoNet
{
    /// <summary>
    /// The in-headset heads-up display: node progress in one corner, countdown in the other,
    /// and the end-of-level panels. Purely a view - <see cref="LevelManager"/> drives it.
    /// </summary>
    public class LevelHud : MonoBehaviour
    {
        [Header("Progress (one corner)")]
        [SerializeField]
        Image m_ProgressFill;

        [SerializeField]
        TMP_Text m_ProgressLabel;

        [Header("Timer (opposite corner)")]
        [SerializeField]
        GameObject m_TimerRoot;

        [SerializeField]
        TMP_Text m_TimerLabel;

        [SerializeField, Tooltip("The countdown turns this colour when time is nearly up.")]
        Color m_TimerUrgentColor = new Color(1f, 0.45f, 0.35f);

        [SerializeField]
        float m_UrgentSeconds = 30f;

        [Header("End of level")]
        [SerializeField]
        GameObject m_CompletePanel;

        [SerializeField]
        GameObject m_FailedPanel;

        [SerializeField]
        TMP_Text m_FailedReason;

        [SerializeField, Tooltip("Hidden on the final level, which has no next level to go to.")]
        GameObject m_NextLevelButton;

        [SerializeField]
        ParticleSystem m_CelebrationVfx;

        [SerializeField]
        AudioSource m_AudioSource;

        [SerializeField]
        AudioClip m_CompleteClip;

        [SerializeField]
        AudioClip m_FailedClip;

        Color m_TimerNormalColor = Color.white;

        void Awake()
        {
            if (m_TimerLabel != null)
                m_TimerNormalColor = m_TimerLabel.color;

            if (m_CompletePanel != null)
                m_CompletePanel.SetActive(false);

            if (m_FailedPanel != null)
                m_FailedPanel.SetActive(false);
        }

        public void SetProgress(float normalised, int connected, int total)
        {
            if (m_ProgressFill != null)
                m_ProgressFill.fillAmount = Mathf.Clamp01(normalised);

            if (m_ProgressLabel != null)
                m_ProgressLabel.text = connected + " / " + total + " nodes";
        }

        public void SetTime(float secondsRemaining)
        {
            if (m_TimerLabel == null)
                return;

            var clamped = Mathf.Max(0f, secondsRemaining);
            var minutes = Mathf.FloorToInt(clamped / 60f);
            var seconds = Mathf.FloorToInt(clamped % 60f);
            m_TimerLabel.text = string.Format("{0}:{1:00}", minutes, seconds);
            m_TimerLabel.color = clamped <= m_UrgentSeconds ? m_TimerUrgentColor : m_TimerNormalColor;
        }

        public void SetTimerVisible(bool visible)
        {
            if (m_TimerRoot != null)
                m_TimerRoot.SetActive(visible);
        }

        public void ShowComplete(int levelNumber)
        {
            if (m_NextLevelButton != null)
                m_NextLevelButton.SetActive(GameFlow.HasLevel(levelNumber + 1));

            if (m_CompletePanel != null)
            {
                Anchor(m_CompletePanel);
                m_CompletePanel.SetActive(true);
            }

            if (m_CelebrationVfx != null)
                m_CelebrationVfx.Play();

            Play(m_CompleteClip);
        }

        public void ShowFailed(string reason)
        {
            if (m_FailedReason != null)
                m_FailedReason.text = reason;

            if (m_FailedPanel != null)
            {
                Anchor(m_FailedPanel);
                m_FailedPanel.SetActive(true);
            }

            Play(m_FailedClip);
        }

        /// <summary>
        /// Drops the panel into world space just in front of the player and leaves it there, so
        /// it stays still to point at instead of drifting with every head movement.
        /// </summary>
        static void Anchor(GameObject panel)
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            panel.transform.SetParent(null, true);
            panel.transform.position = cam.transform.position + forward * 2.2f + Vector3.up * -0.1f;
            panel.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        void Play(AudioClip clip)
        {
            if (m_AudioSource != null && clip != null)
                m_AudioSource.PlayOneShot(clip);
        }
    }
}
