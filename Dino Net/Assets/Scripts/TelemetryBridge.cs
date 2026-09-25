using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Created by <see cref="SessionTelemetry"/> whenever a scene loads. It only listens to the
    /// level's existing systems (quest, level manager, decision panel, hazards) and reports what
    /// happened; it never changes how the level plays.
    /// </summary>
    public class TelemetryBridge : MonoBehaviour
    {
        public static TelemetryBridge Current { get; private set; }

        DinoQuestManager m_Quest;
        LevelManager m_Level;
        DecisionPanel m_Panel;
        RoadNetwork m_Roads;
        CarryableOrb m_Orb;
        DangerZone[] m_Zones = new DangerZone[0];

        readonly Dictionary<NetworkLesson, int> m_Mistakes = new Dictionary<NetworkLesson, int>();
        readonly List<float> m_LegActual = new List<float>();
        readonly List<float> m_LegOptimal = new List<float>();

        bool m_Running;
        bool m_Finished;
        float m_Elapsed;
        float m_LastNodeAt;
        int m_MistakeCount;
        bool m_InDanger;
        float m_DangerSince;
        Vector3 m_LegStart;
        Vector3 m_LastOrbPosition;
        bool m_HaveLastOrb;
        float m_LegWalked;

        public int LevelNumber { get; private set; }

        /// <summary>Wrong nodes plus wrong answers so far this run.</summary>
        public int MistakeCount => m_MistakeCount;

        public float ElapsedSeconds => m_Elapsed;

        /// <summary>Raised once a level's completion has been scored and saved.</summary>
        public event System.Action<LevelResult> LevelScored;

        public struct LevelResult
        {
            public int level;
            public int stars;
            public int bestStars;
            public float seconds;
            public float timeLeftFraction;
            public int mistakes;
            public float efficiency;
            public bool newBest;
        }

        void Awake()
        {
            Current = this;
        }

        void Start()
        {
            m_Quest = FindFirstObjectByType<DinoQuestManager>();
            m_Level = FindFirstObjectByType<LevelManager>();
            if (m_Quest == null || m_Level == null)
            {
                Destroy(gameObject);
                return;
            }

            LevelNumber = m_Level.LevelNumber;
            m_Panel = FindFirstObjectByType<DecisionPanel>(FindObjectsInactive.Include);
            m_Roads = FindFirstObjectByType<RoadNetwork>();
            m_Orb = m_Quest.Orb;
            m_Zones = FindObjectsByType<DangerZone>(FindObjectsSortMode.None);

            m_Quest.QuestStarted += OnQuestStarted;
            m_Quest.NodeConnected += OnNodeConnected;
            m_Quest.QuestCompleted += OnQuestCompleted;
            m_Quest.WrongNodeVisited += OnWrongNode;
            m_Level.LevelFailed += OnLevelFailed;

            if (m_Panel != null)
            {
                m_Panel.Passed += OnLessonPassed;
                m_Panel.Mistaken += OnLessonMistaken;
            }
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;

            if (m_Quest != null)
            {
                m_Quest.QuestStarted -= OnQuestStarted;
                m_Quest.NodeConnected -= OnNodeConnected;
                m_Quest.QuestCompleted -= OnQuestCompleted;
                m_Quest.WrongNodeVisited -= OnWrongNode;
            }

            if (m_Level != null)
                m_Level.LevelFailed -= OnLevelFailed;

            if (m_Panel != null)
            {
                m_Panel.Passed -= OnLessonPassed;
                m_Panel.Mistaken -= OnLessonMistaken;
            }

            if (m_InDanger)
                CloseDanger();
        }

        void Update()
        {
            if (!m_Running || m_Finished)
                return;

            if (!m_Level.IsPaused && !m_Quest.DeliveryPaused)
                m_Elapsed += Time.deltaTime;

            TrackWalking();
            TrackDanger();
        }

        // ---------------------------------------------------------------- run events

        void OnQuestStarted()
        {
            if (m_Running)
                return;

            m_Running = true;
            m_LastNodeAt = 0f;
            if (SessionTelemetry.Instance != null)
                SessionTelemetry.Instance.CurrentLevel = LevelNumber;

            m_LegStart = m_Orb != null ? m_Orb.transform.position : Vector3.zero;
            m_HaveLastOrb = false;

            if (LevelNumber > 0)
                ProgressStore.RecordLevelStart(LevelNumber);

            SessionTelemetry.Log("level_start", LevelNumber, "route", m_Quest.RouteCount, "timeLimit", (int)m_Level.TimeLimit, "recommended", ProgressStore.RecommendedLevel());
        }

        void OnNodeConnected(QuestNode node)
        {
            var seconds = m_Elapsed - m_LastNodeAt;
            m_LastNodeAt = m_Elapsed;

            var end = node.DeliveryAnchor.position;
            var optimal = OptimalDistance(m_LegStart, end);
            if (m_LegWalked > 1f && optimal > 0.5f)
            {
                m_LegActual.Add(m_LegWalked);
                m_LegOptimal.Add(optimal);
            }

            SessionTelemetry.Log("node_connected", LevelNumber,
                "node", node.FriendlyName, "index", m_Quest.ConnectedCount - 1, "seconds", seconds);

            m_LegStart = end;
            m_LegWalked = 0f;
            m_HaveLastOrb = false;
        }

        void OnWrongNode(QuestNode node)
        {
            m_MistakeCount++;
            SessionTelemetry.Log("wrong_node", LevelNumber, "node", node.FriendlyName);
        }

        void OnQuestCompleted()
        {
            if (m_Finished)
                return;

            m_Finished = true;
            if (m_InDanger)
                CloseDanger();

            var limit = Mathf.Max(1f, m_Level.TimeLimit);
            var fraction = Mathf.Clamp01(m_Level.TimeRemaining / limit);
            var efficiency = Efficiency();
            var result = new LevelResult
            {
                level = LevelNumber,
                seconds = m_Elapsed,
                timeLeftFraction = fraction,
                mistakes = m_MistakeCount,
                efficiency = efficiency,
            };

            if (LevelNumber > 0)
            {
                var previousBest = ProgressStore.BestStars(LevelNumber);
                result.stars = ProgressStore.StarsFor(fraction, m_MistakeCount);
                result.bestStars = ProgressStore.RecordCompletion(LevelNumber, m_Elapsed, result.stars);
                result.newBest = result.stars > previousBest;

                ProgressStore.TryAward("first_delivery");
                if (fraction >= 0.66f)
                    ProgressStore.TryAward("speedy_router");
                if (ProgressStore.LevelsCompleted >= GameFlow.LastLevel)
                    ProgressStore.TryAward("big_brain");
            }

            SessionTelemetry.Log("level_complete", LevelNumber,
                "stars", result.stars, "seconds", m_Elapsed, "mistakes", m_MistakeCount,
                "timeLeft", m_Level.TimeRemaining, "efficiency", efficiency < 0f ? null : (object)efficiency);

            LevelScored?.Invoke(result);
        }

        void OnLevelFailed()
        {
            if (m_Finished)
                return;

            m_Finished = true;
            if (m_InDanger)
                CloseDanger();

            if (LevelNumber > 0)
                ProgressStore.RecordLevelFailed(LevelNumber);

            SessionTelemetry.Log("level_fail", LevelNumber,
                "reason", m_Level.TimeRemaining <= 0.01f ? "timeout" : "other", "seconds", m_Elapsed, "connected", m_Quest.ConnectedCount);
        }

        // ---------------------------------------------------------------- lessons

        void OnLessonMistaken(NetworkLesson lesson, LessonOption option)
        {
            m_Mistakes.TryGetValue(lesson, out var count);
            count++;
            m_Mistakes[lesson] = count;
            m_MistakeCount++;

            ProgressStore.RecordAnswer(lesson.conceptTerm, false, count);
            SessionTelemetry.Log("lesson_answer", LevelNumber, "concept", lesson.conceptTerm, "correct", false, "attempt", count);
            if (count == 2)
                SessionTelemetry.Log("hint_used", LevelNumber, "concept", lesson.conceptTerm);
        }

        void OnLessonPassed(NetworkLesson lesson)
        {
            m_Mistakes.TryGetValue(lesson, out var wrong);
            var attempt = wrong + 1;

            ProgressStore.RecordAnswer(lesson.conceptTerm, true, attempt);
            SessionTelemetry.Log("lesson_answer", LevelNumber, "concept", lesson.conceptTerm, "correct", true, "attempt", attempt);

            if (attempt == 1)
            {
                if (lesson.conceptTerm == ConceptCatalog.Https)
                    ProgressStore.TryAward("safe_surfer");
                if (lesson.conceptTerm == ConceptCatalog.Passwords || lesson.conceptTerm == ConceptCatalog.Strong)
                    ProgressStore.TryAward("password_guardian");
            }

            if (lesson.conceptTerm == ConceptCatalog.Backup)
                ProgressStore.TryAward("backup_hero");
        }

        // ---------------------------------------------------------------- danger & walking

        void TrackDanger()
        {
            var inside = false;
            foreach (var zone in m_Zones)
            {
                if (zone != null && zone.PlayerInside)
                {
                    inside = true;
                    break;
                }
            }

            if (inside && !m_InDanger)
            {
                m_InDanger = true;
                m_DangerSince = m_Elapsed;
                SessionTelemetry.Log("danger_enter", LevelNumber, "zone", "volcano");
            }
            else if (!inside && m_InDanger)
            {
                CloseDanger();
            }
        }

        void CloseDanger()
        {
            m_InDanger = false;
            SessionTelemetry.Log("danger_exit", LevelNumber, "zone", "volcano", "seconds", m_Elapsed - m_DangerSince);
        }

        /// <summary>Adds up how far the child actually walks with the packet, for the route-efficiency score.</summary>
        void TrackWalking()
        {
            if (m_Orb == null || !m_Orb.IsHeld)
            {
                m_HaveLastOrb = false;
                return;
            }

            var p = m_Orb.transform.position;
            p.y = 0f;
            if (m_HaveLastOrb)
                m_LegWalked += Vector3.Distance(p, m_LastOrbPosition);

            m_LastOrbPosition = p;
            m_HaveLastOrb = true;
        }

        float OptimalDistance(Vector3 from, Vector3 to)
        {
            var straight = new Vector2(to.x - from.x, to.z - from.z).magnitude;
            if (m_Roads == null)
                return straight;

            var points = new List<Vector3>();
            var a = m_Roads.NearestJunction(from);
            var b = m_Roads.NearestJunction(to);
            if (m_Roads.TryGetPath(a, b, points) && points.Count > 1)
                return Mathf.Min(RoadNetwork.PathLength(points), Mathf.Max(straight, 0.5f) * 1.6f);

            return straight;
        }

        /// <summary>1 = walked no further than needed; lower = took the long way. -1 if nothing was measured.</summary>
        float Efficiency()
        {
            var optimal = 0f;
            var actual = 0f;
            for (var i = 0; i < m_LegActual.Count; i++)
            {
                optimal += m_LegOptimal[i];
                actual += m_LegActual[i];
            }

            if (actual < 1f || optimal < 0.5f)
                return -1f;

            return Mathf.Clamp01(optimal / actual);
        }
    }
}
