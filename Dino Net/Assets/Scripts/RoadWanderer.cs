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

        [Header("Obstacle avoidance")]
        [SerializeField, Tooltip("Off by default so the original Demo scene keeps its old behaviour; the playable levels switch it on.")]
        bool m_AvoidObstacles;

        [SerializeField, Tooltip("Roughly half the body width. Set automatically from the model's bounds by the level builder.")]
        float m_BodyRadius = 0.6f;

        [SerializeField, Tooltip("How far ahead to look for something in the way.")]
        float m_LookAhead = 2.5f;

        [SerializeField, Tooltip("How hard the dino steers away from obstacles.")]
        float m_AvoidStrength = 2.5f;

        [SerializeField, Tooltip("Which layers count as solid. Floors are ignored automatically by their flatness.")]
        LayerMask m_ObstacleMask = 1;

        static readonly Collider[] s_Overlap = new Collider[24];

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

            var heading = to.normalized;
            if (m_AvoidObstacles)
            {
                heading = Steer(heading);
                if (heading == Vector3.zero)
                {
                    // Fully blocked - stand and wait rather than walking through whatever it is.
                    Play(m_IdleClip);
                    return;
                }

                Play(m_WalkClip);
            }

            var look = Quaternion.LookRotation(heading, Vector3.up) * Quaternion.Euler(0f, m_YawOffset, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, m_TurnSpeed * Time.deltaTime);
            transform.position += heading * (m_WalkSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Bends the desired heading away from nearby dinos, trees and nodes, and returns
        /// <see cref="Vector3.zero"/> when the way ahead is genuinely blocked.
        /// </summary>
        Vector3 Steer(Vector3 desired)
        {
            var chest = transform.position + Vector3.up * m_BodyRadius;
            var push = Vector3.zero;
            var range = m_BodyRadius + m_LookAhead;

            var count = Physics.OverlapSphereNonAlloc(chest, range, s_Overlap, m_ObstacleMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var other = s_Overlap[i];
                if (other == null || other.transform.IsChildOf(transform))
                    continue;

                // Floors, ground rings and roads are flat - they're scenery to walk on, not around.
                if (other.bounds.size.y < 0.35f)
                    continue;

                var away = chest - other.ClosestPoint(chest);
                away.y = 0f;
                var distance = away.magnitude;
                if (distance < 0.0001f)
                {
                    // Exactly overlapping: shove straight out so two bodies can't share a space.
                    away = transform.position - other.bounds.center;
                    away.y = 0f;
                    distance = Mathf.Max(away.magnitude, 0.0001f);
                }

                var strength = Mathf.Clamp01(1f - (distance - m_BodyRadius) / m_LookAhead);
                push += away / distance * strength;
            }

            var heading = (desired + push * m_AvoidStrength).normalized;
            if (heading == Vector3.zero)
                heading = desired;

            // Final check: if something solid is still right in front, hold position this frame.
            if (Physics.SphereCast(chest, m_BodyRadius * 0.9f, heading, out var hit, m_LookAhead, m_ObstacleMask, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform)
                && hit.collider.bounds.size.y >= 0.35f
                && hit.distance < m_BodyRadius * 0.75f)
            {
                return Vector3.zero;
            }

            return heading;
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
