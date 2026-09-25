using System.Collections;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Teaches acknowledgements ("got it!") and resending. Every dinosaur that receives the packet
    /// sends a little "Got it!" heart back to the child. Once per level one delivery goes wrong:
    /// the packet is lost on the way, no "got it!" comes back, and the child has to send it again.
    /// This runs before the quest's own delivery check so it can catch that one delivery.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class LostPacketMechanic : MonoBehaviour
    {
        /// <summary>Which stop (0 = first) loses the packet on its first attempt.</summary>
        public int LostIndex = 1;

        DinoQuestManager m_Quest;
        CarryableOrb m_Orb;
        bool m_Lost;
        bool m_Busy;

        void Start()
        {
            m_Quest = FindFirstObjectByType<DinoQuestManager>();
            if (m_Quest == null || m_Quest.Orb == null)
            {
                enabled = false;
                return;
            }

            m_Orb = m_Quest.Orb;
            m_Quest.NodeConnected += OnNode;
        }

        void OnDestroy()
        {
            if (m_Quest != null)
                m_Quest.NodeConnected -= OnNode;
        }

        void Update()
        {
            if (m_Lost || m_Busy || m_Quest == null || !m_Quest.IsRunning || m_Quest.DeliveryPaused)
                return;

            var target = m_Quest.CurrentTarget;
            if (target == null || m_Quest.ConnectedCount != LostIndex || !m_Orb.IsHeld)
                return;

            if (target.IsOrbInRange(m_Orb.transform.position))
                StartCoroutine(Lose(target));
        }

        IEnumerator Lose(QuestNode target)
        {
            m_Lost = true;
            m_Busy = true;
            m_Quest.DeliveryPaused = true;

            Sfx.Play2D("lost_whoosh", 0.9f);
            RuntimeUi.Toast("Oh no! The packet got lost on the way!", new Color(1f, 0.7f, 0.4f), 3.5f);
            VoiceOver.Speak("Oh no! The packet got lost on the way!");
            SessionTelemetry.Log("packet_lost", SessionTelemetry.Instance != null ? SessionTelemetry.Instance.CurrentLevel : 0, "node", target.FriendlyName);

            var last = m_Quest.ConnectedCount > 0 ? m_Quest.Route[m_Quest.ConnectedCount - 1].DeliveryAnchor.position : m_Orb.transform.position;
            MechanicsUtil.ReturnOrb(m_Orb, last, 1.05f);

            yield return new WaitForSeconds(2.4f);
            RuntimeUi.Toast("No \"got it!\" came back. Pick the packet up and send it again!", new Color(0.6f, 0.9f, 1f), 4.5f);
            VoiceOver.Speak("No got it came back. Pick the packet up and send it again!");

            yield return new WaitForSeconds(1.2f);
            m_Quest.DeliveryPaused = false;
            m_Busy = false;
        }

        /// <summary>The receiving dinosaur sends a "Got it!" heart back to the child.</summary>
        void OnNode(QuestNode node)
        {
            StartCoroutine(SendAck(node));
        }

        IEnumerator SendAck(QuestNode node)
        {
            yield return new WaitForSeconds(0.5f);

            var canvas = RuntimeUi.WorldCanvas("Ack", new Vector2(420f, 150f), 0.004f, null, false);
            var start = node.transform.position + Vector3.up * 2.4f;
            canvas.transform.position = start;
            canvas.gameObject.AddComponent<Billboard>();
            RuntimeUi.Panel(canvas.transform, "Heart", new Color(1f, 0.4f, 0.55f), new Vector2(-140f, 0f), new Vector2(110f, 110f), RuntimeUi.Heart);
            RuntimeUi.Text(canvas.transform, "Text", "Got it!", 82f, Color.white, new Vector2(50f, 0f), new Vector2(260f, 120f));
            Sfx.PlayAt("ack_ding", start, 0.9f, 1.1f);

            var cam = Camera.main;
            var t = 0f;
            while (t < 2.2f && canvas != null)
            {
                t += Time.deltaTime;
                if (cam != null)
                {
                    var target = cam.transform.position + cam.transform.forward * 1.2f + Vector3.up * 0.2f;
                    canvas.transform.position = Vector3.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t / 2.2f));
                }

                yield return null;
            }

            if (canvas != null)
                Destroy(canvas.gameObject);
        }
    }
}
