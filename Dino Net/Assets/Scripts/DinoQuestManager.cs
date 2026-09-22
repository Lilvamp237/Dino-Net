using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Drives the carry-the-packet quest: the child presses the start button, picks up the
    /// glowing orb, and carries it to each dino node in order. Every correct delivery plays a
    /// success sound and grows an energy vine along the route the child discovered.
    /// </summary>
    public class DinoQuestManager : MonoBehaviour
    {
        enum QuestState
        {
            WaitingForStart,
            Carrying,
            Complete,
        }

        [Header("Scene references")]
        [SerializeField]
        StartButton m_StartButton;

        [SerializeField]
        CarryableOrb m_Orb;

        [SerializeField, Tooltip("Where the orb sits on the podium, and where the first vine starts.")]
        Transform m_StartAnchor;

        [SerializeField, Tooltip("Dino nodes in the order the child must visit them.")]
        List<QuestNode> m_Route = new List<QuestNode>();

        [SerializeField, Tooltip("Prefab with a LineRenderer + EnergyVineVisual, grown between nodes as the route is completed.")]
        EnergyVineVisual m_VinePrefab;

        [Header("Banner")]
        [SerializeField]
        GameObject m_BannerRoot;

        [SerializeField]
        TMP_Text m_BannerText;

        [SerializeField, Tooltip("How long encouragement messages stay up. The final message stays forever.")]
        float m_BannerDuration = 4f;

        [Header("Audio")]
        [SerializeField, Tooltip("Played when the orb reaches a correct node that is not the final one.")]
        AudioClip m_ArrivalClip;

        [SerializeField, Tooltip("Played when the orb reaches the final destination node.")]
        AudioClip m_FinaleClip;

        [SerializeField, Tooltip("Gentle 'not this one' cue when the orb is taken to the wrong dino.")]
        AudioClip m_WrongNodeClip;

        [Header("Kid-friendly hints")]
        [SerializeField, Tooltip("Seconds of wandering before the correct node's ring starts glowing brighter. Set to 0 to disable hints.")]
        float m_HintDelay = 45f;

        [Header("Messages")]
        [SerializeField]
        string m_StartMessage = "Carry the glowing packet to a dino!";

        [SerializeField]
        string m_ProgressMessage = "Nice! Now find the next dino!";

        [SerializeField]
        string m_AlmostMessage = "Almost there! One more dino!";

        [SerializeField]
        string m_CompleteMessage = "Nice! You reached the destination node!";

        QuestState m_State = QuestState.WaitingForStart;
        int m_CurrentIndex;
        float m_SearchTimer;
        QuestNode m_WrongNodeLatch;
        Coroutine m_BannerRoutine;
        bool m_Halted;

        /// <summary>Raised when the child starts the run and the packet appears.</summary>
        public event Action QuestStarted;

        /// <summary>Raised each time the packet is delivered to the correct next node.</summary>
        public event Action<QuestNode> NodeConnected;

        /// <summary>Raised when the packet reaches the final destination node.</summary>
        public event Action QuestCompleted;

        /// <summary>Total nodes on this level's route.</summary>
        public int RouteCount => m_Route.Count;

        /// <summary>How many route nodes the packet has reached so far.</summary>
        public int ConnectedCount => m_CurrentIndex;

        /// <summary>The node the packet must reach next, or null once the route is finished.</summary>
        public QuestNode CurrentTarget => m_State == QuestState.Carrying && m_CurrentIndex < m_Route.Count ? m_Route[m_CurrentIndex] : null;

        public IReadOnlyList<QuestNode> Route => m_Route;

        public bool IsRunning => m_State == QuestState.Carrying && !m_Halted;

        public CarryableOrb Orb => m_Orb;

        /// <summary>Starts the run without needing the podium button (used by the tutorial and level intro).</summary>
        public void StartQuest() => BeginQuest();

        /// <summary>
        /// Freezes progression so a level that has already failed cannot still be completed.
        /// </summary>
        public void Halt()
        {
            m_Halted = true;

            if (m_BannerRoutine != null)
            {
                StopCoroutine(m_BannerRoutine);
                m_BannerRoutine = null;
            }

            if (m_BannerRoot != null)
                m_BannerRoot.SetActive(false);
        }

        void Start()
        {
            if (m_Orb != null)
            {
                m_Orb.gameObject.SetActive(false);
                if (m_StartAnchor != null)
                    m_Orb.SetHomeAnchor(m_StartAnchor);
            }

            if (m_BannerRoot != null)
                m_BannerRoot.SetActive(false);

            if (m_StartButton != null)
                m_StartButton.Pressed += BeginQuest;
        }

        void OnDestroy()
        {
            if (m_StartButton != null)
                m_StartButton.Pressed -= BeginQuest;
        }

        void Update()
        {
            if (m_Halted || m_State != QuestState.Carrying || m_Orb == null)
                return;

            var target = m_Route[m_CurrentIndex];
            var orbPosition = m_Orb.transform.position;

            if (target.IsOrbInRange(orbPosition))
            {
                DeliverTo(target);
                return;
            }

            CheckWrongNode(orbPosition, target);
            UpdateHint(target);
        }

        void BeginQuest()
        {
            if (m_Halted || m_State != QuestState.WaitingForStart || m_Route.Count == 0)
                return;

            m_State = QuestState.Carrying;
            m_CurrentIndex = 0;
            m_SearchTimer = 0f;

            if (m_Orb != null)
            {
                m_Orb.gameObject.SetActive(true);
                m_Orb.Dock();
            }

            ShowBanner(m_StartMessage, false);
            QuestStarted?.Invoke();
        }

        void DeliverTo(QuestNode node)
        {
            var isFinal = m_CurrentIndex == m_Route.Count - 1;
            node.PlayArrival(isFinal ? m_FinaleClip : m_ArrivalClip);
            node.SetHinted(false);

            GrowVineTo(node);
            LearningProgressTracker.Instance?.RecordDelivery();

            m_CurrentIndex++;
            m_SearchTimer = 0f;
            m_WrongNodeLatch = null;
            NodeConnected?.Invoke(node);

            if (isFinal)
            {
                m_State = QuestState.Complete;
                ShowBanner(m_CompleteMessage, true);
                QuestCompleted?.Invoke();
                return;
            }

            var isLastHop = m_CurrentIndex == m_Route.Count - 1;
            ShowBanner(isLastHop ? m_AlmostMessage : m_ProgressMessage, false);
        }

        /// <summary>Connects the previous stop to <paramref name="node"/> with a permanent energy vine.</summary>
        void GrowVineTo(QuestNode node)
        {
            if (m_VinePrefab == null)
                return;

            var from = m_CurrentIndex == 0 ? m_StartAnchor : m_Route[m_CurrentIndex - 1].VineAnchor;
            if (from == null)
                return;

            var vine = Instantiate(m_VinePrefab, transform);
            vine.Initialize(from, node.VineAnchor);
        }

        /// <summary>
        /// Gives a soft "not this one" nudge when the orb is carried to a dino that isn't the
        /// current target. Latched so it only fires once per visit rather than every frame.
        /// </summary>
        void CheckWrongNode(Vector3 orbPosition, QuestNode target)
        {
            QuestNode touching = null;
            foreach (var node in m_Route)
            {
                if (node == target || node.IsCompleted)
                    continue;

                if (node.IsOrbInRange(orbPosition))
                {
                    touching = node;
                    break;
                }
            }

            if (touching == m_WrongNodeLatch)
                return;

            m_WrongNodeLatch = touching;
            touching?.PlayWrongNode(m_WrongNodeClip);
        }

        void UpdateHint(QuestNode target)
        {
            if (m_HintDelay <= 0f)
                return;

            m_SearchTimer += Time.deltaTime;
            target.SetHinted(m_SearchTimer >= m_HintDelay);
        }

        void ShowBanner(string message, bool permanent)
        {
            if (m_BannerRoot == null || m_BannerText == null)
                return;

            if (m_BannerRoutine != null)
                StopCoroutine(m_BannerRoutine);

            m_BannerText.text = message;
            m_BannerRoot.SetActive(true);

            if (!permanent)
                m_BannerRoutine = StartCoroutine(HideBannerAfterDelay());
        }

        IEnumerator HideBannerAfterDelay()
        {
            yield return new WaitForSeconds(m_BannerDuration);
            m_BannerRoot.SetActive(false);
            m_BannerRoutine = null;
        }
    }
}
