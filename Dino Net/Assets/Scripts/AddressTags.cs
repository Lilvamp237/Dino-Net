using TMPro;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Teaches addresses. Every dinosaur on the route gets a home number floating above it, and the
    /// packet carries a "deliver to Home N" label that changes at each stop - like the address on a
    /// parcel. The questions in this level ask which dinosaur lives at which number.
    /// </summary>
    public class AddressTags : MonoBehaviour
    {
        DinoQuestManager m_Quest;
        TMP_Text m_PacketAddress;
        GameObject m_PacketChip;

        void Start()
        {
            m_Quest = FindFirstObjectByType<DinoQuestManager>();
            if (m_Quest == null || m_Quest.RouteCount == 0)
            {
                enabled = false;
                return;
            }

            for (var i = 0; i < m_Quest.Route.Count; i++)
            {
                var node = m_Quest.Route[i];
                if (node == null)
                    continue;

                var tag = MechanicsUtil.Tag("HOME " + (i + 1), new Color(1f, 0.9f, 0.5f), node.transform.position + Vector3.up * 3.6f, 460f, 68f);
                tag.transform.SetParent(node.transform, true);
            }

            BuildPacketChip();
            m_Quest.QuestStarted += Refresh;
            m_Quest.NodeConnected += OnNode;
        }

        void OnDestroy()
        {
            if (m_Quest != null)
            {
                m_Quest.QuestStarted -= Refresh;
                m_Quest.NodeConnected -= OnNode;
            }
        }

        void BuildPacketChip()
        {
            var orb = m_Quest.Orb;
            if (orb == null)
                return;

            var canvas = RuntimeUi.WorldCanvas("Packet Address", new Vector2(420f, 100f), 0.0016f, orb.transform, false);
            canvas.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            canvas.transform.localScale = Vector3.one * 0.0016f / Mathf.Max(0.05f, orb.transform.lossyScale.x);
            canvas.gameObject.AddComponent<Billboard>();
            RuntimeUi.Panel(canvas.transform, "Background", new Color(0.32f, 0.2f, 0.04f, 0.9f), Vector2.zero, new Vector2(420f, 100f));
            m_PacketAddress = RuntimeUi.Text(canvas.transform, "Text", "DELIVER TO HOME 1", 42f, new Color(1f, 0.95f, 0.7f), Vector2.zero, new Vector2(400f, 90f));
            m_PacketChip = canvas.gameObject;
            m_PacketChip.SetActive(false);
        }

        void OnNode(QuestNode node) => Refresh();

        void Refresh()
        {
            if (m_PacketChip == null)
                return;

            var index = m_Quest.ConnectedCount;
            m_PacketChip.SetActive(index < m_Quest.RouteCount);
            if (m_PacketAddress != null)
                m_PacketAddress.text = "DELIVER TO HOME " + (index + 1);
        }
    }
}
