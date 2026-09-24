using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using DinoNet;

namespace DinoNetEditor
{
    /// <summary>
    /// Layers each level's networking lesson onto the delivery gameplay that already exists: the
    /// decision panel a dinosaur asks its question on, the tag floating above the packet, the HUD
    /// concept chip, and the "Did You Know?" reward on the completion panel. Nothing here rebuilds
    /// the quest, the nodes, the vines or the firefly - it only gates and narrates them.
    /// </summary>
    public static class LessonContentBuilder
    {
        const string k_LessonFolder = "Assets/Lessons/";

        static readonly Color k_Safe = new Color(0.35f, 1f, 0.55f);
        static readonly Color k_Caution = new Color(1f, 0.72f, 0.25f);
        static readonly Color k_Ask = new Color(0.45f, 0.82f, 1f);
        static readonly Color k_Send = new Color(0.78f, 0.62f, 1f);

        /// <summary>One icon plus caption in a fun-fact animation.</summary>
        class Cell
        {
            public string icon;
            public string caption;

            public Cell(string icon, string caption)
            {
                this.icon = icon;
                this.caption = caption;
            }
        }

        class Theme
        {
            public string conceptTitle;
            public string funFact;
            public Cell[] strip;
            public string intro;
        }

        static readonly Dictionary<int, Theme> k_Themes = new Dictionary<int, Theme>
        {
            { 1, new Theme {
                conceptTitle = "Level 1: Safe Connections   -   HTTP vs HTTPS",
                funFact = "The little lock in your browser can show that you are using a secure connection.",
                strip = new[] { new Cell("Icon_Dino", "Dino"), new Cell("Icon_Lock", "HTTPS"), new Cell("Icon_Server", "Website") } } },

            { 2, new Theme {
                conceptTitle = "Level 2: Sending and Requesting   -   GET vs POST",
                funFact = "Websites use different kinds of requests to ask for and to send information.",
                strip = new[] { new Cell("Icon_Get", "GET - give me this"), new Cell("Icon_Server", "Server"), new Cell("Icon_Post", "POST - here is this") } } },

            { 3, new Theme {
                conceptTitle = "Level 3: Protect the Secret   -   private information",
                funFact = "Passwords are private. Even if someone asks nicely, you should never share them.",
                strip = new[] { new Cell("Icon_Key", "Password"), new Cell("Icon_Shield", "Keep it private") } } },

            { 4, new Theme {
                conceptTitle = "Level 4: Smart and Safe Routing   -   put it all together",
                funFact = "The internet connects millions of devices and moves information between them, just like our dino network.",
                strip = new[] { new Cell("Icon_Dino", "Dino"), new Cell("Icon_Node", "Node"), new Cell("Icon_Node", "Node"), new Cell("Icon_Server", "The internet") },
                intro = "The Firefly helps find a good path for our message!" } },
        };

        /// <summary>
        /// Called by the level builder once the level's own systems are in place.
        /// </summary>
        public static void Apply(int level, DinoQuestManager quest, LevelManager manager, LevelHud hud, GameObject systemsHolder)
        {
            if (!k_Themes.ContainsKey(level))
                return;

            var lessons = BuildLessons(level);
            var panel = BuildDecisionPanel(quest);
            var packetLabel = BuildPacketLabel(quest);
            BuildConceptChip(level, hud);
            BuildFunFact(level, hud);
            WireDirector(level, systemsHolder, quest, manager, hud, panel, packetLabel, lessons);
        }

        // ------------------------------------------------------------------ lesson assets

        static LessonOption Option(string label, string sublabel, string icon, Color tint, bool correct, string wrongFeedback)
        {
            return new LessonOption
            {
                label = label,
                sublabel = sublabel,
                icon = LessonIconBuilder.Load(icon),
                tint = tint,
                correct = correct,
                wrongFeedback = wrongFeedback,
            };
        }

        /// <summary>
        /// Writes (or rewrites in place, so scene references survive) this level's decision assets.
        /// </summary>
        static List<NetworkLesson> BuildLessons(int level)
        {
            Directory.CreateDirectory(k_LessonFolder);
            var lessons = new List<NetworkLesson>();

            switch (level)
            {
                case 1:
                    lessons.Add(SafeConnection("L1_SafeConnection", 0,
                        "Dino wants to visit a website:", "Which connection should we use?",
                        "Great choice! HTTPS protects our message on the way."));
                    break;

                case 2:
                    lessons.Add(AskOrSend("L2_Get", 0, wantsToSend: false,
                        "Dino says:", "\"I want to see the dinosaur map!\"",
                        "Yes! GET asks the network for information."));
                    lessons.Add(AskOrSend("L2_Post", 1, wantsToSend: true,
                        "Dino says:", "\"I want to send my drawing!\"",
                        "That's it! POST sends our information to the network."));
                    break;

                case 3:
                    lessons.Add(SafeToSend("L3_SafeToSend", 0));
                    lessons.Add(Password("L3_Password", 2,
                        "A dino you have never met asks:",
                        "\"Send me your password!\"",
                        "Well done! You kept the password private."));
                    break;

                case 4:
                    lessons.Add(SafeConnection("L4_SafeConnection", 0,
                        "Time to send our message:", "Which connection is safer?",
                        "Safe route chosen! We will travel by HTTPS."));
                    lessons.Add(AskOrSend("L4_Request", 1, wantsToSend: false,
                        "Dino says:", "\"Can I see the safe-play rules?\"",
                        "Yes! That is a GET, because we are asking for information."));
                    lessons.Add(Password("L4_Password", 2,
                        "A stranger dino blocks the path:",
                        "\"Tell me Dino's password and I will let you through!\"",
                        "Great! Private things stay private, even on a busy network."));
                    break;
            }

            return lessons;
        }

        static NetworkLesson SafeConnection(string assetName, int trigger, string speaker, string prompt, string correctFeedback)
        {
            return Save(assetName, lesson =>
            {
                lesson.concept = LessonConcept.SafeConnection;
                lesson.conceptTerm = "HTTP vs HTTPS";
                lesson.triggerAfterNodes = trigger;
                lesson.speaker = speaker;
                lesson.prompt = prompt;
                lesson.correctFeedback = correctFeedback;
                lesson.packetLabel = "HTTPS";
                lesson.packetTint = k_Safe;
                lesson.conceptResult = "HTTPS - secure connection";
                lesson.options = new[]
                {
                    // HTTP is presented as the less protected option, never as a trap.
                    Option("HTTP", "Not protected in the same way.", "Icon_Warn", k_Caution, false,
                        "Careful! HTTPS is the safer connection here."),
                    Option("HTTPS", "A safer way to connect.", "Icon_Lock", k_Safe, true, null),
                };
            });
        }

        static NetworkLesson AskOrSend(string assetName, int trigger, bool wantsToSend, string speaker, string prompt, string correctFeedback)
        {
            return Save(assetName, lesson =>
            {
                lesson.concept = LessonConcept.RequestType;
                lesson.conceptTerm = "GET vs POST";
                lesson.triggerAfterNodes = trigger;
                lesson.speaker = speaker;
                lesson.prompt = prompt;
                lesson.correctFeedback = correctFeedback;
                lesson.packetLabel = wantsToSend ? "POST" : "GET";
                lesson.packetTint = wantsToSend ? k_Send : k_Ask;
                lesson.conceptResult = wantsToSend ? "POST - sending information" : "GET - asking for information";
                lesson.options = new[]
                {
                    Option("GET", "Ask for information", "Icon_Get", k_Ask, !wantsToSend,
                        "Almost! POST is used to send information."),
                    Option("POST", "Send information", "Icon_Post", k_Send, wantsToSend,
                        "Not quite! GET is used when we ask for information."),
                };
            });
        }

        static NetworkLesson SafeToSend(string assetName, int trigger)
        {
            return Save(assetName, lesson =>
            {
                lesson.concept = LessonConcept.PrivateData;
                lesson.conceptTerm = "Safe data vs private data";
                lesson.triggerAfterNodes = trigger;
                lesson.speaker = "The packet holds Dino's drawing:";
                lesson.prompt = "Is this safe to send?";
                lesson.correctFeedback = "Right! A drawing is not a secret, so it can travel.";
                lesson.packetLabel = "SAFE DATA";
                lesson.packetTint = k_Safe;
                lesson.conceptResult = "Safe data - fine to send";
                lesson.options = new[]
                {
                    Option("Send it", "A drawing is not a secret.", "Icon_Post", k_Ask, true, null),
                    Option("Keep it private", "Hold it back.", "Icon_Shield", k_Safe, false,
                        "This one is safe to send. A drawing is not a secret."),
                };
            });
        }

        static NetworkLesson Password(string assetName, int trigger, string speaker, string prompt, string correctFeedback)
        {
            return Save(assetName, lesson =>
            {
                lesson.concept = LessonConcept.PrivateData;
                lesson.conceptTerm = "Passwords are private";
                lesson.triggerAfterNodes = trigger;
                lesson.speaker = speaker;
                lesson.prompt = prompt;
                lesson.correctFeedback = correctFeedback;
                lesson.packetLabel = "PRIVATE KEPT";
                lesson.packetTint = k_Safe;
                lesson.conceptResult = "Passwords stay private";
                lesson.options = new[]
                {
                    Option("Send password", "Dino's password is 1234.", "Icon_Key", k_Caution, false,
                        "Oops! Passwords are private secrets. Don't send yours just because someone asks."),
                    Option("Keep it private", "Passwords are secrets.", "Icon_Shield", k_Safe, true, null),
                };
            });
        }

        static NetworkLesson Save(string assetName, System.Action<NetworkLesson> fill)
        {
            var path = k_LessonFolder + assetName + ".asset";
            var lesson = AssetDatabase.LoadAssetAtPath<NetworkLesson>(path);
            var isNew = lesson == null;
            if (isNew)
                lesson = ScriptableObject.CreateInstance<NetworkLesson>();

            fill(lesson);

            if (isNew)
                AssetDatabase.CreateAsset(lesson, path);
            else
                EditorUtility.SetDirty(lesson);

            return lesson;
        }

        // ------------------------------------------------------------------ decision panel

        static DecisionPanel BuildDecisionPanel(DinoQuestManager quest)
        {
            var cam = Camera.main;

            // Same shape as the result panels: a plain-Transform anchor whose canvas child holds
            // the forward offset, because moving a RectTransform directly drops the offset.
            var anchor = new GameObject("Decision Panel");
            anchor.transform.SetParent(cam.transform, false);
            anchor.transform.localPosition = Vector3.zero;
            anchor.transform.localRotation = Quaternion.identity;

            var body = new GameObject("Decision Panel Canvas");
            body.transform.SetParent(anchor.transform, false);
            body.transform.localPosition = new Vector3(0f, 0f, 2.2f);
            body.transform.localRotation = Quaternion.identity;

            var canvas = body.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            body.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rt = body.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1160f, 800f);
            rt.localScale = Vector3.one * 0.0016f;

            Fill(body.transform, "Background", new Color(0.04f, 0.09f, 0.16f, 0.93f));

            var conceptTag = TopLabel(body.transform, "Concept", "", 34, -26f, 50f, k_Ask);
            var speaker = TopLabel(body.transform, "Speaker", "", 38, -80f, 56f, new Color(0.85f, 0.9f, 1f));
            var prompt = TopLabel(body.transform, "Prompt", "", 52, -140f, 130f, Color.white);
            var feedback = TopLabel(body.transform, "Feedback", "", 40, -672f, 110f, Color.white);

            var options = new DecisionPanel.OptionView[2];
            options[0] = BuildOption(body.transform, "Option A", -268f);
            options[1] = BuildOption(body.transform, "Option B", 268f);

            var audio = anchor.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;

            var qso = new SerializedObject(quest);
            var correctClip = qso.FindProperty("m_ArrivalClip").objectReferenceValue as AudioClip;
            var wrongClip = qso.FindProperty("m_WrongNodeClip").objectReferenceValue as AudioClip;

            // The quest's own "not this one" cue is unassigned in the source scene, so fall back to
            // a soft neutral blip. A wrong answer should sound like "think again", not like failure.
            if (wrongClip == null)
                wrongClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/VRTemplateAssets/Audio/Button_14_hover.wav");

            var panel = anchor.AddComponent<DecisionPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("m_Body").objectReferenceValue = body;
            so.FindProperty("m_HudRoot").objectReferenceValue = FindAnywhere("Level HUD");
            so.FindProperty("m_ConceptTag").objectReferenceValue = conceptTag;
            so.FindProperty("m_Speaker").objectReferenceValue = speaker;
            so.FindProperty("m_Prompt").objectReferenceValue = prompt;
            so.FindProperty("m_Feedback").objectReferenceValue = feedback;
            so.FindProperty("m_AudioSource").objectReferenceValue = audio;
            so.FindProperty("m_CorrectClip").objectReferenceValue = correctClip;
            so.FindProperty("m_WrongClip").objectReferenceValue = wrongClip;

            var array = so.FindProperty("m_Options");
            array.arraySize = options.Length;
            for (var i = 0; i < options.Length; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("button").objectReferenceValue = options[i].button;
                element.FindPropertyRelative("frame").objectReferenceValue = options[i].frame;
                element.FindPropertyRelative("icon").objectReferenceValue = options[i].icon;
                element.FindPropertyRelative("label").objectReferenceValue = options[i].label;
                element.FindPropertyRelative("sublabel").objectReferenceValue = options[i].sublabel;
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            body.SetActive(false);
            return panel;
        }

        /// <summary>
        /// One choice card: the whole card is the button, so a child only has to point at a big
        /// coloured picture. The card itself is what turns green or amber.
        /// </summary>
        static DecisionPanel.OptionView BuildOption(Transform parent, string name, float x)
        {
            var card = new GameObject(name, typeof(Image), typeof(Button));
            card.transform.SetParent(parent, false);
            var rt = card.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, -440f);
            rt.sizeDelta = new Vector2(500f, 330f);

            var frame = card.GetComponent<Image>();
            frame.color = new Color(0.16f, 0.22f, 0.32f, 0.72f);

            var button = card.GetComponent<Button>();
            button.targetGraphic = frame;

            var icon = new GameObject("Icon", typeof(Image));
            icon.transform.SetParent(card.transform, false);
            var iconRt = icon.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 1f);
            iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = new Vector2(0f, -22f);
            iconRt.sizeDelta = new Vector2(140f, 140f);
            var iconImage = icon.GetComponent<Image>();
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            var label = TopLabel(card.transform, "Label", "", 60, -170f, 74f, Color.white);
            var sublabel = TopLabel(card.transform, "Sublabel", "", 32, -246f, 74f, new Color(0.88f, 0.92f, 1f));

            return new DecisionPanel.OptionView
            {
                button = button,
                frame = frame,
                icon = iconImage,
                label = label,
                sublabel = sublabel,
            };
        }

        // ------------------------------------------------------------------ packet tag

        /// <summary>
        /// The floating word above the packet. It makes "this is a message travelling through the
        /// network" readable to anyone watching, and changes as decisions are made.
        /// </summary>
        static PacketLabel BuildPacketLabel(DinoQuestManager quest)
        {
            var orb = quest != null ? quest.Orb : null;
            if (orb == null)
                return null;

            var existing = orb.GetComponentInChildren<PacketLabel>(true);
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);

            var tag = new GameObject("Packet Tag");
            tag.transform.SetParent(orb.transform, false);
            tag.transform.localPosition = new Vector3(0f, 0.42f, 0f);

            var canvas = tag.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = tag.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(420f, 130f);
            rt.localScale = Vector3.one * 0.0016f;

            tag.AddComponent<Billboard>();

            var background = Fill(tag.transform, "Background", new Color(0.05f, 0.1f, 0.16f, 0.8f));

            var icon = new GameObject("Icon", typeof(Image));
            icon.transform.SetParent(tag.transform, false);
            var iconRt = icon.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(18f, 0f);
            iconRt.sizeDelta = new Vector2(92f, 92f);
            var iconImage = icon.GetComponent<Image>();
            iconImage.sprite = LessonIconBuilder.Load("Icon_Packet");
            iconImage.preserveAspect = true;

            var text = Label(tag.transform, "Text", "DATA", 62, TextAlignmentOptions.Center, new Vector4(110f, 10f, 20f, 10f));

            var label = tag.AddComponent<PacketLabel>();
            var so = new SerializedObject(label);
            so.FindProperty("m_Text").objectReferenceValue = text;
            so.FindProperty("m_Icon").objectReferenceValue = iconImage;
            so.FindProperty("m_Background").objectReferenceValue = background;
            so.FindProperty("m_Anchor").objectReferenceValue = orb.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            return label;
        }

        // ------------------------------------------------------------------ HUD concept chip

        /// <summary>
        /// Names the concept in its real terms at the top of the HUD, so what is being taught is
        /// visible on screen rather than only in the design document.
        /// </summary>
        static void BuildConceptChip(int level, LevelHud hud)
        {
            var hudRoot = FindAnywhere("Level HUD");
            if (hudRoot == null || hud == null)
            {
                Debug.LogError("[DinoNet] Level " + level + ": no HUD to put the concept chip on.");
                return;
            }

            var chip = Panel(hudRoot.transform, "Concept", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -30f), new Vector2(880f, 118f));
            var chipRt = chip.GetComponent<RectTransform>();
            chipRt.pivot = new Vector2(0.5f, 1f);

            var title = TopLabel(chip, "Title", k_Themes[level].conceptTitle, 34, -12f, 52f, new Color(0.95f, 0.98f, 1f));
            var result = TopLabel(chip, "Result", "Make a safe choice to connect", 30, -62f, 48f, k_Ask);

            var so = new SerializedObject(hud);
            so.FindProperty("m_ConceptTitle").objectReferenceValue = title;
            so.FindProperty("m_ConceptResult").objectReferenceValue = result;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ fun fact

        /// <summary>
        /// Adds the "Did You Know?" reward to this level's completion panel: one short fact and a
        /// small animation of the packet hopping along the idea, above the existing buttons.
        /// </summary>
        static void BuildFunFact(int level, LevelHud hud)
        {
            // The completion panel is built hidden, so GameObject.Find cannot see it.
            var anchor = FindAnywhere("Level Complete Panel");
            if (anchor == null)
            {
                Debug.LogError("[DinoNet] Level " + level + ": no completion panel to put the fun fact on.");
                return;
            }

            var canvas = anchor.transform.Find("Level Complete Panel Canvas");
            if (canvas == null)
                return;

            var theme = k_Themes[level];

            var block = new GameObject("Fun Fact", typeof(Image));
            block.transform.SetParent(canvas, false);
            var blockRt = block.GetComponent<RectTransform>();
            blockRt.anchorMin = new Vector2(0.5f, 1f);
            blockRt.anchorMax = new Vector2(0.5f, 1f);
            blockRt.pivot = new Vector2(0.5f, 1f);
            blockRt.anchoredPosition = new Vector2(0f, -236f);
            blockRt.sizeDelta = new Vector2(840f, 356f);
            block.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);

            var bulb = new GameObject("Bulb", typeof(Image));
            bulb.transform.SetParent(block.transform, false);
            var bulbRt = bulb.GetComponent<RectTransform>();
            bulbRt.anchorMin = new Vector2(0f, 1f);
            bulbRt.anchorMax = new Vector2(0f, 1f);
            bulbRt.pivot = new Vector2(0f, 1f);
            bulbRt.anchoredPosition = new Vector2(24f, -16f);
            bulbRt.sizeDelta = new Vector2(64f, 64f);
            var bulbImage = bulb.GetComponent<Image>();
            bulbImage.sprite = LessonIconBuilder.Load("Icon_Bulb");
            bulbImage.color = new Color(1f, 0.9f, 0.45f);
            bulbImage.preserveAspect = true;

            TopLabel(block.transform, "Header", "DID YOU KNOW?", 40, -20f, 56f, new Color(1f, 0.9f, 0.45f));
            TopLabel(block.transform, "Fact", theme.funFact, 34, -78f, 120f, Color.white);

            BuildStrip(block.transform, theme.strip);
        }

        /// <summary>The animated row under the fact: icons with a packet hopping between them.</summary>
        static void BuildStrip(Transform parent, Cell[] cells)
        {
            var strip = new GameObject("Strip");
            strip.transform.SetParent(parent, false);
            var stripRt = strip.AddComponent<RectTransform>();
            stripRt.anchorMin = new Vector2(0.5f, 1f);
            stripRt.anchorMax = new Vector2(0.5f, 1f);
            stripRt.pivot = new Vector2(0.5f, 1f);
            stripRt.anchoredPosition = new Vector2(0f, -196f);
            stripRt.sizeDelta = new Vector2(800f, 150f);

            var spacing = cells.Length > 3 ? 190f : 240f;
            var anchors = new RectTransform[cells.Length];
            var icons = new Image[cells.Length];

            for (var i = 0; i < cells.Length; i++)
            {
                var x = (i - (cells.Length - 1) * 0.5f) * spacing;

                var cell = new GameObject("Cell " + i);
                cell.transform.SetParent(strip.transform, false);
                var cellRt = cell.AddComponent<RectTransform>();
                cellRt.anchorMin = new Vector2(0.5f, 1f);
                cellRt.anchorMax = new Vector2(0.5f, 1f);
                cellRt.pivot = new Vector2(0.5f, 1f);
                cellRt.anchoredPosition = new Vector2(x, -10f);
                cellRt.sizeDelta = new Vector2(spacing - 20f, 140f);

                var icon = new GameObject("Icon", typeof(Image));
                icon.transform.SetParent(cell.transform, false);
                var iconRt = icon.GetComponent<RectTransform>();
                iconRt.anchorMin = new Vector2(0.5f, 1f);
                iconRt.anchorMax = new Vector2(0.5f, 1f);
                iconRt.pivot = new Vector2(0.5f, 1f);
                iconRt.anchoredPosition = new Vector2(0f, 0f);
                iconRt.sizeDelta = new Vector2(76f, 76f);
                var iconImage = icon.GetComponent<Image>();
                iconImage.sprite = LessonIconBuilder.Load(cells[i].icon);
                iconImage.color = new Color(0.75f, 0.92f, 1f, 0.35f);
                iconImage.preserveAspect = true;

                TopLabel(cell.transform, "Caption", cells[i].caption, 23, -84f, 66f, new Color(0.85f, 0.92f, 1f));

                // The token travels between these, so it needs a point level with the icons.
                var point = new GameObject("Point");
                point.transform.SetParent(strip.transform, false);
                var pointRt = point.AddComponent<RectTransform>();
                pointRt.anchorMin = new Vector2(0.5f, 1f);
                pointRt.anchorMax = new Vector2(0.5f, 1f);
                pointRt.pivot = new Vector2(0.5f, 0.5f);
                pointRt.anchoredPosition = new Vector2(x, -40f);
                pointRt.sizeDelta = Vector2.zero;

                anchors[i] = pointRt;
                icons[i] = iconImage;
            }

            var token = new GameObject("Token", typeof(Image));
            token.transform.SetParent(strip.transform, false);
            var tokenRt = token.GetComponent<RectTransform>();
            tokenRt.anchorMin = new Vector2(0.5f, 1f);
            tokenRt.anchorMax = new Vector2(0.5f, 1f);
            tokenRt.pivot = new Vector2(0.5f, 0.5f);
            tokenRt.sizeDelta = new Vector2(46f, 46f);
            var tokenImage = token.GetComponent<Image>();
            tokenImage.sprite = LessonIconBuilder.Load("Icon_Packet");
            tokenImage.color = new Color(0.45f, 1f, 0.75f);
            tokenImage.preserveAspect = true;

            var component = strip.AddComponent<ConceptStrip>();
            var so = new SerializedObject(component);
            so.FindProperty("m_Token").objectReferenceValue = tokenRt;

            var cellArray = so.FindProperty("m_Cells");
            cellArray.arraySize = anchors.Length;
            for (var i = 0; i < anchors.Length; i++)
                cellArray.GetArrayElementAtIndex(i).objectReferenceValue = anchors[i];

            var iconArray = so.FindProperty("m_CellIcons");
            iconArray.arraySize = icons.Length;
            for (var i = 0; i < icons.Length; i++)
                iconArray.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ director

        static void WireDirector(int level, GameObject systemsHolder, DinoQuestManager quest, LevelManager manager,
            LevelHud hud, DecisionPanel panel, PacketLabel packetLabel, List<NetworkLesson> lessons)
        {
            var director = systemsHolder.AddComponent<LessonDirector>();
            var so = new SerializedObject(director);
            so.FindProperty("m_Quest").objectReferenceValue = quest;
            so.FindProperty("m_Level").objectReferenceValue = manager;
            so.FindProperty("m_Panel").objectReferenceValue = panel;
            so.FindProperty("m_Hud").objectReferenceValue = hud;
            so.FindProperty("m_PacketLabel").objectReferenceValue = packetLabel;
            so.FindProperty("m_IntroMessage").stringValue = k_Themes[level].intro ?? string.Empty;

            var array = so.FindProperty("m_Lessons");
            array.arraySize = lessons.Count;
            for (var i = 0; i < lessons.Count; i++)
                array.GetArrayElementAtIndex(i).objectReferenceValue = lessons[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ small UI helpers

        /// <summary>Finds a scene object by name whether or not it is currently active.</summary>
        static GameObject FindAnywhere(string name)
        {
            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (transform.name == name)
                    return transform.gameObject;
            }

            return null;
        }

        static Image Fill(Transform parent, string name, Color colour)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A label pinned a fixed distance below the top of its parent.</summary>
        static TMP_Text TopLabel(Transform parent, string name, string text, float size, float y, float height, Color colour)
        {
            var label = Label(parent, name, text, size, TextAlignmentOptions.Top, new Vector4(16f, 0f, 16f, 0f));
            label.color = colour;
            label.raycastTarget = false;
            var rt = label.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(-24f, height);
            return label;
        }

        static Transform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.42f);
            image.raycastTarget = false;
            return go.transform;
        }

        static TMP_Text Label(Transform parent, string name, string text, float size, TextAlignmentOptions align, Vector4 margin)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.margin = margin;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return tmp;
        }
    }
}
