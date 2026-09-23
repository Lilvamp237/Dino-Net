using System.Collections;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Makes the networking idea readable without words: when the run starts, the source
    /// dinosaur lights up and hands over the message, and the node the message is headed for
    /// pulses so the child can see dinosaur - connection - message - next dinosaur.
    /// Runs alongside <see cref="DinoQuestManager"/> and only reads from it.
    /// </summary>
    public class RoutePresenter : MonoBehaviour
    {
        [SerializeField]
        DinoQuestManager m_Quest;

        [SerializeField, Tooltip("The dinosaur the message is sent from - it lights up as the packet appears.")]
        Renderer m_SourceHighlight;

        [SerializeField, Tooltip("Vine drawn to the node the message is headed for next, showing the connection it still has to travel.")]
        EnergyVineVisual m_PreviewVinePrefab;

        [SerializeField]
        Transform m_SourceAnchor;

        [SerializeField, Tooltip("How long the next node keeps pulsing after it becomes the target.")]
        float m_HighlightSeconds = 5f;

        [SerializeField]
        Light m_SourceLight;

        [SerializeField, Tooltip("Colour of the not-yet-travelled connection, kept paler than the established vines.")]
        Color m_PendingColor = new Color(0.55f, 0.85f, 1f, 0.7f);

        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock m_Block;
        EnergyVineVisual m_PreviewVine;
        Coroutine m_Highlight;

        void Awake() => m_Block = new MaterialPropertyBlock();

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

        void OnQuestStarted()
        {
            StartCoroutine(SourceHandover());
            UpdatePendingConnection();
            HighlightTarget();
        }

        void OnNodeConnected(QuestNode node)
        {
            UpdatePendingConnection();
            HighlightTarget();
        }

        void OnQuestCompleted()
        {
            if (m_PreviewVine != null)
                Destroy(m_PreviewVine.gameObject);
        }

        /// <summary>
        /// Keeps a pale vine stretched from wherever the message is now to the node it still has
        /// to reach, so the next link in the network is always visible.
        /// </summary>
        void UpdatePendingConnection()
        {
            if (m_PreviewVinePrefab == null)
                return;

            var target = m_Quest.CurrentTarget;
            if (target == null)
            {
                if (m_PreviewVine != null)
                    Destroy(m_PreviewVine.gameObject);
                return;
            }

            var connected = m_Quest.ConnectedCount;
            var from = connected == 0 ? m_SourceAnchor : m_Quest.Route[connected - 1].VineAnchor;
            if (from == null)
                return;

            if (m_PreviewVine == null)
                m_PreviewVine = Instantiate(m_PreviewVinePrefab, transform);

            m_PreviewVine.Initialize(from, target.VineAnchor);
            m_PreviewVine.SetColor(m_PendingColor);
        }

        /// <summary>The source dino glows as it hands the message over.</summary>
        IEnumerator SourceHandover()
        {
            var elapsed = 0f;
            while (elapsed < m_HighlightSeconds)
            {
                elapsed += Time.deltaTime;
                var pulse = 1.5f + Mathf.Sin(elapsed * 6f) * 1.2f;

                if (m_SourceHighlight != null)
                {
                    m_SourceHighlight.GetPropertyBlock(m_Block);
                    m_Block.SetColor(k_EmissionColor, new Color(1f, 0.9f, 0.5f) * pulse);
                    m_SourceHighlight.SetPropertyBlock(m_Block);
                }

                if (m_SourceLight != null)
                    m_SourceLight.intensity = pulse;

                yield return null;
            }

            if (m_SourceLight != null)
                m_SourceLight.intensity = 0.6f;
        }

        /// <summary>Pulses the node the message is travelling to next, then lets it settle.</summary>
        void HighlightTarget()
        {
            if (m_Highlight != null)
                StopCoroutine(m_Highlight);

            var target = m_Quest.CurrentTarget;
            if (target != null)
                m_Highlight = StartCoroutine(HighlightRoutine(target));
        }

        IEnumerator HighlightRoutine(QuestNode target)
        {
            target.SetHinted(true);
            yield return new WaitForSeconds(m_HighlightSeconds);

            if (!target.IsCompleted)
                target.SetHinted(false);

            m_Highlight = null;
        }
    }
}
