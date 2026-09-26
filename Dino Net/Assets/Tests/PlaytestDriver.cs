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

            // Timing checks are meaningless with the narration switched off, so turn it on for the
            // run and hand the setting back exactly as it was.
            var voiceWas = VoiceOver.Enabled;
            VoiceOver.Enabled = true;

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
            else if (script == "packet")
                yield return RunPacket();
            else if (script == "ambience")
                yield return RunAmbience();
            else if (script == "narration")
                yield return RunNarration();
            else if (script == "pause")
                yield return RunPause();
            else if (script == "skip")
                yield return RunSkip();
            else
                Note("ERROR unknown script " + script);

            VoiceOver.Enabled = voiceWas;
            Note("voice-over setting restored to " + (voiceWas ? "on" : "off"));
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
            // Levels 1-4 grow by one node each; levels 5+ use fixed routes of 3-7 nodes.
            Check("node count fits level",
                level.LevelNumber <= 4 ? quest.RouteCount == level.LevelNumber + 1 : quest.RouteCount >= 3 && quest.RouteCount <= 7);
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

                // The child must be able to try again once the explanation has finished.
                var waitingForRetry = 0f;
                while (waitingForRetry < 15f && !CanAnswer())
                {
                    waitingForRetry += Time.deltaTime;
                    yield return null;
                }

                Note("buttons came back after " + waitingForRetry.ToString("0.00") + "s");
                Check("question can be answered again", panel.IsOpen && CanAnswer());
            }

            Click(right);
            var feedbackShown = Time.time;
            yield return new WaitForSeconds(0.3f);
            Note("correct feedback: " + Text("Decision Panel/Decision Panel Canvas/Feedback"));

            var praise = Resources.Load<AudioClip>("VO/" + VoiceOver.Slug(lesson.correctFeedback));
            var closing = 0f;
            while (panel.IsOpen && closing < 15f)
            {
                closing += Time.deltaTime;
                yield return null;
            }

            Check("question closes after the safe choice", !panel.IsOpen);

            if (praise != null)
            {
                var held = Time.time - feedbackShown;
                Note("well-done message held " + held.ToString("0.00") + "s for a "
                     + praise.length.ToString("0.00") + "s recording");
                Check("well-done message is not cut off", held >= praise.length - 0.1f);
            }
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

        /// <summary>True once at least one choice card is clickable again.</summary>
        static bool CanAnswer()
        {
            foreach (var name in new[] { "Option A", "Option B" })
            {
                var card = Find("Decision Panel/Decision Panel Canvas/" + name);
                var button = card != null ? card.GetComponent<Button>() : null;
                if (button != null && button.interactable)
                    return true;
            }

            return false;
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

        /// <summary>
        /// Letting the packet go - on purpose, to point at a question - must leave it on the
        /// ground within reach, not bouncing away or vanishing back to the podium.
        /// </summary>
        IEnumerator RunPacket()
        {
            var quest = FindFirstObjectByType<DinoQuestManager>();
            Check("quest present", quest != null);
            if (quest == null || quest.Orb == null || Camera.main == null)
                yield break;

            var orb = quest.Orb;
            var grab = orb.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            var head = Camera.main.transform;
            var podium = orb.transform.position;

            quest.StartQuest();
            yield return new WaitForSeconds(0.4f);

            // Pick it up, carry it off, then let go somewhere awkward: high up and far away.
            grab.selectEntered.Invoke(new UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs());
            yield return null;
            orb.transform.position = head.position + head.forward * 14f + Vector3.up * 5f;
            grab.selectExited.Invoke(new UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs());

            yield return new WaitForSeconds(3f);

            var resting = orb.transform.position;
            var flat = resting - head.position;
            flat.y = 0f;
            var floor = head.position.y - 1.4f;
            Note("packet came to rest " + flat.magnitude.ToString("0.00") + "m from the child, "
                 + (resting.y - floor).ToString("0.00") + "m off the floor");

            Check("packet is within reach after being let go", flat.magnitude <= 3f);
            Check("packet did not sink through the floor", resting.y > floor - 0.4f);
            Check("packet did not vanish back to the podium", Vector3.Distance(resting, podium) > 1f);
            Check("packet reports itself as resting", orb.IsResting);

            // It has to stay put, so a child can walk back to where they saw it.
            yield return new WaitForSeconds(2f);
            var drift = Vector3.Distance(resting, orb.transform.position);
            Note("packet drifted " + drift.ToString("0.00") + "m while waiting");
            Check("packet stays where it landed", drift < 0.5f);

            Check("packet can still be picked up", grab.isActiveAndEnabled);

            grab.selectEntered.Invoke(new UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs());
            yield return null;
            Check("picking it back up releases it from the ground", !orb.IsResting);
        }

        /// <summary>The jungle bed must not be looping in the child's ears from the first second.</summary>
        IEnumerator RunAmbience()
        {
            AudioSource ambience = null;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (source.GetComponent<AmbienceCycler>() != null)
                {
                    ambience = source;
                    break;
                }
            }

            Check("ambience is on a cycler", ambience != null);
            if (ambience == null)
                yield break;

            Check("ambience does not play on load", !ambience.playOnAwake);

            var loudest = 0f;
            var everPlaying = false;
            var elapsed = 0f;
            while (elapsed < 8f)
            {
                elapsed += Time.deltaTime;
                loudest = Mathf.Max(loudest, ambience.isPlaying ? ambience.volume : 0f);
                everPlaying |= ambience.isPlaying;
                yield return null;
            }

            Note("over the first 8s the jungle bed was " + (everPlaying ? "playing" : "silent")
                 + ", peak volume " + loudest.ToString("0.000"));
            Check("no jungle loop in the child's ears at the start", loudest < 0.01f);

            // Count every roar and grunt over a full minute. One per dinosaur used to mean one
            // every couple of seconds; they now share a single level-wide budget.
            var heard = new HashSet<int>();
            var vocals = 0;
            var footsteps = 0;
            var window = 0f;
            while (window < 60f)
            {
                window += Time.deltaTime;
                foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                {
                    if (!source.gameObject.name.StartsWith("Sfx ") || !heard.Add(source.GetInstanceID()))
                        continue;

                    if (source.gameObject.name.Contains("footstep"))
                        footsteps++;
                    else
                        vocals++;
                }

                yield return null;
            }

            Note("in 60s: " + vocals + " dinosaur roars/grunts, " + footsteps + " footsteps");
            Check("dinosaur noises are occasional, not constant", vocals <= 2);

            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!source.isPlaying || source.volume <= 0.01f)
                    continue;

                Note("audible: " + source.gameObject.name
                     + "  clip=" + (source.clip != null ? source.clip.name : "none")
                     + "  vol=" + source.volume.ToString("0.00")
                     + "  loop=" + source.loop
                     + "  blend=" + source.spatialBlend.ToString("0.0"));
            }

            foreach (var zone in FindObjectsByType<DangerZone>(FindObjectsSortMode.None))
                Note("danger zone " + zone.gameObject.name + " playerInside=" + zone.PlayerInside);
        }

        /// <summary>Each tutorial line must stay up until its voice-over has finished.</summary>
        IEnumerator RunNarration()
        {
            var quest = FindFirstObjectByType<DinoQuestManager>();
            var tutorial = FindFirstObjectByType<TutorialDirector>();
            Check("tutorial director present", tutorial != null);
            if (tutorial == null)
                yield break;

            var previous = string.Empty;
            var shownFor = 0f;
            var measured = 0;
            var cut = 0;
            var elapsed = 0f;
            var started = false;
            var partial = true;

            while (elapsed < 70f && measured < 6)
            {
                elapsed += Time.deltaTime;
                shownFor += Time.deltaTime;

                // Get past the gates, so more than the opening lines are measured.
                if (!started && elapsed > 8f && quest != null)
                {
                    quest.StartQuest();
                    var grab = quest.Orb != null
                        ? quest.Orb.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>()
                        : null;
                    if (grab != null)
                        grab.selectEntered.Invoke(new UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs());

                    started = true;
                }

                var line = Text("Tutorial Panel/Text");
                if (line == previous || string.IsNullOrEmpty(line))
                {
                    yield return null;
                    continue;
                }

                if (!string.IsNullOrEmpty(previous) && partial)
                {
                    // Only part of this one was watched; it proves nothing either way.
                    Note("skipped (already on screen at start): " + previous);
                    partial = false;
                }
                else if (!string.IsNullOrEmpty(previous))
                {
                    var clip = Resources.Load<AudioClip>("VO/" + VoiceOver.Slug(previous));
                    var spoken = clip != null ? clip.length : 0f;
                    measured++;

                    var label = shownFor.ToString("0.00") + "s on screen for a "
                                + spoken.ToString("0.00") + "s line: " + previous;
                    if (spoken > 0f && shownFor < spoken - 0.1f)
                    {
                        cut++;
                        Note("CUT OFF - " + label);
                    }
                    else
                    {
                        Note("held " + label);
                    }
                }

                previous = line;
                shownFor = 0f;
                yield return null;
            }

            Note("lines measured: " + measured);
            Check("narration was measured", measured >= 3);
            Check("no voice-over was cut off mid-line", cut == 0);
        }

        /// <summary>
        /// The pause button has to sit low in the corner of the view, follow the head, and give a
        /// way out of a level that is neither finishing it nor failing it.
        /// </summary>
        IEnumerator RunPause()
        {
            var menu = FindFirstObjectByType<PauseMenu>();
            var level = FindFirstObjectByType<LevelManager>();
            var quest = FindFirstObjectByType<DinoQuestManager>();
            Check("pause menu present", menu != null);
            if (menu == null || Camera.main == null)
                yield break;

            var head = Camera.main.transform;
            var button = Find("Pause Menu/Pause Button");
            Check("pause button present", button != null);
            if (button == null)
                yield break;

            yield return new WaitForSeconds(0.6f);
            Check("pause button is visible from the start", button.activeInHierarchy);

            var toButton = button.transform.position - head.position;
            var flat = new Vector3(toButton.x, 0f, toButton.z);
            Note("pause button sits " + flat.magnitude.ToString("0.00") + "m ahead, "
                 + toButton.y.ToString("0.00") + "m below eye level");
            Check("pause button is within reach", flat.magnitude < 1.6f);
            Check("pause button is down near the floor", toButton.y < -0.4f);

            var side = Vector3.Dot(toButton, head.right);
            Note("pause button is off to the " + (side > 0f ? "right" : "left") + " by " + Mathf.Abs(side).ToString("0.00") + "m");
            Check("pause button is off to one side, not dead ahead", Mathf.Abs(side) > 0.25f);

            // Look somewhere else; it should come with us.
            var before = button.transform.position;
            head.root.Rotate(0f, 90f, 0f);
            yield return new WaitForSeconds(1.2f);
            var moved = Vector3.Distance(before, button.transform.position);
            var stillSide = Vector3.Dot(button.transform.position - head.position, head.right);
            Note("after turning 90 degrees the button moved " + moved.ToString("0.00") + "m and is still "
                 + Mathf.Abs(stillSide).ToString("0.00") + "m to the side");
            Check("pause button follows where the child looks", moved > 0.3f);
            Check("pause button stays in the same corner", Mathf.Abs(stillSide) > 0.25f);

            // Now pause for real.
            if (quest != null)
                quest.StartQuest();

            yield return new WaitForSeconds(0.5f);
            var clockBefore = level != null ? level.TimeRemaining : 0f;

            ClickAt("Pause Menu/Pause Button/Pause Button Canvas/Button");
            yield return new WaitForSecondsRealtime(0.4f);

            Check("game reports itself paused", menu.IsPaused);
            Check("time is frozen", Mathf.Approximately(Time.timeScale, 0f));
            var panel = Find("Pause Panel/Pause Panel Canvas");
            Check("pause menu is shown", panel != null && panel.activeInHierarchy);
            Check("pause button gets out of the way", !button.activeInHierarchy);

            var hud = Find("Level HUD");
            Check("HUD stands aside for the pause menu", hud != null && !hud.activeInHierarchy);

            // Level 1 asks its question as soon as the run starts, so this also covers pausing
            // mid-question: the two panels must not stack on top of each other.
            var question = FindFirstObjectByType<DecisionPanel>();
            var questionCanvas = Find("Decision Panel/Decision Panel Canvas");
            var hadQuestion = question != null && questionCanvas != null;
            if (hadQuestion)
                Check("a question steps aside for the pause menu", !questionCanvas.activeInHierarchy);

            yield return new WaitForSecondsRealtime(1.5f);
            var timed = level != null && level.UsesTimer;
            if (timed)
            {
                Note("countdown moved " + Mathf.Abs(clockBefore - level.TimeRemaining).ToString("0.000")
                     + "s while paused");
                Check("countdown does not run while paused", Mathf.Abs(clockBefore - level.TimeRemaining) < 0.01f);
            }
            else
            {
                Note("this scene runs without a countdown, so there is none to freeze");
            }

            // The way back to the main menu has to be wired, but do not actually load it - that
            // would destroy this driver before it can write its results.
            var exit = Find("Pause Panel/Pause Panel Canvas/Return to Main Menu Button");
            var exitButton = exit != null ? exit.GetComponent<Button>() : null;
            var wired = exitButton != null && exitButton.onClick.GetPersistentEventCount() > 0
                        && exitButton.onClick.GetPersistentMethodName(0) == "ReturnToMainMenu";
            Check("Return to Main Menu is wired up", wired);

            ClickAt("Pause Panel/Pause Panel Canvas/Return to Game Button");
            yield return new WaitForSecondsRealtime(0.4f);

            Check("game resumes", !menu.IsPaused);
            Check("time runs again", Mathf.Approximately(Time.timeScale, 1f));
            Check("pause menu closes", panel == null || !panel.activeInHierarchy);
            Check("pause button comes back", button.activeInHierarchy);

            if (hadQuestion)
            {
                // The question owns the HUD until it is answered, so the HUD stays down and the
                // question comes back instead.
                Check("the question comes back after resuming", questionCanvas.activeInHierarchy);
                Check("HUD stays down while the question is still up", !hud.activeInHierarchy);
            }
            else
            {
                Check("HUD comes back", hud != null && hud.activeInHierarchy);
            }

            if (timed)
            {
                var running = level.TimeRemaining;
                yield return new WaitForSeconds(1f);
                Check("countdown runs again after resuming", running - level.TimeRemaining > 0.5f);
            }
        }

        /// <summary>A child who already understands can tap past the spoken explanation.</summary>
        IEnumerator RunSkip()
        {
            var quest = FindFirstObjectByType<DinoQuestManager>();
            var director = FindFirstObjectByType<LessonDirector>();
            var panel = FindFirstObjectByType<DecisionPanel>();
            Check("lesson director present", director != null);
            Check("decision panel present", panel != null);
            if (quest == null || director == null || panel == null)
                yield break;

            quest.StartQuest();

            var waited = 0f;
            while (!panel.IsOpen && waited < 12f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            Check("a question was asked", panel.IsOpen);
            if (!panel.IsOpen || director.Pending == null)
                yield break;

            var lesson = director.Pending;
            var wrong = IndexOf(lesson, false);
            if (wrong < 0)
                yield break;

            var clip = Resources.Load<AudioClip>("VO/" + VoiceOver.Slug(lesson.options[wrong].wrongFeedback));
            var spoken = clip != null ? clip.length : 0f;

            Click(wrong);
            var clicked = Time.time;

            var skip = Find("Decision Panel/Decision Panel Canvas/Got It Button");
            Check("skip button exists", skip != null);
            if (skip == null)
                yield break;

            var appearing = 0f;
            while (!skip.activeInHierarchy && appearing < 6f)
            {
                appearing += Time.deltaTime;
                yield return null;
            }

            Check("skip button appears while the message is read out", skip.activeInHierarchy);
            Note("skip button appeared after " + (Time.time - clicked).ToString("0.00") + "s");

            skip.GetComponent<Button>().onClick.Invoke();

            var back = 0f;
            while (!CanAnswer() && back < 8f)
            {
                back += Time.deltaTime;
                yield return null;
            }

            var total = Time.time - clicked;
            Note("tapping skip got back to the question in " + total.ToString("0.00")
                 + "s, against a " + spoken.ToString("0.00") + "s recording");
            Check("skip returns to the question", CanAnswer());
            Check("skip is quicker than sitting through the message", spoken < 0.1f || total < spoken);
            Check("skip stops the voice", !VoiceOver.IsSpeaking);
            Check("skip button hides itself again", !skip.activeInHierarchy);
        }

        /// <summary>Presses a button found by path.</summary>
        void ClickAt(string path)
        {
            var go = Find(path);
            var button = go != null ? go.GetComponent<Button>() : null;
            if (button == null)
            {
                Check("found button " + path, false);
                return;
            }

            button.onClick.Invoke();
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
