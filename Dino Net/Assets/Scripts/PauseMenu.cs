using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// A small pause button that rides in the low corner of the child's view, and the menu it
    /// opens. It follows the head's yaw only, so it stays in the same corner as they look around
    /// but does not swing up and down when they look at their feet. Pausing stops the clock, the
    /// dinosaurs and the countdown, and gives them a way out of a level back to the main menu.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField]
        LevelManager m_Level;

        [SerializeField, Tooltip("The little button that follows the corner of the view.")]
        GameObject m_Button;

        [SerializeField, Tooltip("The menu shown while the game is paused.")]
        GameObject m_Panel;

        [SerializeField, Tooltip("Hidden while the menu is up, because the HUD sits closer to the eye and would draw through it.")]
        GameObject m_HudRoot;

        [Header("Placement")]
        [SerializeField, Tooltip("Right, up and forward from the head. Low enough to stay out of the way of the game, but high enough to be properly in view rather than at the edge of vision.")]
        Vector3 m_Offset = new Vector3(0.46f, -0.42f, 1.0f);

        [SerializeField, Tooltip("Puts the button in the left corner instead of the right.")]
        bool m_LeftHanded;

        [SerializeField, Tooltip("How softly it follows the head. Higher is lazier.")]
        float m_Smoothing = 0.12f;

        Transform m_Head;
        Vector3 m_Velocity;
        bool m_HudWasVisible;
        DecisionPanel m_Question;

        public bool IsPaused { get; private set; }

        void Start()
        {
            if (m_Panel != null)
                m_Panel.SetActive(false);

            // A question may be on screen when the child pauses; the two panels would otherwise
            // sit on top of each other.
            m_Question = FindFirstObjectByType<DecisionPanel>();

            // Start it exactly where it belongs rather than letting it fly in from the origin.
            if (m_Button != null && Head() != null)
                m_Button.transform.position = TargetPosition();
        }

        void LateUpdate()
        {
            var head = Head();
            if (head == null || m_Button == null)
                return;

            // Nothing to pause once the level is over - the result panel owns the view by then.
            var offerPause = !IsPaused
                && (m_Level == null || (m_Level.State != LevelManager.LevelState.Complete
                                        && m_Level.State != LevelManager.LevelState.Failed));

            if (m_Button.activeSelf != offerPause)
                m_Button.SetActive(offerPause);

            if (!offerPause)
                return;

            // Unscaled, so the button keeps tracking the head while the game is frozen.
            m_Button.transform.position = Vector3.SmoothDamp(m_Button.transform.position, TargetPosition(),
                ref m_Velocity, m_Smoothing, Mathf.Infinity, Time.unscaledDeltaTime);

            var away = m_Button.transform.position - head.position;
            if (away.sqrMagnitude > 0.0001f)
                m_Button.transform.rotation = Quaternion.LookRotation(away.normalized, Vector3.up);
        }

        Vector3 TargetPosition()
        {
            var head = Head();
            if (head == null)
                return transform.position;

            // Yaw only: the corner it sits in should not change when the child looks up or down.
            var yaw = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            var offset = m_Offset;
            if (m_LeftHanded)
                offset.x = -offset.x;

            return head.position + yaw * offset;
        }

        Transform Head()
        {
            if (m_Head == null && Camera.main != null)
                m_Head = Camera.main.transform;

            return m_Head;
        }

        // ---- Buttons ----

        /// <summary>Freezes the level and opens the menu.</summary>
        public void Pause()
        {
            if (IsPaused)
                return;

            IsPaused = true;
            VoiceOver.Stop();

            if (m_Level != null)
                m_Level.SetPaused(true);

            // A real freeze: the countdown, the dinosaurs and walking all stop.
            Time.timeScale = 0f;

            if (m_Button != null)
                m_Button.SetActive(false);

            m_HudWasVisible = m_HudRoot != null && m_HudRoot.activeSelf;
            if (m_HudWasVisible)
                m_HudRoot.SetActive(false);

            if (m_Question != null)
                m_Question.SetSuspended(true);

            if (m_Panel != null)
            {
                PanelAnchor.PlaceInFront(m_Panel);
                m_Panel.SetActive(true);
            }
        }

        /// <summary>Closes the menu and starts the level running again.</summary>
        public void Resume()
        {
            if (!IsPaused)
                return;

            IsPaused = false;
            Time.timeScale = 1f;

            if (m_Level != null)
                m_Level.SetPaused(false);

            if (m_Panel != null)
                m_Panel.SetActive(false);

            if (m_Question != null)
                m_Question.SetSuspended(false);

            // Only give the HUD back if this menu was the thing that took it away; a question may
            // have hidden it first.
            if (m_HudWasVisible && m_HudRoot != null)
                m_HudRoot.SetActive(true);

            if (m_Button != null)
            {
                m_Button.transform.position = TargetPosition();
                m_Button.SetActive(true);
            }
        }

        public void ReturnToMainMenu()
        {
            // GameFlow restores the time scale as it loads.
            IsPaused = false;
            GameFlow.LoadMainMenu();
        }

        void OnDisable()
        {
            // Never leave the game frozen because a scene was torn down mid-pause.
            if (IsPaused)
                Time.timeScale = 1f;
        }
    }
}
