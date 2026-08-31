using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Draws a sagging "energy vine" line between two node anchors and lets callers sample
    /// points along it, so packets and AI-firefly highlights can travel the same curve.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class EnergyVineVisual : MonoBehaviour
    {
        [SerializeField, Range(2, 40)]
        int m_SegmentCount = 20;

        [SerializeField, Tooltip("How far the middle of the vine sags below a straight line between the two nodes.")]
        float m_SagAmount = 0.5f;

        [SerializeField]
        Color m_DefaultColor = new Color(0.3f, 0.9f, 1f);

        LineRenderer m_LineRenderer;
        Transform m_Start;
        Transform m_End;

        public void Initialize(Transform start, Transform end)
        {
            m_Start = start;
            m_End = end;
            m_LineRenderer = GetComponent<LineRenderer>();
            m_LineRenderer.positionCount = m_SegmentCount + 1;
            SetColor(m_DefaultColor);
            Redraw();
        }

        void LateUpdate()
        {
            if (m_Start != null && m_End != null)
                Redraw();
        }

        void Redraw()
        {
            for (var i = 0; i <= m_SegmentCount; i++)
            {
                var t = i / (float)m_SegmentCount;
                m_LineRenderer.SetPosition(i, GetPointAt(t));
            }
        }

        /// <summary>
        /// Evaluates the vine's curve at <paramref name="t"/> in [0, 1], where 0 is the start
        /// node and 1 is the end node.
        /// </summary>
        public Vector3 GetPointAt(float t)
        {
            var startPos = m_Start.position;
            var endPos = m_End.position;
            var mid = Vector3.Lerp(startPos, endPos, 0.5f) - Vector3.up * m_SagAmount;

            var u = 1f - t;
            return (u * u * startPos) + (2f * u * t * mid) + (t * t * endPos);
        }

        public Color GetColor() => m_DefaultColor;

        public void SetColor(Color color)
        {
            if (m_LineRenderer == null)
                m_LineRenderer = GetComponent<LineRenderer>();

            m_LineRenderer.startColor = color;
            m_LineRenderer.endColor = color;
        }
    }
}
