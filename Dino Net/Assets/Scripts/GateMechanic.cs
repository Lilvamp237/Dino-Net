using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Teaches firewalls. After a set number of stops a gate appears on the road, guarded by a
    /// bouncer dinosaur (one of the roaming dinos, borrowed for the moment). The level's question
    /// is the bouncer asking for the HTTPS shield; a correct answer opens the gate.
    /// </summary>
    public class GateMechanic : MonoBehaviour
    {
        public int AfterNodes = 2;

        DinoQuestManager m_Quest;
        RoadNetwork m_Roads;
        DecisionPanel m_Panel;
        Transform m_Bar;
        GameObject m_Root;
        Animation m_BouncerAnim;
        string m_BouncerIdle;
        string m_BouncerJump;
        bool m_Built;
        bool m_Open;

        void Start()
        {
            m_Quest = FindFirstObjectByType<DinoQuestManager>();
            m_Roads = FindFirstObjectByType<RoadNetwork>();
            m_Panel = FindFirstObjectByType<DecisionPanel>(FindObjectsInactive.Include);
            if (m_Quest == null)
            {
                enabled = false;
                return;
            }

            m_Quest.NodeConnected += OnNode;
            if (m_Panel != null)
                m_Panel.Passed += OnPassed;
        }

        void OnDestroy()
        {
            if (m_Quest != null)
                m_Quest.NodeConnected -= OnNode;

            if (m_Panel != null)
                m_Panel.Passed -= OnPassed;
        }

        void OnNode(QuestNode node)
        {
            if (m_Built || m_Quest.ConnectedCount != AfterNodes || m_Quest.CurrentTarget == null)
                return;

            m_Built = true;
            Build(node, m_Quest.CurrentTarget);
            RuntimeUi.Toast("Halt! The Firewall Bouncer guards this gate.", new Color(1f, 0.75f, 0.4f), 3.5f);
            VoiceOver.Speak("Halt! The Firewall Bouncer guards this gate.");
        }

        void Build(QuestNode from, QuestNode to)
        {
            var path = MechanicsUtil.RoadPath(m_Roads, from.DeliveryAnchor.position, to.DeliveryAnchor.position);
            var point = MechanicsUtil.PointAlong(path, 5f, out var dir);
            point.y = 0f;
            var side = Vector3.Cross(Vector3.up, dir).normalized;

            m_Root = new GameObject("Firewall Gate");
            m_Root.transform.position = point;
            var wood = MechanicsUtil.GlowMaterial(new Color(0.55f, 0.36f, 0.2f));
            var stripe = MechanicsUtil.GlowMaterial(new Color(1f, 0.55f, 0.25f));

            MechanicsUtil.Box("Post L", m_Root.transform, point + side * 1.9f + Vector3.up * 1.2f, new Vector3(0.3f, 2.4f, 0.3f), wood, true);
            MechanicsUtil.Box("Post R", m_Root.transform, point - side * 1.9f + Vector3.up * 1.2f, new Vector3(0.3f, 2.4f, 0.3f), wood, true);
            var bar = MechanicsUtil.Box("Bar", m_Root.transform, point + Vector3.up * 1.15f, new Vector3(0.24f, 0.24f, 3.8f), stripe, true);
            bar.transform.rotation = Quaternion.LookRotation(side, Vector3.up);
            m_Bar = bar.transform;

            var tag = MechanicsUtil.Tag("FIREWALL GATE", new Color(1f, 0.75f, 0.4f), point + Vector3.up * 3.2f, 560f, 62f);
            tag.transform.SetParent(m_Root.transform, true);

            BorrowBouncer(point + side * 3f, -side);
        }

        void BorrowBouncer(Vector3 position, Vector3 facing)
        {
            RoadWanderer best = null;
            var bestDistance = float.MaxValue;
            foreach (var w in FindObjectsByType<RoadWanderer>(FindObjectsSortMode.None))
            {
                var d = Vector3.Distance(w.transform.position, position);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = w;
                }
            }

            if (best == null)
                return;

            best.enabled = false;
            var t = best.transform;
            t.position = new Vector3(position.x, t.position.y, position.z);
            facing.y = 0f;
            t.rotation = Quaternion.LookRotation(facing.normalized, Vector3.up);

            m_BouncerAnim = best.GetComponent<Animation>();
            if (m_BouncerAnim != null)
            {
                foreach (AnimationState state in m_BouncerAnim)
                {
                    if (state.name.EndsWith("_Idle")) m_BouncerIdle = state.name;
                    else if (state.name.EndsWith("_Jump")) m_BouncerJump = state.name;
                }

                if (!string.IsNullOrEmpty(m_BouncerIdle))
                {
                    m_BouncerAnim[m_BouncerIdle].wrapMode = WrapMode.Loop;
                    m_BouncerAnim.CrossFade(m_BouncerIdle, 0.2f);
                }
            }

            var tag = MechanicsUtil.Tag("BOUNCER", new Color(1f, 0.9f, 0.6f), position + Vector3.up * 2.6f, 380f, 56f);
            tag.transform.SetParent(t, true);
        }

        void OnPassed(NetworkLesson lesson)
        {
            if (m_Open || m_Root == null || lesson == null || lesson.conceptTerm != ConceptCatalog.Firewall)
                return;

            // Only the shield question opens the gate; the intro question does not.
            if (lesson.packetLabel != "SHIELD")
                return;

            m_Open = true;
            StartCoroutine(OpenGate());
        }

        IEnumerator OpenGate()
        {
            Sfx.Play2D("gate_open", 0.9f);
            RuntimeUi.Toast("The bouncer says: come on in!", new Color(0.5f, 1f, 0.7f), 3f);
            VoiceOver.Speak("The bouncer says: come on in!");
            if (m_BouncerAnim != null && !string.IsNullOrEmpty(m_BouncerJump))
                m_BouncerAnim.CrossFade(m_BouncerJump, 0.2f);

            var start = m_Bar.position;
            var t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 1.2f;
                m_Bar.position = start + Vector3.up * (Mathf.SmoothStep(0f, 1f, t) * 1.6f);
                yield return null;
            }

            foreach (var c in m_Bar.GetComponents<Collider>())
                c.enabled = false;
        }
    }
}
