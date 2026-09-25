using System.Collections;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Shows where the message comes from: when the run starts, the source dinosaur lights up as
    /// it hands the packet over.
    /// </summary>
    /// <remarks>
    /// Deliberately does NOT preview the route. The Golden Firefly is what shows the child where
    /// to go; a connection only appears once they actually arrive, so a strand on the ground
    /// always means "this link has been established".
    /// </remarks>
    public class RoutePresenter : MonoBehaviour
    {
        [SerializeField]
        DinoQuestManager m_Quest;

        [SerializeField, Tooltip("The dinosaur the message is sent from - it lights up as the packet appears.")]
        Renderer m_SourceHighlight;

        [SerializeField]
        Transform m_SourceAnchor;

        [SerializeField]
        float m_HighlightSeconds = 5f;

        [SerializeField]
        Light m_SourceLight;

        static readonly int k_EmissionColor = Shader.PropertyToID("_EmissionColor");

        MaterialPropertyBlock m_Block;

        void Awake() => m_Block = new MaterialPropertyBlock();

        void OnEnable()
        {
            if (m_Quest != null)
                m_Quest.QuestStarted += OnQuestStarted;
        }

        void OnDisable()
        {
            if (m_Quest != null)
                m_Quest.QuestStarted -= OnQuestStarted;
        }

        void OnQuestStarted() => StartCoroutine(SourceHandover());

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
    }
}
