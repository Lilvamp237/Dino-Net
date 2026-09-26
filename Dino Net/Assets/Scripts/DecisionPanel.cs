using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DinoNet
{
    /// <summary>
    /// The two-choice question a dinosaur asks partway through a delivery. Big icons and colour
    /// do the teaching; the words underneath only reinforce them. A wrong pick never fails the
    /// level - it explains the difference and lets the child choose again.
    /// </summary>
    public class DecisionPanel : MonoBehaviour
    {
        [Serializable]
        public class OptionView
        {
            public Button button;
            public Image frame;
            public Image icon;
            public TMP_Text label;
            public TMP_Text sublabel;
        }

        [Header("Panel")]
        [SerializeField, Tooltip("The canvas child that is shown and hidden. This component stays active on the anchor.")]
        GameObject m_Body;

        [SerializeField]
        TMP_Text m_ConceptTag;

        [SerializeField]
        TMP_Text m_Speaker;

        [SerializeField]
        TMP_Text m_Prompt;

        [SerializeField]
        TMP_Text m_Feedback;

        [SerializeField]
        OptionView[] m_Options = new OptionView[2];

        [SerializeField, Tooltip("Hidden while a question is up. The HUD sits closer to the eye than this panel and would otherwise draw straight through it.")]
        GameObject m_HudRoot;

        [SerializeField, Tooltip("Appears while a message is being read out. Tapping it moves straight on for a child who has already understood.")]
        GameObject m_SkipButton;

        [Header("Pacing")]
        [SerializeField, Tooltip("How long the well-done message stays up before the route continues.")]
        float m_CorrectHold = 2.6f;

        [SerializeField, Tooltip("How long the explanation stays before the buttons come back.")]
        float m_RetryDelay = 1.8f;

        [SerializeField, Tooltip("After this many wrong picks the safe choice starts glowing, so nobody can get stuck.")]
        int m_NudgeAfterMistakes = 2;

        [SerializeField, Tooltip("Breathing room after the voice finishes reading the feedback.")]
        float m_PauseAfterSpeech = 0.7f;

        [SerializeField, Tooltip("How long a message must have been up before it can be skipped, so a quick second tap cannot blow straight past it.")]
        float m_SkipAfterSeconds = 0.8f;

        [Header("Audio")]
        [SerializeField]
        AudioSource m_AudioSource;

        [SerializeField]
        AudioClip m_CorrectClip;

        [SerializeField]
        AudioClip m_WrongClip;

        static readonly Color k_Idle = new Color(0.16f, 0.22f, 0.32f, 0.72f);
        static readonly Color k_Good = new Color(0.3f, 1f, 0.5f, 0.85f);
        static readonly Color k_Warn = new Color(1f, 0.68f, 0.2f, 0.8f);

        NetworkLesson m_Lesson;
        int m_Mistakes;
        int m_Chosen = -1;
        bool m_Settled;
        float m_Spoken;
        bool m_Skipped;
        bool m_Suspended;
        Coroutine m_Routine;

        /// <summary>Raised once the child has made the safe choice and the route may continue.</summary>
        public event Action<NetworkLesson> Passed;

        /// <summary>Raised on every wrong pick, so the level can log it without punishing anyone.</summary>
        public event Action<NetworkLesson, LessonOption> Mistaken;

        public bool IsOpen => m_Body != null && m_Body.activeSelf;

        /// <summary>The question currently on screen, or null.</summary>
        public NetworkLesson CurrentLesson => IsOpen ? m_Lesson : null;

        /// <summary>How many wrong picks before the safe choice starts glowing. Lower = more help.</summary>
        public void SetNudgeThreshold(int mistakes) => m_NudgeAfterMistakes = Mathf.Max(1, mistakes);

        void Awake()
        {
            if (m_Body != null)
                m_Body.SetActive(false);

            for (var i = 0; i < m_Options.Length; i++)
            {
                var index = i;
                if (m_Options[i] != null && m_Options[i].button != null)
                    m_Options[i].button.onClick.AddListener(() => Choose(index));
            }
        }

        void Update()
        {
            if (!IsOpen)
                return;

            // Follow the player if they wander off, otherwise the question is unanswerable.
            if (PanelAnchor.HasDrifted(gameObject))
                PanelAnchor.PlaceInFront(gameObject);

            // The safe option breathes once the child has had a couple of tries.
            if (!m_Settled && m_Mistakes >= m_NudgeAfterMistakes)
            {
                var pulse = 0.45f + Mathf.Abs(Mathf.Sin(Time.time * 2.4f)) * 0.5f;
                for (var i = 0; i < m_Options.Length; i++)
                {
                    if (Correct(i) && m_Options[i].frame != null)
                        m_Options[i].frame.color = new Color(k_Good.r, k_Good.g, k_Good.b, pulse);
                }
            }

            if (m_Settled && m_Chosen >= 0 && m_Options[m_Chosen].icon != null)
            {
                var scale = 1f + Mathf.Sin(Time.time * 6f) * 0.06f;
                m_Options[m_Chosen].icon.transform.localScale = Vector3.one * scale;
            }
        }

        public void Show(NetworkLesson lesson)
        {
            if (lesson == null)
                return;

            m_Lesson = lesson;
            m_Mistakes = 0;
            m_Chosen = -1;
            m_Settled = false;

            if (m_ConceptTag != null)
                m_ConceptTag.text = lesson.conceptTerm;

            if (m_Speaker != null)
                m_Speaker.text = lesson.speaker;

            if (m_Prompt != null)
                m_Prompt.text = lesson.prompt;

            if (m_Feedback != null)
                m_Feedback.text = string.Empty;

            for (var i = 0; i < m_Options.Length; i++)
            {
                var view = m_Options[i];
                if (view == null)
                    continue;

                var has = lesson.options != null && i < lesson.options.Length && lesson.options[i] != null;
                if (view.button != null)
                {
                    view.button.gameObject.SetActive(has);
                    view.button.interactable = has;
                }

                if (!has)
                    continue;

                var option = lesson.options[i];
                if (view.icon != null)
                {
                    view.icon.sprite = option.icon;
                    view.icon.color = option.tint;
                    view.icon.transform.localScale = Vector3.one;
                }

                if (view.label != null)
                {
                    view.label.text = option.label;
                    view.label.color = option.tint;
                }

                if (view.sublabel != null)
                    view.sublabel.text = option.sublabel;

                if (view.frame != null)
                    view.frame.color = k_Idle;
            }

            if (m_Body != null)
                m_Body.SetActive(true);

            if (m_HudRoot != null)
                m_HudRoot.SetActive(false);

            if (m_SkipButton != null)
                m_SkipButton.SetActive(false);

            PanelAnchor.PlaceInFront(gameObject);
            VoiceOver.Speak(lesson.speaker + " " + lesson.prompt);
            m_Spoken = 0f;
        }

        public void Hide()
        {
            // Only give the HUD back if this panel was the one that took it away. Hiding an
            // already-closed panel at the end of a level would otherwise pop the HUD back up in
            // front of the completion screen.
            var wasOpen = IsOpen;

            if (m_Body != null)
                m_Body.SetActive(false);

            if (m_SkipButton != null)
                m_SkipButton.SetActive(false);

            if (wasOpen && m_HudRoot != null)
                m_HudRoot.SetActive(true);
        }

        /// <summary>
        /// Puts the question out of sight while something else takes over the view - the pause
        /// menu - without answering it or handing the HUD back. Calling it with false brings the
        /// same question back exactly as it was.
        /// </summary>
        public void SetSuspended(bool suspended)
        {
            if (m_Body == null)
                return;

            if (suspended)
            {
                if (!m_Body.activeSelf)
                    return;

                m_Suspended = true;
                m_Body.SetActive(false);

                if (m_SkipButton != null)
                    m_SkipButton.SetActive(false);
            }
            else if (m_Suspended)
            {
                m_Suspended = false;
                m_Body.SetActive(true);
                PanelAnchor.PlaceInFront(gameObject);
            }
        }

        /// <summary>
        /// Wired to the skip button: ends the current message early for a child who has already
        /// got the point, instead of making them sit through the whole recording.
        /// </summary>
        public void Skip()
        {
            m_Skipped = true;
            VoiceOver.Stop();
        }

        /// <summary>Wired to each option button. Public so the level tests can drive it directly.</summary>
        public void Choose(int index)
        {
            if (m_Lesson == null || m_Settled || !IsOpen)
                return;

            if (m_Lesson.options == null || index < 0 || index >= m_Lesson.options.Length)
                return;

            var option = m_Lesson.options[index];
            if (option == null)
                return;

            m_Chosen = index;
            SetInteractable(false);

            if (option.correct)
            {
                m_Settled = true;
                Paint(index, k_Good);
                Dim(index);

                if (m_Feedback != null)
                {
                    m_Feedback.color = new Color(0.55f, 1f, 0.68f);
                    m_Feedback.text = m_Lesson.correctFeedback;
                }

                Play(m_CorrectClip);
                m_Spoken = VoiceOver.Speak(m_Lesson.correctFeedback);
                Restart(FinishRoutine());
                return;
            }

            m_Mistakes++;
            Paint(index, k_Warn);

            if (m_Feedback != null)
            {
                m_Feedback.color = new Color(1f, 0.85f, 0.5f);
                m_Feedback.text = option.wrongFeedback;
            }

            Play(m_WrongClip);
            m_Spoken = VoiceOver.Speak(option.wrongFeedback);
            Mistaken?.Invoke(m_Lesson, option);
            Restart(RetryRoutine());
        }

        IEnumerator FinishRoutine()
        {
            yield return Hold(m_CorrectHold);
            Hide();
            Passed?.Invoke(m_Lesson);
        }

        IEnumerator RetryRoutine()
        {
            yield return Hold(m_RetryDelay);

            m_Chosen = -1;
            for (var i = 0; i < m_Options.Length; i++)
            {
                if (m_Options[i] != null && m_Options[i].frame != null)
                    m_Options[i].frame.color = k_Idle;
            }

            if (m_Feedback != null && m_Mistakes >= m_NudgeAfterMistakes)
            {
                m_Feedback.color = new Color(0.8f, 1f, 0.9f);
                m_Feedback.text = "Try the glowing one!";
            }

            SetInteractable(true);
        }

        /// <summary>
        /// Waits at least <paramref name="seconds"/>, and longer if the voice is still reading the
        /// message out, so feedback is never cut off part-way through - unless the child taps the
        /// skip button, which ends the wait straight away.
        /// </summary>
        IEnumerator Hold(float seconds)
        {
            m_Skipped = false;
            var target = Mathf.Max(seconds, m_Spoken + m_PauseAfterSpeech);
            var elapsed = 0f;
            var skipShown = false;

            while (!m_Skipped && (elapsed < target || VoiceOver.IsSpeaking))
            {
                elapsed += Time.deltaTime;

                if (!skipShown && elapsed >= m_SkipAfterSeconds && m_SkipButton != null)
                {
                    skipShown = true;
                    m_SkipButton.SetActive(true);
                }

                yield return null;
            }

            if (m_SkipButton != null)
                m_SkipButton.SetActive(false);
        }

        void Restart(IEnumerator routine)
        {
            if (m_Routine != null)
                StopCoroutine(m_Routine);

            m_Routine = StartCoroutine(routine);
        }

        bool Correct(int index) =>
            m_Lesson != null && m_Lesson.options != null && index < m_Lesson.options.Length
            && m_Lesson.options[index] != null && m_Lesson.options[index].correct;

        void SetInteractable(bool value)
        {
            foreach (var view in m_Options)
            {
                if (view != null && view.button != null && view.button.gameObject.activeSelf)
                    view.button.interactable = value;
            }
        }

        void Paint(int index, Color colour)
        {
            if (index >= 0 && index < m_Options.Length && m_Options[index] != null && m_Options[index].frame != null)
                m_Options[index].frame.color = colour;
        }

        /// <summary>Fades the option that wasn't taken, so the chosen one clearly won.</summary>
        void Dim(int keep)
        {
            for (var i = 0; i < m_Options.Length; i++)
            {
                if (i == keep || m_Options[i] == null)
                    continue;

                if (m_Options[i].icon != null)
                {
                    var c = m_Options[i].icon.color;
                    m_Options[i].icon.color = new Color(c.r, c.g, c.b, 0.3f);
                }

                if (m_Options[i].label != null)
                {
                    var c = m_Options[i].label.color;
                    m_Options[i].label.color = new Color(c.r, c.g, c.b, 0.35f);
                }
            }
        }

        void Play(AudioClip clip)
        {
            if (m_AudioSource != null && clip != null)
                m_AudioSource.PlayOneShot(clip);
        }
    }
}
