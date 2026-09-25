using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Teaches backup routes. After a set number of stops a road on the way to the next dinosaur
    /// gets blocked by a rockfall. The Golden Firefly instantly finds another road, and the level's
    /// question asks the child what to do. A blocked road is only ever picked if another way
    /// really exists, so the child is never stuck.
    /// </summary>
    public class BlockedRoadMechanic : MonoBehaviour
    {
        /// <summary>The road is blocked once this many stops are connected.</summary>
        public int AfterNodes = 3;

        DinoQuestManager m_Quest;
        RoadNetwork m_Roads;
        GuideFirefly m_Firefly;
        bool m_Done;

        void Start()
        {
            m_Quest = FindFirstObjectByType<DinoQuestManager>();
            m_Roads = FindFirstObjectByType<RoadNetwork>();
            m_Firefly = FindFirstObjectByType<GuideFirefly>();
            if (m_Quest == null || m_Roads == null)
            {
                enabled = false;
                return;
            }

            m_Quest.NodeConnected += OnNode;
        }

        void OnDestroy()
        {
            if (m_Quest != null)
                m_Quest.NodeConnected -= OnNode;
        }

        void OnNode(QuestNode node)
        {
            if (m_Done || m_Quest.ConnectedCount != AfterNodes || m_Quest.CurrentTarget == null)
                return;

            StartCoroutine(Block(node, m_Quest.CurrentTarget));
        }

        IEnumerator Block(QuestNode from, QuestNode to)
        {
            yield return new WaitForSeconds(0.6f);

            var a = m_Roads.NearestJunction(from.DeliveryAnchor.position);
            var b = m_Roads.NearestJunction(to.DeliveryAnchor.position);
            var path = new List<Vector3>();
            var used = new List<int>();
            if (!m_Roads.TryGetPath(a, b, path, null, used) || used.Count == 0)
                yield break;

            var mainLength = RoadNetwork.PathLength(path);

            // Prefer a road near the middle of the way, and only one that has a sensible detour.
            var middle = used.Count / 2;
            var positions = new List<int>();
            for (var i = 0; i < used.Count; i++)
                positions.Add(i);
            positions.Sort((x, y) => Mathf.Abs(x - middle).CompareTo(Mathf.Abs(y - middle)));

            var chosen = -1;
            var alt = new List<Vector3>();
            foreach (var position in positions)
            {
                var roadIndex = used[position];
                if (m_Roads.TryGetPath(a, b, alt, new[] { roadIndex }, null) && RoadNetwork.PathLength(alt) <= mainLength * 2.4f + 6f)
                {
                    chosen = roadIndex;
                    break;
                }
            }

            if (chosen < 0)
                yield break;

            m_Done = true;
            m_Roads.SetBlocked(chosen, true);
            var road = m_Roads.Roads[chosen];
            var mid = road.points[road.points.Length / 2];
            var dir = road.points[Mathf.Min(road.points.Length - 1, road.points.Length / 2 + 1)] - road.points[Mathf.Max(0, road.points.Length / 2 - 1)];
            dir.y = 0f;
            dir = dir.sqrMagnitude < 0.0001f ? Vector3.forward : dir.normalized;
            BuildBarrier(mid, dir);

            Sfx.Play2D("lost_whoosh", 0.7f, 0.8f);
            RuntimeUi.Toast("A rockfall blocked the road! The firefly is finding a backup road.", new Color(1f, 0.75f, 0.4f), 4.5f);
            VoiceOver.Speak("A rockfall blocked the road! The firefly is finding a backup road.");
            SessionTelemetry.Log("road_blocked", SessionTelemetry.Instance != null ? SessionTelemetry.Instance.CurrentLevel : 0, "detourMetres", RoadNetwork.PathLength(alt) - mainLength);

            if (m_Firefly != null)
                m_Firefly.RebuildPath();
        }

        static void BuildBarrier(Vector3 centre, Vector3 roadDirection)
        {
            var root = new GameObject("Rockfall").transform;
            root.position = new Vector3(centre.x, 0f, centre.z);
            var side = Vector3.Cross(Vector3.up, roadDirection);
            var rock = MechanicsUtil.GlowMaterial(new Color(0.42f, 0.4f, 0.4f));
            var dark = MechanicsUtil.GlowMaterial(new Color(0.3f, 0.29f, 0.3f));

            var rng = new System.Random((int)(centre.x * 100f) ^ (int)(centre.z * 57f));
            for (var i = 0; i < 7; i++)
            {
                var offset = side * (i - 3) * 0.55f + roadDirection * ((float)rng.NextDouble() - 0.5f) * 0.6f;
                var size = 0.9f + (float)rng.NextDouble() * 0.7f;
                var box = MechanicsUtil.Box("Rock", root, root.position + offset + Vector3.up * size * 0.4f,
                    new Vector3(size, size * 0.8f, size), i % 2 == 0 ? rock : dark, true);
                box.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 90f, 0f);
            }

            var tag = MechanicsUtil.Tag("ROAD BLOCKED", new Color(1f, 0.55f, 0.35f), root.position + Vector3.up * 2.6f, 560f, 64f);
            tag.transform.SetParent(root, true);
        }
    }
}
