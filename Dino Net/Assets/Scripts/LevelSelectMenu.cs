using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DinoNet
{
    /// <summary>
    /// The main menu's "Choose a level" and "My Progress" screens. Every level is open so a
    /// teacher or parent can pick what a child should practise; the suggested level (from the
    /// adaptive system) is highlighted. The progress screen shows the family code a parent types
    /// into the dashboard - there is no login inside the game.
    /// </summary>
    public class LevelSelectMenu : MonoBehaviour
    {
        static readonly string[] s_Names =
        {
            "", "Safe Connections", "Ask or Send", "Keep Secrets", "Smart Routing", "Pieces & Signal",
            "Got It!", "Find the Home", "Fake Friends", "Gatekeepers", "Grand Challenge",
        };

        [SerializeField, Tooltip("The main menu panel (hidden while these screens are open).")]
        GameObject m_MainPanel;

        GameObject m_LevelsPanel;
        GameObject m_ProgressPanel;

        TMP_Text m_VoiceLabel;
        TMP_Text m_MusicLabel;

        public void OpenLevels()
        {
            Close();
            m_LevelsPanel = BuildLevels();
            Show(m_LevelsPanel);
        }

        public void OpenProgress()
        {
            Close();
            m_ProgressPanel = BuildProgress();
            Show(m_ProgressPanel);
        }

        public void BackToMenu()
        {
            Close();
            if (m_MainPanel != null)
            {
                m_MainPanel.SetActive(true);
                PanelAnchor.PlaceInFront(m_MainPanel);
            }
        }

        void Close()
        {
            if (m_LevelsPanel != null)
                Destroy(m_LevelsPanel);
            if (m_ProgressPanel != null)
                Destroy(m_ProgressPanel);

            m_LevelsPanel = null;
            m_ProgressPanel = null;
        }

        void Show(GameObject panel)
        {
            if (m_MainPanel != null)
                m_MainPanel.SetActive(false);

            PanelAnchor.PlaceInFront(panel);
        }

        // ---------------------------------------------------------------- level select

        GameObject BuildLevels()
        {
            var anchor = new GameObject("Level Select");
            var canvas = RuntimeUi.WorldCanvas("Level Select Canvas", new Vector2(1500f, 1020f), 0.0016f, anchor.transform, true);
            canvas.transform.localPosition = new Vector3(0f, 0f, 2.2f);
            RuntimeUi.Panel(canvas.transform, "Background", new Color(0.05f, 0.18f, 0.3f, 0.95f), Vector2.zero, new Vector2(1500f, 1020f));

            RuntimeUi.Text(canvas.transform, "Title", "Choose a level", 76f, Color.white, new Vector2(0f, 445f), new Vector2(1300f, 100f));
            RuntimeUi.Text(canvas.transform, "Sub", "Every level is open. The gold one is suggested for you!", 34f, new Color(0.75f, 0.9f, 1f), new Vector2(0f, 375f), new Vector2(1300f, 50f));

            var suggested = ProgressStore.RecommendedLevel();
            for (var level = 1; level <= GameFlow.LastLevel; level++)
            {
                var col = (level - 1) % 5;
                var row = (level - 1) / 5;
                var pos = new Vector2((col - 2) * 290f, 195f - row * 270f);
                BuildCard(canvas.transform, level, pos, level == suggested);
            }

            var tutorial = RuntimeUi.MakeButton(canvas.transform, "Tutorial", new Vector2(-380f, -262f), new Vector2(560f, 100f),
                new Color(0.25f, 0.55f, 0.75f), Color.white, GameFlow.LoadTutorial, 46f);
            var sandbox = RuntimeUi.MakeButton(canvas.transform, "Sandbox: build a network", new Vector2(380f, -262f), new Vector2(560f, 100f),
                new Color(0.4f, 0.4f, 0.8f), Color.white, GameFlow.LoadSandbox, 42f);
            RuntimeUi.MakeButton(canvas.transform, "Back", new Vector2(0f, -400f), new Vector2(360f, 90f),
                new Color(1f, 1f, 1f, 0.92f), new Color(0.08f, 0.1f, 0.12f), BackToMenu, 44f);

            return anchor;
        }

        void BuildCard(Transform parent, int level, Vector2 position, bool suggested)
        {
            var levelCopy = level;
            var frame = suggested ? new Color(1f, 0.82f, 0.25f) : new Color(0.35f, 0.6f, 0.8f);
            var button = RuntimeUi.MakeButton(parent, string.Empty, position, new Vector2(262f, 246f), frame, Color.white, () => GameFlow.LoadLevel(levelCopy));
            var body = RuntimeUi.Panel(button.transform, "Body", new Color(0.09f, 0.22f, 0.34f, 1f), Vector2.zero, new Vector2(250f, 234f));
            body.raycastTarget = false;

            RuntimeUi.Text(button.transform, "Number", level.ToString(), 96f, suggested ? new Color(1f, 0.9f, 0.5f) : Color.white, new Vector2(0f, 62f), new Vector2(240f, 110f));
            RuntimeUi.Text(button.transform, "Name", s_Names[level], 32f, new Color(0.85f, 0.95f, 1f), new Vector2(0f, -22f), new Vector2(236f, 70f));

            var best = ProgressStore.BestStars(level);
            for (var i = 0; i < 3; i++)
            {
                var star = RuntimeUi.Panel(button.transform, "Star " + i, i < best ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 1f, 1f, 0.2f),
                    new Vector2((i - 1) * 62f, -80f), new Vector2(54f, 54f), RuntimeUi.Star);
                star.raycastTarget = false;
            }

            if (suggested)
                RuntimeUi.Text(button.transform, "Suggested", "SUGGESTED", 24f, new Color(1f, 0.85f, 0.3f), new Vector2(0f, 104f), new Vector2(240f, 30f));
        }

        // ---------------------------------------------------------------- progress

        GameObject BuildProgress()
        {
            var anchor = new GameObject("My Progress");
            var canvas = RuntimeUi.WorldCanvas("My Progress Canvas", new Vector2(1500f, 1020f), 0.0016f, anchor.transform, true);
            canvas.transform.localPosition = new Vector3(0f, 0f, 2.2f);
            RuntimeUi.Panel(canvas.transform, "Background", new Color(0.05f, 0.2f, 0.18f, 0.95f), Vector2.zero, new Vector2(1500f, 1020f));

            var telemetry = SessionTelemetry.Instance;
            var nickname = telemetry != null ? telemetry.Nickname : "Explorer";
            var code = telemetry != null ? telemetry.FamilyCode : "------";
            var minutes = telemetry != null ? Mathf.RoundToInt(telemetry.TotalActiveSeconds / 60f) : 0;

            RuntimeUi.Text(canvas.transform, "Title", "My Progress - " + nickname, 68f, Color.white, new Vector2(0f, 445f), new Vector2(1300f, 90f));

            RuntimeUi.Panel(canvas.transform, "CodeBox", new Color(0.02f, 0.1f, 0.1f, 0.9f), new Vector2(-380f, 300f), new Vector2(600f, 200f));
            RuntimeUi.Text(canvas.transform, "CodeLabel", "FAMILY CODE (for grown-ups)", 30f, new Color(0.7f, 0.9f, 0.85f), new Vector2(-380f, 362f), new Vector2(580f, 40f));
            RuntimeUi.Text(canvas.transform, "Code", code, 110f, new Color(1f, 0.9f, 0.45f), new Vector2(-380f, 300f), new Vector2(580f, 130f));
            RuntimeUi.Text(canvas.transform, "CodeHelp", "Type it on the dashboard to see how you're doing.", 26f, new Color(0.7f, 0.9f, 0.85f), new Vector2(-380f, 232f), new Vector2(580f, 40f));

            var lines = new[]
            {
                "Time played:  " + minutes + " min",
                "Stars:  " + ProgressStore.TotalStars + " / " + (GameFlow.LastLevel * 3),
                "Levels finished:  " + ProgressStore.LevelsCompleted + " / " + GameFlow.LastLevel,
                "Play streak:  " + Mathf.Max(1, ProgressStore.Streak) + " day" + (ProgressStore.Streak > 1 ? "s" : string.Empty),
            };
            for (var i = 0; i < lines.Length; i++)
                RuntimeUi.Text(canvas.transform, "Stat " + i, lines[i], 44f, Color.white, new Vector2(380f, 360f - i * 62f), new Vector2(640f, 56f), TextAlignmentOptions.Left);

            RuntimeUi.Text(canvas.transform, "BadgesTitle", "Badges", 44f, new Color(1f, 0.9f, 0.5f), new Vector2(0f, 150f), new Vector2(600f, 56f));
            var badges = ProgressStore.AllBadges;
            for (var i = 0; i < badges.Count; i++)
            {
                var col = i % 4;
                var row = i / 4;
                var pos = new Vector2((col - 1.5f) * 350f, 60f - row * 150f);
                var earned = ProgressStore.Has(badges[i].id);
                var box = RuntimeUi.Panel(canvas.transform, "Badge " + i, earned ? new Color(0.35f, 0.3f, 0.08f, 0.95f) : new Color(1f, 1f, 1f, 0.08f), pos, new Vector2(330f, 130f));
                box.raycastTarget = false;
                RuntimeUi.Panel(canvas.transform, "BadgeStar " + i, earned ? new Color(1f, 0.85f, 0.25f) : new Color(1f, 1f, 1f, 0.2f), pos + new Vector2(-120f, 0f), new Vector2(64f, 64f), RuntimeUi.Star).raycastTarget = false;
                RuntimeUi.Text(canvas.transform, "BadgeName " + i, badges[i].name, 32f, earned ? Color.white : new Color(1f, 1f, 1f, 0.55f), pos + new Vector2(38f, 22f), new Vector2(230f, 60f));
                RuntimeUi.Text(canvas.transform, "BadgeDesc " + i, badges[i].description, 22f, earned ? new Color(1f, 0.95f, 0.7f) : new Color(1f, 1f, 1f, 0.4f), pos + new Vector2(38f, -30f), new Vector2(230f, 56f));
            }

            var pending = telemetry != null ? telemetry.PendingEvents : 0;
            RuntimeUi.Text(canvas.transform, "Sync", pending == 0 ? "Everything is saved to the dashboard." : "Saved on this headset - will send to the dashboard when online (" + pending + ").",
                26f, new Color(0.65f, 0.85f, 0.8f), new Vector2(0f, -235f), new Vector2(1300f, 40f));

            var voice = RuntimeUi.MakeButton(canvas.transform, string.Empty, new Vector2(-260f, -320f), new Vector2(430f, 84f), new Color(0.3f, 0.7f, 0.6f), Color.white, ToggleVoice, 40f);
            m_VoiceLabel = voice.GetComponentInChildren<TMP_Text>();
            var music = RuntimeUi.MakeButton(canvas.transform, string.Empty, new Vector2(260f, -320f), new Vector2(430f, 84f), new Color(0.3f, 0.7f, 0.6f), Color.white, ToggleMusic, 40f);
            m_MusicLabel = music.GetComponentInChildren<TMP_Text>();
            RefreshToggles();

            RuntimeUi.MakeButton(canvas.transform, "Back", new Vector2(0f, -430f), new Vector2(360f, 84f), new Color(1f, 1f, 1f, 0.92f), new Color(0.08f, 0.1f, 0.12f), BackToMenu, 44f);
            return anchor;
        }

        void ToggleVoice()
        {
            VoiceOver.Enabled = !VoiceOver.Enabled;
            RefreshToggles();
        }

        void ToggleMusic()
        {
            MusicPlayer.Enabled = !MusicPlayer.Enabled;
            RefreshToggles();
        }

        void RefreshToggles()
        {
            if (m_VoiceLabel != null)
                m_VoiceLabel.text = "Voice: " + (VoiceOver.Enabled ? "ON" : "OFF");
            if (m_MusicLabel != null)
                m_MusicLabel.text = "Music: " + (MusicPlayer.Enabled ? "ON" : "OFF");
        }
    }
}
