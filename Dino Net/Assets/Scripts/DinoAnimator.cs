using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Gives the dinosaurs life. The models ship playing a single looping clip, which made every
    /// dino look frozen (and the friendly ones look like they were attacking). This picks the
    /// right clips: friendly node dinos idle, look around, shuffle, turn to greet the child, and
    /// jump for joy when the packet arrives; hazard dinos idle and give a (harmless) roar as a
    /// warning; roaming dinos get footsteps and occasional grunts on top of their walking.
    /// </summary>
    public class DinoAnimator : MonoBehaviour
    {
        public enum Role
        {
            Friendly,
            Hazard,
            Wanderer,
        }

        Animation m_Anim;
        Role m_Role;
        QuestNode m_Node;
        Transform m_Head;

        string m_Idle;
        string m_Walk;
        string m_Jump;
        string m_Attack;

        Quaternion m_BaseRotation;
        float m_NextAction;
        float m_OneShotEnd;
        float m_BusyUntil;
        float m_TurnUntil;
        Quaternion m_TurnTarget;
        bool m_WasCompleted;
        bool m_PlayerNear;
        float m_NextFootstep;
        Vector3 m_LastPosition;
        float m_Big;

        void Start()
        {
            m_Anim = GetComponent<Animation>();
            if (m_Anim == null)
            {
                enabled = false;
                return;
            }

            m_Node = GetComponent<QuestNode>();
            m_BaseRotation = transform.rotation;
            m_LastPosition = transform.position;
            m_Big = transform.lossyScale.x > 0.25f ? 1f : 0f;

            foreach (AnimationState state in m_Anim)
            {
                if (state.name.EndsWith("_Idle")) m_Idle = state.name;
                else if (state.name.EndsWith("_Walk")) m_Walk = state.name;
                else if (state.name.EndsWith("_Jump")) m_Jump = state.name;
                else if (state.name.EndsWith("_Attack")) m_Attack = state.name;
            }

            if (GetComponent<RoadWanderer>() != null)
                m_Role = Role.Wanderer;
            else if (m_Node != null)
                m_Role = Role.Friendly;
            else if (name.Contains("T-Rex") || name.Contains("Volcano_Source"))
                m_Role = Role.Hazard;
            else
                m_Role = Role.Friendly;

            if (m_Role != Role.Wanderer && !string.IsNullOrEmpty(m_Idle))
            {
                m_Anim[m_Idle].wrapMode = WrapMode.Loop;
                m_Anim[m_Idle].speed = Random.Range(0.85f, 1.1f);
                m_Anim.Stop();
                m_Anim.Play(m_Idle);
                m_Anim[m_Idle].time = Random.value * m_Anim[m_Idle].length;
            }

            m_NextAction = Time.time + Random.Range(2f, 9f);
        }

        void Update()
        {
            if (m_Head == null && Camera.main != null)
                m_Head = Camera.main.transform;

            var distance = m_Head != null ? Vector3.Distance(m_Head.position, transform.position) : 99f;

            if (m_Role == Role.Wanderer)
            {
                UpdateWanderer(distance);
                return;
            }

            if (m_Node != null && m_Node.IsCompleted && !m_WasCompleted)
            {
                m_WasCompleted = true;
                m_BusyUntil = Time.time + 1.8f;
                PlayOnce(m_Jump);
                Sfx.PlayAt(m_Big > 0f ? "grunt_large" : "grunt_small", transform.position, 0.7f, 1.35f);
            }

            FinishOneShot();
            UpdateFacing(distance);

            if (Time.time >= m_NextAction && Time.time >= m_OneShotEnd && Time.time >= m_BusyUntil)
                DoRandomAction(distance);
        }

        // ---------------------------------------------------------------- friendly & hazard

        void DoRandomAction(float distance)
        {
            if (m_Role == Role.Hazard)
            {
                m_NextAction = Time.time + Random.Range(9f, 18f);
                if (distance < 26f && !string.IsNullOrEmpty(m_Attack))
                {
                    PlayOnce(m_Attack);
                    Sfx.PlayAt("roar", transform.position, 0.55f, Random.Range(1.05f, 1.3f), 32f);
                }

                return;
            }

            m_NextAction = Time.time + Random.Range(5f, 12f);
            var roll = Random.value;
            if (roll < 0.35f)
            {
                // Glance to one side and back.
                var yaw = Random.Range(25f, 45f) * (Random.value < 0.5f ? -1f : 1f);
                m_TurnTarget = m_BaseRotation * Quaternion.Euler(0f, yaw, 0f);
                m_TurnUntil = Time.time + 2.2f;
            }
            else if (roll < 0.65f && !string.IsNullOrEmpty(m_Walk))
            {
                // A little shuffle on the spot.
                PlayOnce(m_Walk, 1.6f);
            }
            else if (roll < 0.85f && !string.IsNullOrEmpty(m_Jump) && distance < 14f)
            {
                PlayOnce(m_Jump);
                Sfx.PlayAt(m_Big > 0f ? "grunt_large" : "grunt_small", transform.position, 0.4f, Random.Range(1.1f, 1.4f));
            }
            else
            {
                Sfx.PlayAt(m_Big > 0f ? "grunt_large" : "grunt_small", transform.position, 0.35f, Random.Range(0.9f, 1.2f));
            }
        }

        void UpdateFacing(float distance)
        {
            if (Time.time < m_BusyUntil)
                return;

            Quaternion goal;
            if (distance < 8f && m_Head != null && m_Role == Role.Friendly)
            {
                // Turn to greet the child when they come close.
                var toHead = m_Head.position - transform.position;
                toHead.y = 0f;
                if (toHead.sqrMagnitude < 0.01f)
                    return;

                goal = Quaternion.LookRotation(toHead.normalized, Vector3.up);
                if (!m_PlayerNear)
                {
                    m_PlayerNear = true;
                    if (m_Node != null && !m_Node.IsCompleted)
                        Sfx.PlayAt(m_Big > 0f ? "grunt_large" : "grunt_small", transform.position, 0.45f, 1.25f);
                }
            }
            else if (Time.time < m_TurnUntil)
            {
                goal = m_TurnTarget;
                m_PlayerNear = false;
            }
            else
            {
                goal = m_BaseRotation;
                m_PlayerNear = false;
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, goal, 1.6f * Time.deltaTime);
        }

        // ---------------------------------------------------------------- wanderers

        void UpdateWanderer(float distance)
        {
            var moved = Vector3.Distance(transform.position, m_LastPosition) / Mathf.Max(Time.deltaTime, 0.0001f);
            m_LastPosition = transform.position;

            // Footsteps only for dinosaurs close enough to hear.
            if (moved > 0.3f && distance < 15f && Time.time >= m_NextFootstep)
            {
                m_NextFootstep = Time.time + Mathf.Clamp(0.9f / moved, 0.35f, 1.1f);
                Sfx.PlayAt("footstep", transform.position, m_Big > 0f ? 0.5f : 0.25f, m_Big > 0f ? 0.7f : 1.1f, 14f);
            }

            if (Time.time >= m_NextAction)
            {
                m_NextAction = Time.time + Random.Range(10f, 24f);
                if (distance < 22f)
                    Sfx.PlayAt(m_Big > 0f ? "grunt_large" : "grunt_small", transform.position, 0.35f, Random.Range(0.85f, 1.15f));
            }
        }

        // ---------------------------------------------------------------- clip helpers

        void PlayOnce(string clip, float seconds = 0f)
        {
            if (string.IsNullOrEmpty(clip) || m_Anim == null)
                return;

            var state = m_Anim[clip];
            if (state == null)
                return;

            state.wrapMode = seconds > 0f ? WrapMode.Loop : WrapMode.Once;
            state.speed = 1f;
            m_Anim.CrossFade(clip, 0.2f);
            m_OneShotEnd = Time.time + (seconds > 0f ? seconds : state.length);
        }

        void FinishOneShot()
        {
            if (m_OneShotEnd > 0f && Time.time >= m_OneShotEnd && !string.IsNullOrEmpty(m_Idle))
            {
                m_OneShotEnd = 0f;
                m_Anim.CrossFade(m_Idle, 0.3f);
            }
        }
    }
}
