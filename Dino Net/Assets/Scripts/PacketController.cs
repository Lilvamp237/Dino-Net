using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Attached to the "data packet" (glowing orb) prefab. Moves through a list of waypoints
    /// at a constant speed, then invokes a completion callback and destroys itself.
    /// </summary>
    public class PacketController : MonoBehaviour
    {
        [SerializeField, Tooltip("Units per second the packet travels along its path.")]
        float m_Speed = 2f;

        public void TravelPath(IReadOnlyList<Vector3> waypoints, Action onComplete)
        {
            StartCoroutine(TravelRoutine(waypoints, onComplete));
        }

        IEnumerator TravelRoutine(IReadOnlyList<Vector3> waypoints, Action onComplete)
        {
            if (waypoints.Count > 0)
                transform.position = waypoints[0];

            for (var i = 1; i < waypoints.Count; i++)
            {
                var from = waypoints[i - 1];
                var to = waypoints[i];
                var distance = Vector3.Distance(from, to);
                var duration = m_Speed > 0f ? distance / m_Speed : 0f;
                var elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    transform.position = Vector3.Lerp(from, to, elapsed / duration);
                    yield return null;
                }

                transform.position = to;
            }

            onComplete?.Invoke();
            Destroy(gameObject);
        }
    }
}
