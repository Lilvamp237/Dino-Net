using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DinoNet
{
    /// <summary>
    /// The tag floating above the carried packet. It always says what is being sent - DATA, GET,
    /// POST, HTTPS - so anyone watching can see that the child is moving a real message across
    /// the network rather than fetching an object.
    /// </summary>
    public class PacketLabel : MonoBehaviour
    {
        [SerializeField]
        TMP_Text m_Text;

        [SerializeField, Tooltip("Optional icon beside the word.")]
        Image m_Icon;

        [SerializeField, Tooltip("Panel behind the word, tinted to match.")]
        Image m_Background;

        [SerializeField]
        string m_DefaultText = "DATA";

        [SerializeField]
        Color m_DefaultTint = new Color(0.4f, 0.85f, 1f);

        [SerializeField, Tooltip("The packet itself. The tag hovers this far above it, ignoring the packet's own spin.")]
        Transform m_Anchor;

        [SerializeField]
        float m_Height = 0.42f;

        void Start()
        {
            Set(m_DefaultText, m_DefaultTint);
        }

        void LateUpdate()
        {
            // Held and dropped packets tumble, so the tag is kept upright above them instead of
            // riding along with the rotation.
            if (m_Anchor != null)
                transform.position = m_Anchor.position + Vector3.up * m_Height;
        }

        public void Set(string text, Color tint)
        {
            if (string.IsNullOrEmpty(text))
                return;

            if (m_Text != null)
            {
                m_Text.text = text;
                m_Text.color = tint;
            }

            if (m_Icon != null)
                m_Icon.color = tint;

            if (m_Background != null)
                m_Background.color = new Color(tint.r * 0.15f, tint.g * 0.15f, tint.b * 0.15f, 0.8f);
        }

        public void SetIcon(Sprite sprite)
        {
            if (m_Icon == null)
                return;

            m_Icon.sprite = sprite;
            m_Icon.enabled = sprite != null;
        }
    }
}
