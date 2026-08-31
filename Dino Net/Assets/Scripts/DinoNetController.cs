using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Top-level orchestrator: whenever the topology changes, checks every Source -> EndUser
    /// pair for a newly-completed path, sends a packet along it, highlights the AI-optimal
    /// route, and triggers the destination's success feedback on arrival.
    /// </summary>
    public class DinoNetController : MonoBehaviour
    {
        [SerializeField]
        AIPathOptimizer m_PathOptimizer;

        [SerializeField]
        PacketController m_PacketPrefab;

        [SerializeField, Range(2, 30), Tooltip("Points sampled along each vine's curve when building a packet's travel path.")]
        int m_CurveSamplesPerHop = 10;

        readonly HashSet<(string source, string endUser)> m_DeliveredPairs = new HashSet<(string, string)>();

        void Start()
        {
            NetworkTopologyManager.Instance.TopologyChanged += OnTopologyChanged;
        }

        void OnDestroy()
        {
            if (NetworkTopologyManager.Instance != null)
                NetworkTopologyManager.Instance.TopologyChanged -= OnTopologyChanged;
        }

        void OnTopologyChanged()
        {
            var topology = NetworkTopologyManager.Instance;
            var sources = topology.Nodes.Where(n => n.NodeType == NodeType.Source);
            var endUsers = topology.Nodes.Where(n => n.NodeType == NodeType.EndUser);

            foreach (var source in sources)
            {
                foreach (var endUser in endUsers)
                {
                    var pairKey = (source.NodeId, endUser.NodeId);
                    if (m_DeliveredPairs.Contains(pairKey))
                        continue;

                    var path = RoutingLogic.FindPathBfs(topology, source.NodeId, endUser.NodeId);
                    if (path == null)
                        continue;

                    m_DeliveredPairs.Add(pairKey);
                    SendPacket(topology, path, endUser);

                    if (m_PathOptimizer != null)
                        m_PathOptimizer.HighlightOptimalPath(source.NodeId, endUser.NodeId);
                }
            }
        }

        void SendPacket(NetworkTopologyManager topology, List<string> path, NetworkNode endUser)
        {
            if (m_PacketPrefab == null)
                return;

            var waypoints = BuildWaypoints(topology, path);
            if (waypoints.Count == 0)
                return;

            var packet = Instantiate(m_PacketPrefab, waypoints[0], Quaternion.identity);
            packet.TravelPath(waypoints, () =>
            {
                var feedback = endUser.GetComponent<VisualFeedbackController>();
                feedback?.PlayDeliverySuccess();
                LearningProgressTracker.Instance?.RecordDelivery();
            });
        }

        List<Vector3> BuildWaypoints(NetworkTopologyManager topology, List<string> path)
        {
            var waypoints = new List<Vector3>();

            for (var i = 0; i < path.Count - 1; i++)
            {
                if (topology.TryGetEdgeVisual(path[i], path[i + 1], out var vine, out var reversed))
                {
                    for (var s = 0; s <= m_CurveSamplesPerHop; s++)
                    {
                        var t = s / (float)m_CurveSamplesPerHop;
                        waypoints.Add(vine.GetPointAt(reversed ? 1f - t : t));
                    }
                }
                else
                {
                    var a = topology.GetNode(path[i]);
                    var b = topology.GetNode(path[i + 1]);
                    if (a == null || b == null)
                        continue;

                    waypoints.Add(a.ConnectionAnchor.position);
                    waypoints.Add(b.ConnectionAnchor.position);
                }
            }

            return waypoints;
        }
    }
}
