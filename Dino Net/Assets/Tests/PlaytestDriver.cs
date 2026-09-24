using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DinoNet.Playtest
{
    /// <summary>
    /// Plays a level the way a child would - start the run, answer the dinosaur's questions, carry
    /// the packet to each node - and writes down what actually happened. Created at runtime from a
    /// request file by the editor task runner, so no scene ever has to store it.
    /// </summary>
    public class PlaytestDriver : MonoBehaviour
    {
        const string k_RequestPath = "DinoNetTasks/playtest.txt";

        public string script;
        public string resultPath;

        readonly StringBuilder m_Log = new StringBuilder();
        int m_Failures;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!Application.isEditor || !File.Exists(k_RequestPath))
                return;

            var lines = File.ReadAllLines(k_RequestPath);
            File.Delete(k_RequestPath);
            if (lines.Length == 0)
                return;

            var holder = new GameObject("Playtest Driver");
            var driver = holder.AddComponent<PlaytestDriver>();
            driver.script = lines[0].Trim();
            driver.resultPath = lines.Length > 1 ? lines[1].Trim() : "DinoNetTasks/result.txt";
        }

        IEnumerator Start()
        {
            yield return null;
            yield return null;

            if (script.StartsWith("level"))
                yield return RunLevel();
            else if (script == "tutorial")
                yield return RunTutorial();
            else if (script == "mainmenu")
                yield return RunMainMenu();
            else if (script == "buttons")
                yield return RunButtons();
            else if (script == "danger")
                yield return RunDanger();
            else
                Note("ERROR unknown script " + script);

            Finish();
        }

        // ---------------------------------------------------------------- levels

        IEnumerator RunLevel()
        {
            var quest = FindFirstObjectByType<DinoQuestManager>();
            var level = FindFirstObjectByType<LevelManager>();
            var director = FindFirstObjectByType<LessonDirector>();
            var panel = FindFirstObjectByType<DecisionPanel>();
            var hud = FindFirstObjectByType<LevelHud>();
            var firefly = FindFirstObjectByType<GuideFirefly>();

            Check("quest present", quest != null);
            Check("level manager present", level != null);
            Check("lesson director present", director != null);
            Check("decision panel present", panel != null);
            Check("firefly kept", firefly != null);
            if (quest == null || level == null || director == null || panel == null)
                yield break;

            Note("level " + level.LevelNumber + ": nodes=" + quest.RouteCount + " lessons=" + director.LessonCount);
            Check("node count matches level+1", quest.RouteCount == level.LevelNumber + 1);
            Check("level has at least one lesson", director.LessonCount >= 1);
            Note("concept chip: " + Text("Level HUD/Concept/Title"));
            Note("fun fact: " + Text("Level Complete Panel/Level Complete Panel Canvas/Fun Fact/Fact"));
            Check("fun fact text present", !string.IsNullOrEmpty(Text("Level Complete Panel/Level Complete Panel Canvas/Fun Fact/Fact")));

            var npcs = FindObjectsByType<RoadWanderer>(FindObjectsSortMode.None).Length;
            Note("moving NPC dinosaurs: " + npcs);
            Check("NPCs still wander", npcs > 0);

            var orb = quest.Orb;
            Check("packet tag present", orb != null && orb.GetComponentInChildren<PacketLabel>(true) != null);

            quest.StartQuest();
            yield return new WaitForSeconds(0.4f);
            Check("quest running", quest.IsRunning);

            // Let the test move the packet directly; the component would otherwise hold it docked.
            if (orb != null)
                orb.enabled = false;

            var connectionsBefore = FindObjectsByType<NetworkConnection>(FindObjectsSortMode.None).Length;
            Check("no connections before any arrival", connectionsBefore == 0);

            var deliveries = 0;
            var lessonsSeen = 0;
            var guard = 0;

            while (deliveries < quest.RouteCount && guard++ < 40)
            {
                // A question may be due either at the start or after the arrival just made.
                if (director.Pending != null)
                {
                    yield return AnswerDecision(director, panel, quest, level, lessonsSeen == 0);
                    lessonsSeen++;
                    continue;
                }

                var target = quest.CurrentTarget;
                if (target == null)
                    break;

                if (orb != null)
                    orb.transform.position = target.DeliveryAnchor.position + Vector3.up * 0.6f;

                yield return new WaitForSeconds(0.5f);

                if (quest.ConnectedCount > deliveries)
                {
                    deliveries = quest.ConnectedCount;
                    Note("delivered to node " + deliveries + "/" + quest.RouteCount
                         + " connections=" + FindObjectsByType<NetworkConnection>(FindObjectsSortMode.None).Length);

                    // Give the director its pause before the next question appears.
                    yield return new WaitForSeconds(2.2f);
                }
            }

            Check("all lessons answered", director.LessonsPassed == director.LessonCount);
            Check("every node connected", quest.ConnectedCount == quest.RouteCount);
            Check("one connection per hop",
                FindObjectsByType<NetworkConnection>(FindObjectsSortMode.None).Length == quest.RouteCount);
            Check("level complete", level.State == LevelManager.LevelState.Complete);
            Note("concept result chip: " + Text("Level HUD/Concept/Result"));

            yield return new WaitForSeconds(0.6f);
            var completePanel = Find("Level Complete Panel/Level Complete Panel Canvas");
            Check("completion panel shown", completePanel != null && completePanel.activeInHierarchy);

            // The HUD sits closer to the eye than the panel, so it has to get out of the way.
            var hudRoot = Find("Level HUD");
            Check("HUD stands aside for the completion panel", hudRoot != null && !hudRoot.activeInHierarchy);

            // The fun fact animation has to actually move, or it is just a static picture.
            var token = Find("Level Complete Panel/Level Complete Panel Canvas/Fun Fact/Strip/Token");
            if (token != null)
            {
                var start = token.GetComponent<RectTransform>().anchoredPosition;
                yield return new WaitForSeconds(1.2f);
                var moved = Vector2.Distance(start, token.GetComponent<RectTransform>().anchoredPosition);
                Check("fun fact animation runs (moved " + moved.ToString("0.0") + ")", moved > 1f);
            }
            else
            {
                Check("fun fact animation present", false);
            }

            foreach (var label in new[] { "Next Level Button", "Play Again Button", "Return to Main Menu Button" })
            {
                var button = Find("Level Complete Panel/Level Complete Panel Canvas/" + label);
                var kept = button != null && button.GetComponent<Button>() != null
                           && button.GetComponent<Button>().onClick.GetPersistentEventCount() > 0;
                Check("kept " + label, kept);
            }
        }

        /// <summary>
        /// Answers one decision: checks the question really blocks the route, deliberately gets it
        /// wrong once to confirm the explanation and the free retry, then chooses safely.
        /// </summary>
        IEnumerator AnswerDecision(LessonDirector director, DecisionPanel panel, DinoQuestManager quest,
            LevelManager level, bool deepChecks)
        {
            var lesson = director.Pending;
            Note("--- decision: " + lesson.conceptTerm + " | " + lesson.speaker + " " + lesson.prompt);

            Check("delivery gated while a question is pending", quest.DeliveryPaused);
            Check("countdown held while a question is pending", level.IsPaused);

            var waited = 0f;
            while (!panel.IsOpen && waited < 6f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Check("decision panel opened", panel.IsOpen);
            if (!panel.IsOpen)
                yield break;

            var hud = Find("Level HUD");
            Check("HUD stands aside for the question", hud != null && !hud.activeInHierarchy);

            Note("option A: " + Text("Decision Panel/Decision Panel Canvas/Option A/Label")
                 + " - " + Text("Decision Panel/Decision Panel Canvas/Option A/Sublabel"));
            Note("option B: " + Text("Decision Panel/Decision Panel Canvas/Option B/Label")
                 + " - " + Text("Decision Panel/Decision Panel Canvas/Option B/Sublabel"));

            if (deepChecks)
            {
                // The clock must not tick down while a child is reading the question.
                var before = level.TimeRemaining;
                yield return new WaitForSeconds(1f);
                Check("timer did not drop during the question (" + (before - level.TimeRemaining).ToString("0.000") + "s)",
                    Mathf.Abs(before - level.TimeRemaining) < 0.01f);

                // Running to the node early must not skip the question.
                var target = quest.CurrentTarget;
                if (target != null && quest.Orb != null)
                {
                    quest.Orb.transform.position = target.DeliveryAnchor.position + Vector3.up * 0.6f;
                    yield return new WaitForSeconds(0.4f);
                    Check("reaching the node early does not skip the question", quest.ConnectedCount == 0);
                }
            }

            var wrong = IndexOf(lesson, false);
            var right = IndexOf(lesson, true);
            Check("lesson has a safe answer", right >= 0);
            if (right < 0)
                yield break;

            var progressBefore = quest.ConnectedCount;

            if (wrong >= 0)
            {
                // Press the real button, so the wiring is exercised rather than the API.
                Click(wrong);
                yield return new WaitForSeconds(0.3f);

                Note("wrong-choice feedback: " + Text("Decision Panel/Decision Panel Canvas/Feedback"));
                Check("wrong choice explains itself",
                    !string.IsNullOrEmpty(Text("Decision Panel/Decision Panel Canvas/Feedback")));
                Check("wrong choice does not close the question", panel.IsOpen);
                Check("wrong choice costs no progress", quest.ConnectedCount == progressBefore);
                Check("wrong choice does not fail the level", level.State == LevelManager.LevelState.Running);

                // The child must be able to try again.
                yield return new WaitForSeconds(2f);
                Check("question can be answered again", panel.IsOpen);
            }

            Click(right);
            yield return new WaitForSeconds(0.3f);
            Note("correct feedback: " + Text("Decision Panel/Decision Panel Canvas/Feedback"));

            var closing = 0f;
            while (panel.IsOpen && closing < 6f)
            {
                closing += Time.deltaTime;
                yield return null;
            }

            Check("question closes after the safe choice", !panel.IsOpen);
            Check("HUD comes back once the question is answered", hud != null && hud.activeInHierarchy);

            // The packet may already be standing on the next node, in which case it is delivered
            // the instant the gate lifts and the following question closes it again. Either the
            // route is open or the next question is already queued - never stuck with neither.
            var queued = director.Pending != null;
            Check("route continues after the safe choice" + (queued ? " (next question queued)" : ""),
                !quest.DeliveryPaused || queued);
            Check("countdown resumes" + (queued ? " (held for the next question)" : ""),
                !level.IsPaused || queued);
            Note("packet tag now reads: " + Text2(quest.Orb, "Packet Tag/Text"));
            Check("packet tag shows the choice", Text2(quest.Orb, "Packet Tag/Text") == lesson.packetLabel);
            Check("concept chip records the term", Text("Level HUD/Concept/Result") == lesson.conceptResult);
        }

        static int IndexOf(NetworkLesson lesson, bool correct)
        {
            for (var i = 0; i < lesson.options.Length; i++)
            {
                if (lesson.options[i] != null && lesson.options[i].correct == correct)
                    return i;
            }

            return -1;
        }

        void Click(int optionIndex)
        {
            var name = optionIndex == 0 ? "Option A" : "Option B";
            var card = Find("Decision Panel/Decision Panel Canvas/" + name);
            var button = card != null ? card.GetComponent<Button>() : null;
            if (button == null)
            {
                Check("found " + name + " button", false);
                return;
            }

            button.onClick.Invoke();
        }

        // ---------------------------------------------------------------- other scenes

        IEnumerator RunTutorial()
        {
            var quest = FindFirstObjectByType<DinoQuestManager>();
            var tutorial = FindFirstObjectByType<TutorialDirector>();
            var lessons = FindFirstObjectByType<LessonDirector>();

            Check("tutorial director present", tutorial != null);
            Check("tutorial keeps its own script, no level lessons", lessons == null);
            Check("tutorial quest present", quest != null);
            Check("tutorial has 2 nodes", quest != null && quest.RouteCount == 2);
            yield return new WaitForSeconds(1f);
            Note("tutorial line: " + Text("Tutorial Panel/Text"));
            Check("tutorial is speaking", !string.IsNullOrEmpty(Text("Tutorial Panel/Text")));
        }

        IEnumerator RunMainMenu()
        {
            yield return new WaitForSeconds(0.6f);
            var menu = FindFirstObjectByType<MainMenuController>();
            Check("menu controller present", menu != null);

            var panel = Find("Main Menu Panel");
            Check("menu panel present", panel != null);

            if (panel != null && Camera.main != null)
            {
                var distance = Vector3.Distance(panel.transform.position, Camera.main.transform.position);
                Note("menu panel is " + distance.ToString("0.00") + "m from the headset");
                Check("menu panel is in front of the player", distance < 4f);
            }
        }

        /// <summary>
        /// Standing near a volcano must still burn the clock faster now that questions can hold it.
        /// </summary>
        IEnumerator RunDanger()
        {
            var quest = FindFirstObjectByType<DinoQuestManager>();
            var level = FindFirstObjectByType<LevelManager>();
            var director = FindFirstObjectByType<LessonDirector>();
            var panel = FindFirstObjectByType<DecisionPanel>();
            if (quest == null || level == null)
                yield break;

            quest.StartQuest();
            yield return new WaitForSeconds(0.5f);

            // Clear the opening question first, so the clock is actually running.
            if (director != null && director.Pending != null)
            {
                var waited = 0f;
                while (!panel.IsOpen && waited < 6f)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }

                var right = IndexOf(director.Pending, true);
                Click(right);

                var closing = 0f;
                while (panel.IsOpen && closing < 8f)
                {
                    closing += Time.deltaTime;
                    yield return null;
                }
            }

            Check("countdown running again", !level.IsPaused);

            var volcano = null as DangerZone;
            foreach (var zone in FindObjectsByType<DangerZone>(FindObjectsSortMode.None))
            {
                if (zone.name.IndexOf("Volcano", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    volcano = zone;
                    break;
                }
            }

            Check("volcano hazard present", volcano != null);
            if (volcano == null || Camera.main == null)
                yield break;

            var rig = Camera.main.transform.root;
            var safeStart = level.TimeRemaining;
            yield return new WaitForSeconds(2f);
            var safeUsed = safeStart - level.TimeRemaining;

            rig.position = volcano.transform.position;
            yield return new WaitForSeconds(0.6f);
            Check("player is detected inside the volcano zone", level.InDanger);

            var dangerStart = level.TimeRemaining;
            yield return new WaitForSeconds(2f);
            var dangerUsed = dangerStart - level.TimeRemaining;

            var ratio = safeUsed > 0.01f ? dangerUsed / safeUsed : 0f;
            Note("clock used " + safeUsed.ToString("0.00") + "s when safe, "
                 + dangerUsed.ToString("0.00") + "s near the volcano (" + ratio.ToString("0.00") + "x)");
            Check("volcano drains the clock faster", ratio > 2f);
        }

        IEnumerator RunButtons()
        {
            yield return new WaitForSeconds(0.4f);
            var level = FindFirstObjectByType<LevelManager>();
            Check("level manager present", level != null);
            if (level == null)
                yield break;

            level.Fail("Playtest");
            yield return new WaitForSeconds(0.5f);

            var failed = Find("Level Failed Panel/Level Failed Panel Canvas");
            Check("failure panel shown", failed != null && failed.activeInHierarchy);

            foreach (var label in new[] { "Try Again Button", "Return to Main Menu Button" })
            {
                var button = Find("Level Failed Panel/Level Failed Panel Canvas/" + label);
                var bound = button != null && button.GetComponent<Button>() != null
                            && button.GetComponent<Button>().onClick.GetPersistentEventCount() > 0;
                Check("failure panel " + label + " bound", bound);
            }

            Note("failure reason: " + Text("Level Failed Panel/Level Failed Panel Canvas/Reason"));
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Finds an object by a slash path, including inactive ones. The first name is matched
        /// anywhere in the scene, because panels hang off the camera until they are anchored.
        /// </summary>
        static GameObject Find(string path)
        {
            var parts = path.Split('/');
            GameObject current = null;

            foreach (var transform in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform.name == parts[0])
                {
                    current = transform.gameObject;
                    break;
                }
            }

            for (var i = 1; i < parts.Length && current != null; i++)
            {
                var child = current.transform.Find(parts[i]);
                current = child != null ? child.gameObject : null;
            }

            return current;
        }

        static string Text(string path)
        {
            var go = Find(path);
            if (go == null)
                return string.Empty;

            var text = go.GetComponent<TMP_Text>();
            return text != null ? text.text : string.Empty;
        }

        /// <summary>Text lookup relative to a component, for things parented to the moving packet.</summary>
        static string Text2(Component root, string path)
        {
            if (root == null)
                return string.Empty;

            var child = root.transform.Find(path);
            if (child == null)
                return string.Empty;

            var text = child.GetComponent<TMP_Text>();
            return text != null ? text.text : string.Empty;
        }

        void Check(string label, bool passed)
        {
            if (!passed)
                m_Failures++;

            m_Log.AppendLine((passed ? "PASS  " : "FAIL  ") + label);
        }

        void Note(string line) => m_Log.AppendLine("      " + line);

        void Finish()
        {
            m_Log.AppendLine();
            m_Log.AppendLine(m_Failures == 0 ? "ALL CHECKS PASSED" : m_Failures + " CHECK(S) FAILED");

            var path = string.IsNullOrEmpty(resultPath) ? "DinoNetTasks/result.txt" : resultPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "playtest " + script + "\n\n" + m_Log);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }
}
