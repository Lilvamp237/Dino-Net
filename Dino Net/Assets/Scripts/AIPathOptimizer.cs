using System.Collections;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// The "Golden Firefly" bonus feature: highlights the physically shortest path between a
    /// source and an end-user by flashing its vines gold for a moment.
    /// </summary>
    public class AIPathOptimizer : MonoBehaviour
    {
        [SerializeField]
        Color m_HighlightColor = new Color(1f, 0.84f, 0f);

        [SerializeField]
        float m_HighlightDuration = 2f;

        public void HighlightOptimalPath(string sourceId, string targetId)
        {
            var path = RoutingLogic.FindShortestWeightedPath(NetworkTopologyManager.Instance, sourceId, targetId);
            if (path == null)
                return;

            for (var i = 0; i < path.Count - 1; i++)
            {
                var vine = NetworkTopologyManager.Instance.GetEdgeVisual(path[i], path[i + 1]);
                if (vine != null)
                    StartCoroutine(FlashGold(vine));
            }
        }

        IEnumerator FlashGold(EnergyVineVisual vine)
        {
            var original = vine.GetColor();
            vine.SetColor(m_HighlightColor);
            yield return new WaitForSeconds(m_HighlightDuration);
            vine.SetColor(original);
        }
    }
}
