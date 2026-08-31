using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Owns the node/edge graph for the scene: which dinos exist and which are currently
    /// linked by an energy vine. NetworkNode registers itself here, NodeConnector adds edges.
    /// </summary>
    public class NetworkTopologyManager : MonoBehaviour
    {
        public static NetworkTopologyManager Instance { get; private set; }

        readonly List<NetworkNode> m_Nodes = new List<NetworkNode>();
        readonly HashSet<(string a, string b)> m_Edges = new HashSet<(string, string)>();
        readonly Dictionary<(string a, string b), (EnergyVineVisual visual, string startId)> m_EdgeVisuals =
            new Dictionary<(string, string), (EnergyVineVisual, string)>();

        public event Action TopologyChanged;

        public IReadOnlyList<NetworkNode> Nodes => m_Nodes;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Multiple NetworkTopologyManager instances found; destroying the extra one.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void RegisterNode(NetworkNode node)
        {
            if (!m_Nodes.Contains(node))
                m_Nodes.Add(node);
        }

        public void UnregisterNode(NetworkNode node)
        {
            m_Nodes.Remove(node);
        }

        public NetworkNode GetNode(string nodeId)
        {
            return m_Nodes.FirstOrDefault(n => n.NodeId == nodeId);
        }

        public bool TryAddEdge(NetworkNode a, NetworkNode b)
        {
            if (a == null || b == null || a == b)
                return false;

            if (!m_Edges.Add(OrderedKey(a.NodeId, b.NodeId)))
                return false;

            TopologyChanged?.Invoke();
            return true;
        }

        public bool HasEdge(string nodeIdA, string nodeIdB)
        {
            return m_Edges.Contains(OrderedKey(nodeIdA, nodeIdB));
        }

        public List<string> GetNeighborIds(string nodeId)
        {
            var neighbors = new List<string>();
            foreach (var edge in m_Edges)
            {
                if (edge.a == nodeId)
                    neighbors.Add(edge.b);
                else if (edge.b == nodeId)
                    neighbors.Add(edge.a);
            }

            return neighbors;
        }

        public void RegisterEdgeVisual(string startId, string endId, EnergyVineVisual visual)
        {
            m_EdgeVisuals[OrderedKey(startId, endId)] = (visual, startId);
        }

        public EnergyVineVisual GetEdgeVisual(string nodeIdA, string nodeIdB)
        {
            return m_EdgeVisuals.TryGetValue(OrderedKey(nodeIdA, nodeIdB), out var entry) ? entry.visual : null;
        }

        /// <summary>
        /// Same as <see cref="GetEdgeVisual"/> but also reports whether the vine was originally
        /// authored from <paramref name="fromId"/> to <paramref name="toId"/> or the reverse,
        /// so callers can sample the curve in the right direction.
        /// </summary>
        public bool TryGetEdgeVisual(string fromId, string toId, out EnergyVineVisual visual, out bool reversed)
        {
            if (m_EdgeVisuals.TryGetValue(OrderedKey(fromId, toId), out var entry))
            {
                visual = entry.visual;
                reversed = entry.startId != fromId;
                return true;
            }

            visual = null;
            reversed = false;
            return false;
        }

        public float GetEdgeDistance(string nodeIdA, string nodeIdB)
        {
            var a = GetNode(nodeIdA);
            var b = GetNode(nodeIdB);
            if (a == null || b == null)
                return float.PositiveInfinity;

            return Vector3.Distance(a.ConnectionAnchor.position, b.ConnectionAnchor.position);
        }

        static (string, string) OrderedKey(string a, string b)
        {
            return string.CompareOrdinal(a, b) <= 0 ? (a, b) : (b, a);
        }
    }
}
