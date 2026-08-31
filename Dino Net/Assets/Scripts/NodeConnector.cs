using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// v1 connection interaction: select one node, then select another, and an energy vine
    /// links them. Selecting the same node twice cancels the pending connection.
    /// </summary>
    public class NodeConnector : MonoBehaviour
    {
        [SerializeField, Tooltip("Prefab with a LineRenderer + EnergyVineVisual, instantiated between two connected nodes.")]
        EnergyVineVisual m_VinePrefab;

        NetworkNode m_PendingNode;

        void OnEnable()
        {
            NetworkNode.AnyNodeSelected += OnNodeSelected;
        }

        void OnDisable()
        {
            NetworkNode.AnyNodeSelected -= OnNodeSelected;
        }

        void OnNodeSelected(NetworkNode node)
        {
            if (m_PendingNode == null)
            {
                m_PendingNode = node;
                return;
            }

            if (m_PendingNode == node)
            {
                m_PendingNode = null;
                return;
            }

            TryConnect(m_PendingNode, node);
            m_PendingNode = null;
        }

        void TryConnect(NetworkNode a, NetworkNode b)
        {
            if (!NetworkTopologyManager.Instance.TryAddEdge(a, b))
                return;

            var vine = Instantiate(m_VinePrefab);
            vine.Initialize(a.ConnectionAnchor, b.ConnectionAnchor);
            NetworkTopologyManager.Instance.RegisterEdgeVisual(a.NodeId, b.NodeId, vine);
        }
    }
}
