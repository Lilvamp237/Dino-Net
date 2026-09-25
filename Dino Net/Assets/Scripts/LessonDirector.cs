using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Drops the level's networking decisions into the delivery the child is already doing. The
    /// packet still gets carried from dinosaur to dinosaur; at set points along the route the
    /// dinosaur asks a question, and the route only continues once the safe answer is chosen.
    /// Nothing here duplicates the quest - it gates and narrates the existing one.
    /// </summary>
    public class LessonDirector : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField]
        DinoQuestManager m_Quest;

        [SerializeField]
        LevelManager m_Level;

        [SerializeField]
        DecisionPanel m_Panel;

        [SerializeField]
        LevelHud m_Hud;

        [SerializeField, Tooltip("Floating tag above the packet, so what is being sent is always visible.")]
        PacketLabel m_PacketLabel;

        [Header("Lessons")]
        [SerializeField, Tooltip("This level's decisions. Each one names how many nodes must be connected before it is asked.")]
        List<NetworkLesson> m_Lessons = new List<NetworkLesson>();

        [Header("Messages")]
        [SerializeField, Tooltip("Said once when the run starts, e.g. Level 4's line about the Firefly.")]
        string m_IntroMessage;

        [SerializeField, Tooltip("Said after a decision is answered correctly.")]
        string m_ContinueMessage = "Connection ready! Follow the Firefly.";

        [Header("Pacing")]
        [SerializeField, Tooltip("Breathing room after a dinosaur's celebration before it asks the next question.")]
        float m_AskDelay = 1.8f;

        readonly HashSet<NetworkLesson> m_Answered = new HashSet<NetworkLesson>();
        int m_Connected;
        Coroutine m_AskRoutine;

        /// <summary>How many of this level's decisions have been answered correctly.</summary>
        public int LessonsPassed => m_Answered.Count;

        public int LessonCount => m_Lessons.Count;

        /// <summary>The decision currently being asked, or null.</summary>
        public NetworkLesson Pending { get; private set; }

        void OnEnable()
        {
            if (m_Quest != null)
            {
                m_Quest.QuestStarted += OnQuestStarted;
                m_Quest.NodeConnected += OnNodeConnected;
                m_Quest.QuestCompleted += OnQuestCompleted;
            }

            if (m_Panel != null)
                m_Panel.Passed += OnPassed;
        }

        void OnDisable()
        {
            if (m_Quest != null)
            {
                m_Quest.QuestStarted -= OnQuestStarted;
                m_Quest.NodeConnected -= OnNodeConnected;
                m_Quest.QuestCompleted -= OnQuestCompleted;
            }

            if (m_Panel != null)
                m_Panel.Passed -= OnPassed;
        }

        void OnQuestStarted()
        {
            m_Connected = 0;

            if (!string.IsNullOrEmpty(m_IntroMessage) && m_Quest != null)
                m_Quest.Announce(m_IntroMessage);

            Ask(0f);
        }

        void OnNodeConnected(QuestNode node)
        {
            m_Connected++;

            // Let the dinosaur finish being pleased before it asks anything.
            Ask(m_AskDelay);
        }

        void OnQuestCompleted()
        {
            StopAsking();
            Pending = null;

            if (m_Panel != null)
                m_Panel.Hide();

            Gate(false);
        }

        void Ask(float delay)
        {
            var lesson = LessonFor(m_Connected);
            if (lesson == null)
                return;

            StopAsking();

            // The gate goes up straight away, so a child who runs on ahead cannot skip the
            // question by reaching the next node during the pause.
            Pending = lesson;
            Gate(true);
            m_AskRoutine = StartCoroutine(AskRoutine(lesson, delay));
        }

        IEnumerator AskRoutine(NetworkLesson lesson, float delay)
        {
            if (delay > 0f)
                yield return new WaitForSeconds(delay);

            if (m_Panel != null)
                m_Panel.Show(lesson);

            m_AskRoutine = null;
        }

        void StopAsking()
        {
            if (m_AskRoutine == null)
                return;

            StopCoroutine(m_AskRoutine);
            m_AskRoutine = null;
        }

        /// <summary>The unanswered decision due at this point on the route, if any.</summary>
        NetworkLesson LessonFor(int connected)
        {
            foreach (var lesson in m_Lessons)
            {
                if (lesson != null && lesson.triggerAfterNodes == connected && !m_Answered.Contains(lesson))
                    return lesson;
            }

            return null;
        }

        void OnPassed(NetworkLesson lesson)
        {
            m_Answered.Add(lesson);
            Pending = null;

            if (m_Hud != null && !string.IsNullOrEmpty(lesson.conceptResult))
                m_Hud.SetConcept(lesson.conceptResult);

            if (m_PacketLabel != null)
                m_PacketLabel.Set(lesson.packetLabel, lesson.packetTint);

            LearningProgressTracker.Instance?.RecordConceptLearned(lesson.conceptTerm);

            // A level can ask two questions back to back at the same point on the route.
            var next = LessonFor(m_Connected);
            if (next != null)
            {
                Pending = next;
                m_AskRoutine = StartCoroutine(AskRoutine(next, 0.6f));
                return;
            }

            if (m_Quest != null && !string.IsNullOrEmpty(m_ContinueMessage))
                m_Quest.Announce(m_ContinueMessage);

            Gate(false);
        }

        /// <summary>Holds the delivery and the countdown while a question is on screen.</summary>
        void Gate(bool closed)
        {
            if (m_Quest != null)
                m_Quest.DeliveryPaused = closed;

            if (m_Level != null)
                m_Level.SetPaused(closed);
        }
    }
}
