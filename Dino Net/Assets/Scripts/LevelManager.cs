using System;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Runs one playable level: counts how many nodes the packet has connected, runs the
    /// countdown, and decides win/lose. Entirely driven by the level's own route length, so the
    /// same component works for every level without per-level logic.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public enum LevelState
        {
            Waiting,
            Running,
            Complete,
            Failed,
        }

        [SerializeField, Tooltip("Level 1-4. Level 0 means the tutorial, which has no timer or win panel.")]
        int m_LevelNumber = 1;

        [SerializeField]
        DinoQuestManager m_Quest;

        [SerializeField]
        LevelHud m_Hud;

        [SerializeField, Tooltip("Countdown length in seconds. 300 = five minutes.")]
        float m_TimeLimit = 300f;

        [SerializeField, Tooltip("Tutorial levels skip the countdown.")]
        bool m_UseTimer = true;

        [SerializeField, Tooltip("Off for the tutorial, which ends with its own panel instead of the generic win/fail ones.")]
        bool m_ShowResultPanels = true;

        [SerializeField, Tooltip("Stops wandering dinosaurs and the packet once the level is over.")]
        bool m_FreezeOnFinish = true;

        [SerializeField, Tooltip("How much faster the clock runs while the child is inside a volcano danger zone.")]
        float m_DangerTimeMultiplier = 3f;

        DangerZone[] m_DangerZones;

        /// <summary>True while the countdown is being drained faster by a nearby volcano.</summary>
        public bool InDanger { get; private set; }

        public event Action LevelCompleted;
        public event Action LevelFailed;

        public LevelState State { get; private set; } = LevelState.Waiting;
        public int LevelNumber => m_LevelNumber;
        public float TimeRemaining { get; private set; }

        /// <summary>Nodes connected so far / total nodes on the route, as 0..1.</summary>
        public float Progress => TotalNodes == 0 ? 0f : ConnectedNodes / (float)TotalNodes;

        public int TotalNodes => m_Quest != null ? m_Quest.RouteCount : 0;
        public int ConnectedNodes => m_Quest != null ? m_Quest.ConnectedCount : 0;

        void Awake()
        {
            TimeRemaining = m_TimeLimit;
            m_DangerZones = FindObjectsByType<DangerZone>(FindObjectsSortMode.None);
        }

        void OnEnable()
        {
            if (m_Quest == null)
                return;

            m_Quest.QuestStarted += OnQuestStarted;
            m_Quest.NodeConnected += OnNodeConnected;
            m_Quest.QuestCompleted += OnQuestCompleted;
        }

        void OnDisable()
        {
            if (m_Quest == null)
                return;

            m_Quest.QuestStarted -= OnQuestStarted;
            m_Quest.NodeConnected -= OnNodeConnected;
            m_Quest.QuestCompleted -= OnQuestCompleted;
        }

        void Start()
        {
            if (m_Hud != null)
            {
                m_Hud.SetTimerVisible(m_UseTimer);
                m_Hud.SetProgress(0f, 0, TotalNodes);
                m_Hud.SetTime(TimeRemaining);
            }
        }

        void Update()
        {
            if (State != LevelState.Running || !m_UseTimer)
                return;

            // Standing near a volcano burns the clock faster, so hazards cost something.
            InDanger = false;
            foreach (var zone in m_DangerZones)
            {
                if (zone != null && zone.PlayerInside)
                {
                    InDanger = true;
                    break;
                }
            }

            TimeRemaining -= Time.deltaTime * (InDanger ? m_DangerTimeMultiplier : 1f);

            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                Fail("Time's up!");
            }

            if (m_Hud != null)
                m_Hud.SetTime(TimeRemaining, InDanger);
        }

        void OnQuestStarted()
        {
            // The countdown only begins once the child actually starts the run.
            if (State == LevelState.Waiting)
                State = LevelState.Running;

            RefreshProgress();
        }

        void OnNodeConnected(QuestNode node) => RefreshProgress();

        void OnQuestCompleted()
        {
            if (State != LevelState.Running)
                return;

            State = LevelState.Complete;
            RefreshProgress();

            if (m_FreezeOnFinish && m_ShowResultPanels)
                FreezeWanderers();

            if (m_Hud != null && m_ShowResultPanels)
                m_Hud.ShowComplete(m_LevelNumber);

            LevelCompleted?.Invoke();
        }

        void RefreshProgress()
        {
            if (m_Hud != null)
                m_Hud.SetProgress(Progress, ConnectedNodes, TotalNodes);
        }

        public void Fail(string reason)
        {
            if (State == LevelState.Complete || State == LevelState.Failed)
                return;

            State = LevelState.Failed;

            // Stop the run so a late delivery can't still complete a failed level.
            if (m_Quest != null)
                m_Quest.Halt();

            if (m_FreezeOnFinish)
                FreezeWanderers();

            if (m_Hud != null && m_ShowResultPanels)
                m_Hud.ShowFailed(reason);

            LevelFailed?.Invoke();
        }

        void FreezeWanderers()
        {
            foreach (var wanderer in FindObjectsByType<RoadWanderer>(FindObjectsSortMode.None))
                wanderer.enabled = false;
        }

        // ---- Buttons ----

        public void Retry() => GameFlow.ReloadCurrentScene();

        public void NextLevel()
        {
            var next = m_LevelNumber + 1;
            if (GameFlow.HasLevel(next))
                GameFlow.LoadLevel(next);
            else
                GameFlow.LoadMainMenu();
        }

        public void ReturnToMainMenu() => GameFlow.LoadMainMenu();
    }
}
