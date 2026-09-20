using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Lets a non-node dinosaur stroll the dirt roads: pick a junction, walk there along the road,
    /// rest a moment, repeat. Uses the model's own Walk and Idle clips.
    /// </summary>
    public class RoadWanderer : MonoBehaviour
    {
        [SerializeField]
        RoadNetwork m_Network;

        [SerializeField]
        Animation m_Animation;

        [SerializeField]
        float m_WalkSpeed = 1.2f;

        [SerializeField]
        float m_TurnSpeed = 4f;

        [SerializeField]
        Vector2 m_RestSeconds = new Vector2(2f, 6f);

        [SerializeField, Tooltip("Extra yaw if the model's forward axis isn't +Z.")]
        float m_YawOffset;

        readonly List<Vector3> m_Path = new List<Vector3>();
        int m_Index;
        float m_Rest;
        float m_GroundY;
        string m_WalkClip;
        string m_IdleClip;
        string m_Playing;
        int m_CurrentJunction = -1;

        void Start()
        {
            m_GroundY = transform.position.y;
            if (m_Animation == null)
                m_Animation = GetComponent<Animation>();

            if (m_Animation != null)
            {
                foreach (AnimationState state in m_Animation)
                {
                    if (state.name.EndsWith("_Walk"))
                        m_WalkClip = state.name;
                    else if (state.name.EndsWith("_Idle"))
                        m_IdleClip = state.name;
                }

                if (m_WalkClip != null)
                    m_Animation[m_WalkClip].wrapMode = WrapMode.Loop;
                if (m_IdleClip != null)
                    m_Animation[m_IdleClip].wrapMode = WrapMode.Loop;
            }

            m_Rest = Random.Range(0f, m_RestSeconds.y);
            Play(m_IdleClip);
        }

        void Update()
        {
            if (m_Network == null)
                return;

            if (m_Index >= m_Path.Count)
            {
                m_Rest -= Time.deltaTime;
                if (m_Rest <= 0f)
                    PickDestination();
                return;
            }

            var target = m_Path[m_Index];
            target.y = m_GroundY;
            var to = target - transform.position;
            to.y = 0f;

            if (to.magnitude < 0.25f)
            {
                m_Index++;
                if (m_Index >= m_Path.Count)
                {
                    m_Rest = Random.Range(m_RestSeconds.x, m_RestSeconds.y);
                    Play(m_IdleClip);
                }

                return;
            }

            var look = Quaternion.LookRotation(to.normalized, Vector3.up) * Quaternion.Euler(0f, m_YawOffset, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, m_TurnSpeed * Time.deltaTime);
            transform.position += to.normalized * (m_WalkSpeed * Time.deltaTime);
        }

        void PickDestination()
        {
            var from = m_CurrentJunction >= 0 ? m_CurrentJunction : m_Network.NearestJunction(transform.position);
            var to = m_Network.RandomWanderableJunction(from);
            if (to < 0 || !m_Network.TryGetPath(from, to, m_Path))
            {
                m_Rest = 2f;
                return;
            }

            m_CurrentJunction = to;
            m_Index = 0;
            Play(m_WalkClip);
        }

        void Play(string clip)
        {
            if (m_Animation == null || string.IsNullOrEmpty(clip) || m_Playing == clip)
                return;

            m_Playing = clip;
            m_Animation.CrossFade(clip, 0.25f);
        }
    }
}
