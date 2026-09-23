using System.Collections;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Makes a node dinosaur visibly react when the message reaches it - a couple of happy hops
    /// with a little wiggle, so a child can see the receiving dinosaur respond rather than just a
    /// ring changing colour.
    /// </summary>
    /// <remarks>
    /// The models animate their bones through a legacy <see cref="Animation"/> component under
    /// RootNode, so this drives the object's own root transform instead - the two never fight.
    /// </remarks>
    public class NodeArrivalReaction : MonoBehaviour
    {
        [SerializeField]
        int m_Hops = 2;

        [SerializeField]
        float m_HopHeight = 0.35f;

        [SerializeField]
        float m_HopDuration = 0.4f;

        [SerializeField, Tooltip("Degrees the dino swings side to side while hopping.")]
        float m_Wiggle = 12f;

        Coroutine m_Routine;

        public void Play()
        {
            if (!isActiveAndEnabled)
                return;

            if (m_Routine != null)
                StopCoroutine(m_Routine);

            m_Routine = StartCoroutine(HopRoutine());
        }

        IEnumerator HopRoutine()
        {
            var basePosition = transform.position;
            var baseRotation = transform.rotation;

            for (var hop = 0; hop < m_Hops; hop++)
            {
                var elapsed = 0f;
                while (elapsed < m_HopDuration)
                {
                    elapsed += Time.deltaTime;
                    var t = Mathf.Clamp01(elapsed / m_HopDuration);

                    // A single arch per hop, with the wiggle running twice as fast.
                    transform.position = basePosition + Vector3.up * (Mathf.Sin(t * Mathf.PI) * m_HopHeight);
                    transform.rotation = baseRotation * Quaternion.Euler(0f, Mathf.Sin(t * Mathf.PI * 2f) * m_Wiggle, 0f);
                    yield return null;
                }
            }

            transform.SetPositionAndRotation(basePosition, baseRotation);
            m_Routine = null;
        }
    }
}
