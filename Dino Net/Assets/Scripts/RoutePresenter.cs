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

        [SerializeField, Tooltip("Vine drawn briefly from the source dino to the first node, showing the connection the message will travel.")]
        EnergyVineVisual m_PreviewVinePrefab;

        [SerializeField]
        Transform m_SourceAnchor;

        [SerializeField, Tooltip("How long the next node keeps pulsing after it becomes the target.")]
        float m_HighlightSeconds = 5f;

        [SerializeField]
        Light m_SourceLight;

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
            HighlightTarget();
        }

        void OnNodeConnected(QuestNode node) => HighlightTarget();

        void OnQuestCompleted()
        {
            if (m_PreviewVine != null)
                Destroy(m_PreviewVine.gameObject);
        }

        /// <summary>The source dino glows and a connection appears toward the first node.</summary>
        IEnumerator SourceHandover()
        {
            var target = m_Quest.CurrentTarget;
            if (m_PreviewVinePrefab != null && m_SourceAnchor != null && target != null)
            {
                m_PreviewVine = Instantiate(m_PreviewVinePrefab, transform);
                m_PreviewVine.Initialize(m_SourceAnchor, target.VineAnchor);
            }

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
