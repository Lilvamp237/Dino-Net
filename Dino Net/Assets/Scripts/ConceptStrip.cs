using UnityEngine;
using UnityEngine.UI;

namespace DinoNet
{
    /// <summary>
    /// The little animation under a "Did You Know?" fact: a packet token hops along a row of
    /// icons, lighting each one as it arrives, then starts over. Says the same thing as the fact
    /// without asking a five-year-old to read it.
    /// </summary>
    public class ConceptStrip : MonoBehaviour
    {
        [SerializeField, Tooltip("The travelling packet.")]
        RectTransform m_Token;

        [SerializeField, Tooltip("Cell anchors in order, left to right. Unused cells should be left out.")]
        RectTransform[] m_Cells;

        [SerializeField, Tooltip("Icon of each cell, brightened as the packet reaches it.")]
        Image[] m_CellIcons;

        [SerializeField]
        float m_HopSeconds = 0.75f;

        [SerializeField]
        float m_HoldSeconds = 0.5f;

        [SerializeField, Tooltip("Pause before the loop restarts from the left.")]
        float m_LoopPause = 1.1f;

        [SerializeField]
        float m_DimAlpha = 0.35f;

        [SerializeField, Tooltip("How high the packet arcs between cells.")]
        float m_ArcHeight = 26f;

        int m_From;
        float m_Timer;
        bool m_Holding = true;

        void OnEnable()
        {
            m_From = 0;
            m_Timer = 0f;
            m_Holding = true;
            Light(0);
            Place(0f);
        }

        void Update()
        {
            if (m_Cells == null || m_Cells.Length < 2 || m_Token == null)
                return;

            m_Timer += Time.unscaledDeltaTime;

            if (m_Holding)
            {
                var wait = m_From >= m_Cells.Length - 1 ? m_LoopPause : m_HoldSeconds;
                if (m_Timer < wait)
                    return;

                m_Timer = 0f;
                m_Holding = false;

                if (m_From >= m_Cells.Length - 1)
                {
                    // Round again from the left.
                    m_From = 0;
                    m_Holding = true;
                    Light(0);
                    Place(0f);
                }

                return;
            }

            var t = Mathf.Clamp01(m_Timer / m_HopSeconds);
            Place(t);

            if (t < 1f)
                return;

            m_From++;
            m_Timer = 0f;
            m_Holding = true;
            Light(m_From);
        }

        /// <summary>Slides the token from cell <c>m_From</c> to the next one, with a small arc.</summary>
        void Place(float t)
        {
            if (m_Token == null || m_Cells == null || m_Cells.Length == 0)
                return;

            var from = m_Cells[Mathf.Clamp(m_From, 0, m_Cells.Length - 1)];
            var to = m_Cells[Mathf.Clamp(m_From + 1, 0, m_Cells.Length - 1)];
            if (from == null || to == null)
                return;

            var eased = Mathf.SmoothStep(0f, 1f, t);
            var position = Vector2.Lerp(from.anchoredPosition, to.anchoredPosition, eased);
            position.y += Mathf.Sin(eased * Mathf.PI) * m_ArcHeight;
            m_Token.anchoredPosition = position;
        }

        /// <summary>Brightens every cell up to <paramref name="upTo"/> and dims the rest.</summary>
        void Light(int upTo)
        {
            if (m_CellIcons == null)
                return;

            for (var i = 0; i < m_CellIcons.Length; i++)
            {
                if (m_CellIcons[i] == null)
                    continue;

                var c = m_CellIcons[i].color;
                m_CellIcons[i].color = new Color(c.r, c.g, c.b, i <= upTo ? 1f : m_DimAlpha);
            }
        }
    }
}
