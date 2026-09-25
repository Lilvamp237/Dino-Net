using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DinoNet
{
    /// <summary>
    /// The reward moment when a level is finished: the "Village Power-Up" (every dinosaur on the
    /// route lights up in turn) followed by a star panel, streak and the next suggested level.
    /// It reads the result the telemetry bridge scored; it never changes the level's own panels.
    /// </summary>
    public class RewardPresenter : MonoBehaviour
    {
        DinoQuestManager m_Quest;
        LevelManager m_Level;
        TelemetryBridge m_Bridge;
        GameObject m_Panel;

        void Start()
        {
            m_Quest = FindFirstObjectByType<DinoQuestManager>();
            m_Level = FindFirstObjectByType<LevelManager>();
            m_Bridge = TelemetryBridge.Current;
            if (m_Quest == null || m_Level == null)
            {
                enabled = false;
                return;
            }

            if (m_Bridge != null)
                m_Bridge.LevelScored += OnScored;

            m_Quest.QuestCompleted += OnCompleted;
            ProgressStore.BadgeEarned += OnBadge;
        }

        void OnDestroy()
        {
            if (m_Bridge != null)
                m_Bridge.LevelScored -= OnScored;

            if (m_Quest != null)
                m_Quest.QuestCompleted -= OnCompleted;

            ProgressStore.BadgeEarned -= OnBadge;
        }

        void OnBadge(ProgressStore.BadgeInfo badge)
        {
            RuntimeUi.Toast("Badge earned: " + badge.name, new Color(1f, 0.85f, 0.3f), 4f, RuntimeUi.Star);
            Sfx.Play2D("sparkle", 0.8f);
        }

        void OnCompleted()
        {
            StartCoroutine(PowerUp());
        }

        /// <summary>Every dinosaur on the route lights up in turn, so the whole village "powers up".</summary>
        IEnumerator PowerUp()
        {
            var nodes = new List<QuestNode>(m_Quest.Route);
            var pitch = 0.85f;
            foreach (var node in nodes)
            {
                if (node == null)
                    continue;

                node.PlayArrival(null);
                Sfx.Play2D("ack_ding", 0.7f, pitch);
                pitch += 0.09f;
                StartCoroutine(LightUp(node.transform.position + Vector3.up * 2.5f));

                var reaction = node.GetComponent<NodeArrivalReaction>();
                if (reaction != null)
                    reaction.Play();

                yield return new WaitForSeconds(0.32f);
            }

            Sfx.Play2D("power_up", 0.9f);
        }

        IEnumerator LightUp(Vector3 position)
        {
            var go = new GameObject("Village Light");
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.45f);
            light.range = 11f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;

            var t = 0f;
            while (t < 1.2f)
            {
                t += Time.deltaTime;
                light.intensity = Mathf.Lerp(0f, 4.5f, t / 1.2f);
                yield return null;
            }
        }

        void OnScored(TelemetryBridge.LevelResult result)
        {
            if (result.level <= 0)
                return;

            StartCoroutine(ShowStars(result));
        }

        IEnumerator ShowStars(TelemetryBridge.LevelResult result)
        {
            // Give the completion panel and the power-up wave a moment first.
            yield return new WaitForSeconds(1.6f);

            var anchor = new GameObject("Reward Panel");
            var canvas = RuntimeUi.WorldCanvas("Reward Canvas", new Vector2(1000f, 520f), 0.0016f, anchor.transform, false);
            canvas.transform.localPosition = new Vector3(0f, 0f, 2.2f);
            RuntimeUi.Panel(canvas.transform, "Background", new Color(0.04f, 0.14f, 0.16f, 0.92f), Vector2.zero, new Vector2(1000f, 520f));
            m_Panel = anchor;

            RuntimeUi.Text(canvas.transform, "Title", result.newBest ? "New best!" : "Your stars", 60f,
                result.newBest ? new Color(1f, 0.9f, 0.4f) : Color.white, new Vector2(0f, 205f), new Vector2(900f, 80f));

            var stars = new Image[3];
            for (var i = 0; i < 3; i++)
            {
                stars[i] = RuntimeUi.Panel(canvas.transform, "Star " + i, new Color(1f, 1f, 1f, 0.16f), new Vector2((i - 1) * 210f, 80f), new Vector2(190f, 190f), RuntimeUi.Star);
                stars[i].transform.localScale = Vector3.one;
            }

            var streak = ProgressStore.Streak;
            var next = ProgressStore.RecommendedLevel();
            var minutes = Mathf.Max(1, Mathf.RoundToInt(result.seconds / 60f));
            RuntimeUi.Text(canvas.transform, "Detail",
                result.mistakes == 0 ? "No mistakes - amazing!" : result.mistakes + (result.mistakes == 1 ? " mistake" : " mistakes") + " - you're learning!",
                40f, new Color(0.85f, 0.95f, 1f), new Vector2(0f, -55f), new Vector2(900f, 60f));
            RuntimeUi.Text(canvas.transform, "Streak", (streak > 1 ? streak + " days in a row!   " : string.Empty) + "Time: " + minutes + " min   Total stars: " + ProgressStore.TotalStars,
                34f, new Color(0.7f, 0.9f, 0.85f), new Vector2(0f, -115f), new Vector2(900f, 50f));
            if (result.level < GameFlow.LastLevel || next != result.level)
            {
                RuntimeUi.Text(canvas.transform, "Next", "Suggested next: Level " + next, 42f, new Color(1f, 0.9f, 0.5f), new Vector2(0f, -190f), new Vector2(900f, 60f));
            }

            PanelAnchor.PlaceInFront(anchor, 1.25f);
            anchor.transform.SetParent(null, true);

            for (var i = 0; i < result.stars; i++)
            {
                yield return new WaitForSeconds(0.55f);
                Sfx.Play2D("ack_ding", 0.8f, 1f + i * 0.2f);
                yield return PopStar(stars[i]);
            }

            if (result.stars == 3)
                Sfx.Play2D("sparkle", 0.9f);
        }

        static IEnumerator PopStar(Image star)
        {
            star.color = new Color(1f, 0.85f, 0.25f, 1f);
            var t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                var k = t / 0.35f;
                star.transform.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.45f);
                yield return null;
            }

            star.transform.localScale = Vector3.one;
        }
    }
}
