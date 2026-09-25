using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// The adaptive part of the game. It looks at how this child has done on each networking idea
    /// (saved in <see cref="ProgressStore"/>) and quietly tunes the level: more help and a little
    /// extra time when something has been hard, a firmer challenge when they are flying.
    /// It also reminds them of an idea they found tricky. Every adjustment is logged so the
    /// dashboard can show teachers and parents what the coach did.
    /// </summary>
    public class AdaptiveCoach : MonoBehaviour
    {
        LevelManager m_Level;
        DinoQuestManager m_Quest;
        DecisionPanel m_Panel;
        bool m_Applied;

        void Start()
        {
            m_Level = FindFirstObjectByType<LevelManager>();
            m_Quest = FindFirstObjectByType<DinoQuestManager>();
            m_Panel = FindFirstObjectByType<DecisionPanel>(FindObjectsInactive.Include);
            if (m_Level == null || m_Quest == null || m_Level.LevelNumber <= 0)
            {
                enabled = false;
                return;
            }

            m_Quest.QuestStarted += Apply;
        }

        void OnDestroy()
        {
            if (m_Quest != null)
                m_Quest.QuestStarted -= Apply;
        }

        void Apply()
        {
            if (m_Applied)
                return;

            m_Applied = true;
            var level = m_Level.LevelNumber;
            var attempts = ProgressStore.Attempts(level);

            // Attempts was already bumped for this run by the telemetry bridge, so > 1 means a retry.
            var struggling = ProgressStore.FailedLastTime(level) || (attempts > 1 && !ProgressStore.IsCompleted(level));
            var weak = ProgressStore.WeakestConcept();
            var confident = attempts > 1 && ProgressStore.IsCompleted(level) && weak == null;

            if (struggling)
            {
                m_Level.AddTime(60f);
                if (m_Panel != null)
                    m_Panel.SetNudgeThreshold(1);

                RuntimeUi.Toast("Extra help switched on: +1 minute and hints ready", new Color(0.6f, 1f, 0.8f), 4f);
                SessionTelemetry.Log("adaptive", level, "action", "help", "bonusSeconds", 60);
            }
            else if (confident)
            {
                if (m_Panel != null)
                    m_Panel.SetNudgeThreshold(3);

                RuntimeUi.Toast("You know this one - no hints this time. Show me!", new Color(1f, 0.85f, 0.4f), 4f);
                SessionTelemetry.Log("adaptive", level, "action", "challenge");
            }
            else if (weak != null)
            {
                var weakLevel = ConceptCatalog.LevelFor(weak);
                if (weakLevel == level)
                {
                    RuntimeUi.Toast("Let's practise " + ConceptCatalog.Friendly(weak) + " again!", new Color(0.55f, 0.85f, 1f), 4f);
                    if (m_Panel != null)
                        m_Panel.SetNudgeThreshold(1);

                    SessionTelemetry.Log("adaptive", level, "action", "practise", "concept", weak);
                }
                else if (weakLevel > 0 && weakLevel < level)
                {
                    RuntimeUi.Toast("Remember: " + ConceptCatalog.Friendly(weak) + ".", new Color(0.55f, 0.85f, 1f), 3.5f);
                    SessionTelemetry.Log("adaptive", level, "action", "remind", "concept", weak);
                }
            }
        }
    }
}
