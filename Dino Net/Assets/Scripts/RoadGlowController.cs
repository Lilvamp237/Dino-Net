using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Lights up the energy-vine strands along each stretch of road the moment the orb is
    /// delivered at the end of it - one connection at a time, not only when the whole route is done.
    /// Reads the quest's nodes (never writes to them), so it works alongside DinoQuestManager unchanged.
    /// </summary>
    public class RoadGlowController : MonoBehaviour
    {
        [SerializeField, Tooltip("Route nodes in visit order. Segment i is the road that leads into node i.")]
        List<QuestNode> m_Route = new List<QuestNode>();

        [SerializeField, Tooltip("One entry per route node: every strand renderer that makes up that stretch of road.")]
        List<SegmentRenderers> m_Segments = new List<SegmentRenderers>();

        [SerializeField]
        float m_RestIntensity = 0.0f;

        [SerializeField]
        float m_ConnectedIntensity = 2.2f;

        [SerializeField, Tooltip("Extra flash right when a connection is made, fading back to the connected glow.")]
        float m_FlashBoost = 3.5f;

        [SerializeField]
        float m_FlashDecay = 1.6f;

        [SerializeField]
        float m_FadeInSpeed = 4f;

        [System.Serializable]
        public class SegmentRenderers
        {
            public List<Renderer> strands = new List<Renderer>();
        }

        static readonly int k_Intensity = Shader.PropertyToID("_Intensity");

        float[] m_Level;
        float[] m_Flash;
        bool[] m_WasCompleted;
        MaterialPropertyBlock m_Block;

        void Awake()
        {
            m_Block = new MaterialPropertyBlock();
            m_Level = new float[m_Segments.Count];
            m_Flash = new float[m_Segments.Count];
            m_WasCompleted = new bool[m_Segments.Count];
            for (var i = 0; i < m_Level.Length; i++)
                m_Level[i] = m_RestIntensity;
        }

        void Update()
        {
            var count = Mathf.Min(m_Segments.Count, m_Route.Count);
            for (var i = 0; i < count; i++)
            {
                var node = m_Route[i];
                var completed = node != null && node.IsCompleted;

                if (completed && !m_WasCompleted[i])
                    m_Flash[i] = m_FlashBoost;
                m_WasCompleted[i] = completed;

                var target = completed ? m_ConnectedIntensity : m_RestIntensity;
                m_Level[i] = Mathf.MoveTowards(m_Level[i], target, m_FadeInSpeed * Time.deltaTime);
                m_Flash[i] = Mathf.MoveTowards(m_Flash[i], 0f, m_FlashDecay * Time.deltaTime);

                Apply(m_Segments[i], m_Level[i] + m_Flash[i]);
            }
        }

        void Apply(SegmentRenderers segment, float intensity)
        {
            foreach (var strand in segment.strands)
            {
                if (strand == null)
                    continue;

                strand.GetPropertyBlock(m_Block);
                m_Block.SetFloat(k_Intensity, intensity);
                strand.SetPropertyBlock(m_Block);
            }
        }
    }
}
