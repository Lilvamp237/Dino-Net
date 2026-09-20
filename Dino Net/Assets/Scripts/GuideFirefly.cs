using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// A floating firefly that leads the child along the dirt roads to the next dino on the route.
    /// It waits for the child to catch up rather than racing ahead, and only reads quest state
    /// (QuestNode.IsCompleted and whether the orb is active) so DinoQuestManager stays untouched.
    /// </summary>
    public class GuideFirefly : MonoBehaviour
    {
        [SerializeField]
        RoadNetwork m_Network;

        [SerializeField, Tooltip("Route nodes in visit order.")]
        List<QuestNode> m_Route = new List<QuestNode>();

        [SerializeField, Tooltip("The orb becomes active when the quest starts; before that the firefly waits by the podium.")]
        CarryableOrb m_Orb;

        [SerializeField]
        Transform m_IdleAnchor;

        [SerializeField]
        Transform[] m_Wings;

        [SerializeField]
        Light m_Light;

        [SerializeField]
        float m_Speed = 3.2f;

        [SerializeField]
        float m_HoverHeight = 1.9f;

        [SerializeField, Tooltip("If the child falls this far behind, the firefly stops and hovers until they catch up.")]
        float m_WaitDistance = 6f;

        [SerializeField, Tooltip("The firefly sets off again once the child is this close.")]
        float m_ResumeDistance = 3.5f;

        readonly List<Vector3> m_Path = new List<Vector3>();
        QuestNode m_PathTarget;
        int m_PathIndex;
        Vector3 m_Velocity;
        bool m_Waiting;
        Transform m_Head;
        float m_Seed;

        void Awake()
        {
            m_Seed = Random.value * 100f;
            if (m_IdleAnchor != null)
                transform.position = m_IdleAnchor.position + Vector3.up * 1.2f;
        }

        void Update()
        {
            if (m_Head == null && Camera.main != null)
                m_Head = Camera.main.transform;

            var goal = ComputeGoal(out var speed);
            transform.position = Vector3.SmoothDamp(transform.position, goal, ref m_Velocity, 0.3f, speed);

            AnimateBody();
        }

        Vector3 ComputeGoal(out float speed)
        {
            speed = m_Speed;
            var started = m_Orb != null && m_Orb.gameObject.activeInHierarchy;
            var target = NextTarget();

            if (!started || m_Network == null)
                return IdleGoal(m_IdleAnchor != null ? m_IdleAnchor.position : transform.position, 0.7f, 0.9f);

            if (target == null)
                return IdleGoal(LastNodePosition(), 1.6f, 2.4f);

            if (target != m_PathTarget)
                BuildPath(target);

            var headDistance = HeadDistance();
            if (m_Waiting && headDistance < m_ResumeDistance)
                m_Waiting = false;
            else if (!m_Waiting && headDistance > m_WaitDistance)
                m_Waiting = true;

            if (m_Waiting || m_PathIndex >= m_Path.Count)
                return Hover(transform.position);

            var next = m_Path[m_PathIndex] + Vector3.up * m_HoverHeight;
            var flat = next - transform.position;
            flat.y = 0f;
            if (flat.magnitude < 0.7f)
                m_PathIndex++;

            speed = headDistance < m_ResumeDistance ? m_Speed * 1.3f : m_Speed;
            return next + Wobble();
        }

        QuestNode NextTarget()
        {
            foreach (var node in m_Route)
            {
                if (node != null && !node.IsCompleted)
                    return node;
            }

            return null;
        }

        Vector3 LastNodePosition()
        {
            for (var i = m_Route.Count - 1; i >= 0; i--)
            {
                if (m_Route[i] != null)
                    return m_Route[i].DeliveryAnchor.position;
            }

            return transform.position;
        }

        void BuildPath(QuestNode target)
        {
            m_PathTarget = target;
            m_PathIndex = 0;
            m_Waiting = false;

            var from = m_Network.NearestJunction(transform.position);
            var to = m_Network.NearestJunction(target.DeliveryAnchor.position);
            if (!m_Network.TryGetPath(from, to, m_Path))
                m_Path.Clear();
        }

        float HeadDistance()
        {
            if (m_Head == null)
                return 0f;

            var d = m_Head.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        Vector3 Hover(Vector3 around)
        {
            return around + Wobble();
        }

        Vector3 IdleGoal(Vector3 centre, float radius, float height)
        {
            var t = Time.time * 0.7f + m_Seed;
            return centre + new Vector3(Mathf.Cos(t) * radius, height + Mathf.Sin(t * 1.7f) * 0.15f, Mathf.Sin(t) * radius);
        }

        Vector3 Wobble()
        {
            var t = Time.time + m_Seed;
            return new Vector3(
                (Mathf.PerlinNoise(t * 0.8f, 0f) - 0.5f) * 0.6f,
                Mathf.Sin(t * 2.3f) * 0.18f,
                (Mathf.PerlinNoise(0f, t * 0.8f) - 0.5f) * 0.6f);
        }

        void AnimateBody()
        {
            var flap = Mathf.Sin(Time.time * 42f) * 38f;
            for (var i = 0; i < m_Wings.Length; i++)
            {
                if (m_Wings[i] == null)
                    continue;

                var side = i % 2 == 0 ? 1f : -1f;
                m_Wings[i].localRotation = Quaternion.Euler(0f, 0f, side * (25f + flap));
            }

            if (m_Light != null)
            {
                var pulse = m_Waiting ? 5f : 2.5f;
                m_Light.intensity = 1.7f + Mathf.Sin(Time.time * pulse) * 0.6f;
            }
        }
    }
}
