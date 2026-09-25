using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Makes the firefly the "Golden Firefly": it turns gold and, every time it sets off toward
    /// the next dinosaur, briefly shows how it chose the way - a few possible roads in dim blue and
    /// the shortest one in gold with its length. This is a real search over the road network
    /// (shortest path, then the best alternatives if a road were closed), shown so a child can see
    /// what an "intelligent router" does.
    /// </summary>
    public class FireflyAnalysis : MonoBehaviour
    {
        static readonly Color k_Gold = new Color(1f, 0.78f, 0.15f);

        GuideFirefly m_Firefly;
        RoadNetwork m_Roads;
        DinoQuestManager m_Quest;
        Material m_LineMaterial;
        readonly List<GameObject> m_Drawn = new List<GameObject>();
        Coroutine m_Routine;
        bool m_ExplainedOnce;

        void Start()
        {
            m_Firefly = GetComponent<GuideFirefly>();
            m_Roads = FindFirstObjectByType<RoadNetwork>();
            m_Quest = FindFirstObjectByType<DinoQuestManager>();
            if (m_Firefly == null)
            {
                enabled = false;
                return;
            }

            GoldenLook();
            m_Firefly.RouteHintStarted += OnRoute;
        }

        void OnDestroy()
        {
            if (m_Firefly != null)
                m_Firefly.RouteHintStarted -= OnRoute;

            Clear();
        }

        /// <summary>Tints the firefly's body, halo, light and sparkle trail gold.</summary>
        void GoldenLook()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer)
                    continue;

                if (r.name == "Body")
                    r.material.SetColor("_BaseColor", new Color(2.4f, 1.5f, 0.25f));
                else if (r.name == "Halo")
                    r.material.SetColor("_BaseColor", new Color(0.7f, 0.45f, 0.05f));
            }

            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                if (ps.name == "Halo")
                    main.startColor = new Color(1f, 0.8f, 0.25f, 1f);
                else
                    main.startColor = new Color(1f, 0.85f, 0.3f, 1f);
            }

            foreach (var l in GetComponentsInChildren<Light>(true))
                l.color = new Color(1f, 0.82f, 0.35f);
        }

        void OnRoute(QuestNode target)
        {
            if (m_Roads == null || target == null)
                return;

            if (m_Routine != null)
                StopCoroutine(m_Routine);

            m_Routine = StartCoroutine(Analyse(target));
        }

        IEnumerator Analyse(QuestNode target)
        {
            // Let the firefly finish building its own path this frame.
            yield return null;
            Clear();

            var best = new List<Vector3>(m_Firefly.CurrentPath);
            if (best.Count < 2)
                yield break;

            var start = best[0];
            var end = best[best.Count - 1];
            var from = m_Roads.NearestJunction(start);
            var to = m_Roads.NearestJunction(end);

            // Alternatives: close one road of the best route at a time and find the next-best way.
            var roadsUsed = new List<int>();
            var check = new List<Vector3>();
            m_Roads.TryGetPath(from, to, check, null, roadsUsed);

            var alternatives = new List<List<Vector3>>();
            var tried = 0;
            foreach (var road in roadsUsed)
            {
                if (tried++ >= 2)
                    break;

                var alt = new List<Vector3>();
                if (m_Roads.TryGetPath(from, to, alt, new[] { road }, null) && alt.Count > 1)
                {
                    var duplicate = false;
                    foreach (var existing in alternatives)
                    {
                        if (Mathf.Abs(RoadNetwork.PathLength(existing) - RoadNetwork.PathLength(alt)) < 0.5f)
                            duplicate = true;
                    }

                    if (!duplicate)
                        alternatives.Add(alt);
                }
            }

            var bestLength = RoadNetwork.PathLength(best);
            var lines = new List<LineRenderer>();
            foreach (var alt in alternatives)
            {
                var line = Draw(alt, new Color(0.55f, 0.75f, 1f, 0.55f), 0.07f);
                lines.Add(line);
                Label(alt, Mathf.RoundToInt(RoadNetwork.PathLength(alt)) + " m", new Color(0.7f, 0.85f, 1f), 0.45f);
            }

            var goldLine = Draw(best, k_Gold, 0.16f);
            Label(best, Mathf.RoundToInt(bestLength) + " m - shortest!", k_Gold, 0.5f);

            if (!m_ExplainedOnce && m_Quest != null)
            {
                m_ExplainedOnce = true;
                m_Quest.Announce("The Golden Firefly compares the roads and picks the shortest one!");
            }

            SessionTelemetry.Log("firefly_analysis", SessionTelemetry.Instance != null ? SessionTelemetry.Instance.CurrentLevel : 0,
                "shortest", bestLength, "alternatives", alternatives.Count);

            // Show the comparison, then fade the alternatives and finally the gold route.
            yield return new WaitForSeconds(2.6f);
            foreach (var line in lines)
                if (line != null)
                    Destroy(line.gameObject);

            yield return new WaitForSeconds(2.4f);
            if (goldLine != null)
            {
                var t = 1f;
                while (t > 0f && goldLine != null)
                {
                    t -= Time.deltaTime;
                    var c = k_Gold;
                    c.a = Mathf.Clamp01(t);
                    goldLine.startColor = c;
                    goldLine.endColor = c;
                    yield return null;
                }
            }

            Clear();
        }

        LineRenderer Draw(List<Vector3> path, Color color, float width)
        {
            if (m_LineMaterial == null)
                m_LineMaterial = new Material(Shader.Find("Sprites/Default"));

            var go = new GameObject("Firefly Route");
            go.transform.SetParent(transform.parent, true);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = m_LineMaterial;
            line.useWorldSpace = true;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.numCornerVertices = 3;
            line.positionCount = path.Count;
            for (var i = 0; i < path.Count; i++)
                line.SetPosition(i, new Vector3(path[i].x, 0.16f, path[i].z));

            line.startColor = color;
            line.endColor = color;
            m_Drawn.Add(go);
            return line;
        }

        void Label(List<Vector3> path, string text, Color color, float lift)
        {
            var mid = path[path.Count / 2];
            var canvas = RuntimeUi.WorldCanvas("Route Label", new Vector2(520f, 110f), 0.004f, null, false);
            canvas.transform.position = new Vector3(mid.x, 0.55f + lift, mid.z);
            canvas.gameObject.AddComponent<Billboard>();
            RuntimeUi.Panel(canvas.transform, "Background", new Color(0.05f, 0.08f, 0.14f, 0.75f), Vector2.zero, new Vector2(520f, 110f));
            RuntimeUi.Text(canvas.transform, "Text", text, 56f, color, Vector2.zero, new Vector2(500f, 100f));
            m_Drawn.Add(canvas.gameObject);
        }

        void Clear()
        {
            foreach (var go in m_Drawn)
            {
                if (go != null)
                    Destroy(go);
            }

            m_Drawn.Clear();
        }
    }
}
