using System.Collections.Generic;

namespace DinoNet
{
    /// <summary>
    /// Pure graph algorithms over the current <see cref="NetworkTopologyManager"/> state.
    /// No MonoBehaviour, no side effects - easy to unit test and to call from multiple places.
    /// </summary>
    public static class RoutingLogic
    {
        public static bool HasPath(NetworkTopologyManager topology, string sourceId, string targetId)
        {
            return FindPathBfs(topology, sourceId, targetId) != null;
        }

        /// <summary>
        /// Finds any path (fewest hops) from source to target. Used to decide whether a packet
        /// can be delivered at all - a broken link means no path exists, per the proposal's
        /// packet-switching / reliability teaching point.
        /// </summary>
        public static List<string> FindPathBfs(NetworkTopologyManager topology, string sourceId, string targetId)
        {
            if (sourceId == targetId)
                return new List<string> { sourceId };

            var visited = new HashSet<string> { sourceId };
            var queue = new Queue<string>();
            var cameFrom = new Dictionary<string, string>();
            queue.Enqueue(sourceId);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in topology.GetNeighborIds(current))
                {
                    if (!visited.Add(neighbor))
                        continue;

                    cameFrom[neighbor] = current;
                    if (neighbor == targetId)
                        return ReconstructPath(cameFrom, sourceId, targetId);

                    queue.Enqueue(neighbor);
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the physically shortest path (Dijkstra, weighted by real distance between node
        /// anchors) for the AI Firefly "optimal route" highlight. This can differ from the
        /// fewest-hop BFS path when a mesh has multiple relay options.
        /// </summary>
        public static List<string> FindShortestWeightedPath(NetworkTopologyManager topology, string sourceId, string targetId)
        {
            var nodeIds = new List<string>();
            foreach (var node in topology.Nodes)
                nodeIds.Add(node.NodeId);

            if (!nodeIds.Contains(sourceId) || !nodeIds.Contains(targetId))
                return null;

            var distances = new Dictionary<string, float>();
            var cameFrom = new Dictionary<string, string>();
            var visited = new HashSet<string>();
            var unvisited = new List<string>(nodeIds);

            foreach (var id in nodeIds)
                distances[id] = float.PositiveInfinity;
            distances[sourceId] = 0f;

            while (unvisited.Count > 0)
            {
                unvisited.Sort((a, b) => distances[a].CompareTo(distances[b]));
                var current = unvisited[0];
                unvisited.RemoveAt(0);

                if (float.IsPositiveInfinity(distances[current]))
                    break;

                if (current == targetId)
                    break;

                visited.Add(current);

                foreach (var neighbor in topology.GetNeighborIds(current))
                {
                    if (visited.Contains(neighbor))
                        continue;

                    var newDist = distances[current] + topology.GetEdgeDistance(current, neighbor);
                    if (newDist < distances[neighbor])
                    {
                        distances[neighbor] = newDist;
                        cameFrom[neighbor] = current;
                    }
                }
            }

            if (sourceId != targetId && !cameFrom.ContainsKey(targetId))
                return null;

            return ReconstructPath(cameFrom, sourceId, targetId);
        }

        static List<string> ReconstructPath(Dictionary<string, string> cameFrom, string sourceId, string targetId)
        {
            var path = new List<string> { targetId };
            var current = targetId;
            while (current != sourceId)
            {
                current = cameFrom[current];
                path.Add(current);
            }

            path.Reverse();
            return path;
        }
    }
}
