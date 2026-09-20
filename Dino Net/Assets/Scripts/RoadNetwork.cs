using System;
using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// The dirt-road graph across the jungle. Junctions sit at every dino node and fork; roads
    /// carry their smoothed centre-line so guides and wandering dinos can follow them exactly.
    /// </summary>
    public class RoadNetwork : MonoBehaviour
    {
        [Serializable]
        public class Junction
        {
            public string id;
            public Vector3 position;
            public bool wanderable = true;
        }

        [Serializable]
        public class Road
        {
            public string from;
            public string to;
            public Vector3[] points;
        }

        [SerializeField]
        List<Junction> m_Junctions = new List<Junction>();

        [SerializeField]
        List<Road> m_Roads = new List<Road>();

        public IReadOnlyList<Junction> Junctions => m_Junctions;
        public IReadOnlyList<Road> Roads => m_Roads;

        public void Configure(List<Junction> junctions, List<Road> roads)
        {
            m_Junctions = junctions;
            m_Roads = roads;
        }

        public int IndexOf(string id)
        {
            for (var i = 0; i < m_Junctions.Count; i++)
            {
                if (m_Junctions[i].id == id)
                    return i;
            }

            return -1;
        }

        public int NearestJunction(Vector3 position)
        {
            var best = -1;
            var bestSqr = float.MaxValue;
            for (var i = 0; i < m_Junctions.Count; i++)
            {
                var d = m_Junctions[i].position - position;
                d.y = 0f;
                var sqr = d.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        public int RandomWanderableJunction(int exclude)
        {
            var candidates = new List<int>();
            for (var i = 0; i < m_Junctions.Count; i++)
            {
                if (i != exclude && m_Junctions[i].wanderable)
                    candidates.Add(i);
            }

            return candidates.Count == 0 ? -1 : candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        /// <summary>Fills <paramref name="result"/> with the shortest road centre-line from one junction to another.</summary>
        public bool TryGetPath(int from, int to, List<Vector3> result)
        {
            result.Clear();
            if (from < 0 || to < 0 || from >= m_Junctions.Count || to >= m_Junctions.Count)
                return false;

            if (from == to)
            {
                result.Add(m_Junctions[from].position);
                return true;
            }

            var count = m_Junctions.Count;
            var dist = new float[count];
            var prevJunction = new int[count];
            var prevRoad = new int[count];
            var done = new bool[count];
            for (var i = 0; i < count; i++)
            {
                dist[i] = float.PositiveInfinity;
                prevJunction[i] = -1;
                prevRoad[i] = -1;
            }

            dist[from] = 0f;
            for (var step = 0; step < count; step++)
            {
                var u = -1;
                for (var i = 0; i < count; i++)
                {
                    if (!done[i] && (u == -1 || dist[i] < dist[u]))
                        u = i;
                }

                if (u == -1 || float.IsPositiveInfinity(dist[u]))
                    break;

                done[u] = true;
                if (u == to)
                    break;

                for (var r = 0; r < m_Roads.Count; r++)
                {
                    var road = m_Roads[r];
                    var a = IndexOf(road.from);
                    var b = IndexOf(road.to);
                    int v;
                    if (a == u)
                        v = b;
                    else if (b == u)
                        v = a;
                    else
                        continue;

                    var alt = dist[u] + Length(road);
                    if (alt < dist[v])
                    {
                        dist[v] = alt;
                        prevJunction[v] = u;
                        prevRoad[v] = r;
                    }
                }
            }

            if (float.IsPositiveInfinity(dist[to]))
                return false;

            var chain = new List<int>();
            for (var at = to; at != -1; at = prevJunction[at])
                chain.Add(at);
            chain.Reverse();

            for (var i = 0; i < chain.Count - 1; i++)
            {
                var road = m_Roads[prevRoad[chain[i + 1]]];
                var forward = IndexOf(road.from) == chain[i];
                var start = result.Count == 0 ? 0 : 1;
                if (forward)
                {
                    for (var p = start; p < road.points.Length; p++)
                        result.Add(road.points[p]);
                }
                else
                {
                    for (var p = road.points.Length - 1 - start; p >= 0; p--)
                        result.Add(road.points[p]);
                }
            }

            return result.Count > 0;
        }

        static float Length(Road road)
        {
            var total = 0f;
            for (var i = 1; i < road.points.Length; i++)
                total += Vector3.Distance(road.points[i - 1], road.points[i]);
            return total;
        }
    }
}
