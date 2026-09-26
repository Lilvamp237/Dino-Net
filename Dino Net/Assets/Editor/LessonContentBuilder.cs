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

            { 5, new Theme {
                conceptTitle = "Level 5: Pieces and Signal   -   packets and signal strength",
                funFact = "Big messages are split into small pieces called packets, and joined up again at the end. Long distances weaken signals, so networks use boosters.",
                strip = new[] { new Cell("Icon_Packet", "Packet"), new Cell("Icon_Packet", "Packet"), new Cell("Icon_Packet", "Packet"), new Cell("Icon_Server", "Joined up") },
                intro = "Big messages travel in pieces. Watch your signal!" } },

            { 6, new Theme {
                conceptTitle = "Level 6: Got It!   -   replies and backup roads",
                funFact = "A computer says \"got it!\" when a message arrives. No reply? The sender tries again. If a road is blocked, the network finds another way.",
                strip = new[] { new Cell("Icon_Dino", "Send"), new Cell("Icon_Node", "Receive"), new Cell("Icon_Arrow", "Got it!") },
                intro = "Listen for the \"got it!\" from each dino." } },

            { 7, new Theme {
                conceptTitle = "Level 7: Find the Home   -   addresses and the phone book",
                funFact = "Every device has an address, like a home number. DNS is the internet's phone book: it turns a name into an address.",
                strip = new[] { new Cell("Icon_Bulb", "Name"), new Cell("Icon_Server", "Phone book"), new Cell("Icon_Node", "Address") },
                intro = "Every dino has a home number. Check the address!" } },

            { 8, new Theme {
                conceptTitle = "Level 8: Fake Friends   -   staying safe online",
                funFact = "Some messages pretend to be from friends. Never click strange links or share secrets - tell a grown-up. A strong password is long and silly.",
                strip = new[] { new Cell("Icon_Warn", "Fake friend"), new Cell("Icon_Shield", "Tell a grown-up"), new Cell("Icon_Key", "Strong password") },
                intro = "Careful - not everyone online is who they say they are." } },

            { 9, new Theme {
                conceptTitle = "Level 9: Gatekeepers and Secret Codes   -   firewalls and encryption",
                funFact = "A firewall is a gatekeeper that blocks sneaky visitors. Encryption scrambles a message into a secret code so only the right receiver can read it.",
                strip = new[] { new Cell("Icon_Shield", "Firewall"), new Cell("Icon_Key", "Secret code"), new Cell("Icon_Lock", "Locked") },
                intro = "A gatekeeper is ahead. Get ready to show your shield!" } },

            { 10, new Theme {
                conceptTitle = "Level 10: Grand Challenge   -   put it all together",
                funFact = "Real networks use many ideas together: safe connections, strong passwords, backup roads, firewalls and secret codes.",
                strip = new[] { new Cell("Icon_Dino", "Dino"), new Cell("Icon_Lock", "Safe"), new Cell("Icon_Shield", "Protected"), new Cell("Icon_Server", "The internet") },
                intro = "The Grand Challenge! Use everything you have learned." } },
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

                case 5:
                    lessons.Add(PiecesLesson("L5_Pieces", 0));
                    lessons.Add(BoosterLesson("L5_Signal", 1));
                    lessons.Add(ShortRoadLesson("L5_ShortRoad", 2));
                    break;

                case 6:
                    lessons.Add(AckIntroLesson("L6_AckIntro", 0));
                    lessons.Add(ResendLesson("L6_Resend", 2));
                    lessons.Add(BackupRoadLesson("L6_Backup", 3));
                    break;

                case 7:
                    lessons.Add(HomeLesson("L7_Home1", 0, "The message says: Home 1.", "Which dinosaur lives at Home 1?", "Spiky Stego", "Home 1", "Crest Head", "Home 2", 1));
                    lessons.Add(DnsLesson("L7_Dns", 1));
                    lessons.Add(HomeLesson("L7_Home4", 2, "The last message is for Home 4.", "Which dinosaur lives at Home 4?", "Three Horns", "Home 4", "Blue Spikes", "Home 3", 4));
                    break;

                case 8:
                    lessons.Add(ShinyLinkLesson("L8_Link", 0));
                    lessons.Add(FakeFriendLesson("L8_Fake", 1));
                    lessons.Add(StrongPasswordLesson("L8_Strong", 2));
                    lessons.Add(Password("L8_Password", 3,
                        "A stranger dino blocks the path:",
                        "\"Tell me Dino's password and I will let you through!\"",
                        "Well done! A real friend never needs your password."));
                    break;

                case 9:
                    lessons.Add(FirewallIntroLesson("L9_FirewallIntro", 0));
                    lessons.Add(ShieldGateLesson("L9_Shield", 2));
                    lessons.Add(SecretCodeLesson("L9_Code", 3));
                    lessons.Add(KeyHolderLesson("L9_KeyHolder", 4));
                    break;

                case 10:
                    lessons.Add(SafeConnection("L10_SafeConnection", 0,
                        "The Grand Challenge begins:", "Which connection is safer?",
                        "Safe route chosen! We travel by HTTPS."));
                    lessons.Add(AskOrSend("L10_Request", 1, wantsToSend: false,
                        "Dino says:", "\"Can I see the jungle map?\"",
                        "Yes! That is a GET, because we are asking for information."));
                    lessons.Add(StrongPasswordLesson("L10_Strong", 2));
                    lessons.Add(BackupRoadLesson("L10_Backup", 3));
                    lessons.Add(ShieldGateLesson("L10_Shield", 4));
                    lessons.Add(SecretCodeLesson("L10_Code", 5));
                    lessons.Add(FakeFriendLesson("L10_Fake", 6));
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


        // ---- generic two-option lesson used by the newer levels

        static NetworkLesson Two(string assetName, LessonConcept concept, string term, int trigger, string speaker, string prompt,
            string correctFeedback, string packetLabel, Color packetTint, string conceptResult, LessonOption right, LessonOption wrong, bool rightFirst)
        {
            return Save(assetName, lesson =>
            {
                lesson.concept = concept;
                lesson.conceptTerm = term;
                lesson.triggerAfterNodes = trigger;
                lesson.speaker = speaker;
                lesson.prompt = prompt;
                lesson.correctFeedback = correctFeedback;
                lesson.packetLabel = packetLabel;
                lesson.packetTint = packetTint;
                lesson.conceptResult = conceptResult;
                lesson.options = rightFirst ? new[] { right, wrong } : new[] { wrong, right };
            });
        }

        static NetworkLesson PiecesLesson(string name, int trigger) => Two(name, LessonConcept.RequestType, ConceptCatalog.Pieces, trigger,
            "The picture is too big to send in one go:", "How should we send it?",
            "Yes! Big messages are split into small pieces called packets.", "PIECE", k_Ask, "Packets - messages in pieces",
            Option("Small pieces", "Each piece travels by itself.", "Icon_Packet", k_Safe, true, null),
            Option("All at once", "One giant lump.", "Icon_Warn", k_Caution, false, "Big lumps are slow and easy to lose. Small pieces are better!"), false);

        static NetworkLesson BoosterLesson(string name, int trigger) => Two(name, LessonConcept.SafeConnection, ConceptCatalog.Signal, trigger,
            "The signal fades on long roads:", "How do we make it strong again?",
            "Great! Boosters pass the signal on strongly.", "STRONG", k_Safe, "Boosters keep the signal strong",
            Option("Use a booster", "Boosters strengthen the signal.", "Icon_Node", k_Safe, true, null),
            Option("Walk faster", "Speed does not fix distance.", "Icon_Arrow", k_Caution, false, "Not quite! Boosters give the signal a fresh push."), true);

        static NetworkLesson ShortRoadLesson(string name, int trigger) => Two(name, LessonConcept.SafeConnection, ConceptCatalog.Signal, trigger,
            "Two roads lead to the next dino:", "Which road keeps the signal stronger?",
            "Right! Shorter roads keep signals strong.", "STRONG", k_Safe, "Short roads, strong signal",
            Option("The short road", "Less distance, less fading.", "Icon_Arrow", k_Safe, true, null),
            Option("The long road", "More distance, more fading.", "Icon_Warn", k_Caution, false, "Longer roads weaken the signal more. Try the short one!"), false);

        static NetworkLesson AckIntroLesson(string name, int trigger) => Two(name, LessonConcept.RequestType, ConceptCatalog.Acks, trigger,
            "A dino says \"Got it!\" when a message arrives:", "Why does it say that?",
            "Yes! \"Got it!\" tells the sender the message arrived.", "GOT IT?", k_Ask, "Receivers reply: got it!",
            Option("So the sender knows", "No reply means it might be lost.", "Icon_Arrow", k_Safe, true, null),
            Option("Just to be friendly", "Nice, but not the reason.", "Icon_Bulb", k_Caution, false, "It is friendly, but the real reason is telling the sender it arrived!"), true);

        static NetworkLesson ResendLesson(string name, int trigger) => Two(name, LessonConcept.RequestType, ConceptCatalog.Acks, trigger,
            "The first try got lost and no \"got it!\" came back:", "What should a sender do?",
            "Right! If there is no \"got it!\", send it again.", "RESENT", k_Safe, "No reply? Send it again",
            Option("Send it again", "Try one more time.", "Icon_Post", k_Safe, true, null),
            Option("Give up", "Forget the message.", "Icon_Warn", k_Caution, false, "Never give up! Sending again makes sure it arrives."), false);

        static NetworkLesson BackupRoadLesson(string name, int trigger) => Two(name, LessonConcept.SafeConnection, ConceptCatalog.Backup, trigger,
            "A rockfall blocked the road ahead:", "What should the message do?",
            "Yes! When one road is blocked, the network uses another.", "REROUTED", k_Safe, "Blocked road? Take a backup road",
            Option("Take a backup road", "Networks have more than one route.", "Icon_Arrow", k_Safe, true, null),
            Option("Wait for it to clear", "That could take a long time.", "Icon_Warn", k_Caution, false, "Waiting is slow! Networks find another road."), true);

        static NetworkLesson HomeLesson(string name, int trigger, string speaker, string prompt, string rightName, string rightHome,
            string wrongName, string wrongHome, int number) => Two(name, LessonConcept.RequestType, ConceptCatalog.Address, trigger,
            speaker, prompt,
            "Right! Every dino has its own home number, like an address.", "HOME " + number, k_Ask, "Addresses find the right home",
            Option(rightName, "Lives at " + rightHome, "Icon_Dino", k_Safe, true, null),
            Option(wrongName, "Lives at " + wrongHome, "Icon_Dino", k_Caution, false, "Check the number! " + wrongName + " lives at " + wrongHome + "."), trigger % 2 == 0);

        static NetworkLesson DnsLesson(string name, int trigger) => Two(name, LessonConcept.RequestType, ConceptCatalog.Dns, trigger,
            "Ask the Wise Old Dino, the phone book of the jungle:", "Where does Crest Head live?",
            "Yes! A phone book (DNS) turns a name into a home number.", "HOME 2", k_Ask, "DNS: names to addresses",
            Option("Home 2", "The phone book says so.", "Icon_Bulb", k_Safe, true, null),
            Option("Home 4", "That is Three Horns.", "Icon_Dino", k_Caution, false, "That is Three Horns' home. The phone book says Crest Head lives at Home 2."), false);

        static NetworkLesson ShinyLinkLesson(string name, int trigger) => Two(name, LessonConcept.PrivateData, ConceptCatalog.Fake, trigger,
            "A dino you have never met says:", "\"Click my shiny link to win a prize!\"",
            "Smart! Ignore strange links and tell a grown-up.", "SAFE", k_Safe, "Strange links? Tell a grown-up",
            Option("Ignore it, tell a grown-up", "Unknown links can be tricks.", "Icon_Shield", k_Safe, true, null),
            Option("Click the link", "A free prize? Sounds nice...", "Icon_Warn", k_Caution, false, "Careful! Prizes from strangers are often tricks. Never click - tell a grown-up."), true);

        static NetworkLesson FakeFriendLesson(string name, int trigger) => Two(name, LessonConcept.PrivateData, ConceptCatalog.Fake, trigger,
            "This dino looks like your friend, but it is a fake:", "\"What is your home address?\"",
            "Well done! Do not share private things until a grown-up says it is OK.", "SAFE", k_Safe, "Fake friends: ask a grown-up",
            Option("Ask a grown-up first", "Check before sharing.", "Icon_Shield", k_Safe, true, null),
            Option("Tell them", "They seem friendly...", "Icon_Warn", k_Caution, false, "Whoa! A fake friend could be a stranger. Ask a grown-up first."), false);

        static NetworkLesson StrongPasswordLesson(string name, int trigger) => Two(name, LessonConcept.PrivateData, ConceptCatalog.Strong, trigger,
            "Time to protect Dino's account:", "Which password is stronger?",
            "Strong choice! Long and silly beats short and simple.", "STRONG", k_Safe, "Long, silly passwords are strong",
            Option("Purple-Volcano-Sings-7", "Long, silly and hard to guess.", "Icon_Lock", k_Safe, true, null),
            Option("1234", "Short and easy to guess.", "Icon_Key", k_Caution, false, "Too easy to guess! Long, silly passwords are much stronger."), false);

        static NetworkLesson FirewallIntroLesson(string name, int trigger) => Two(name, LessonConcept.SafeConnection, ConceptCatalog.Firewall, trigger,
            "The Firewall Bouncer guards the gate:", "What does a firewall do?",
            "Yes! A firewall is a gatekeeper for the network.", "FIREWALL", k_Safe, "Firewalls block sneaky visitors",
            Option("Blocks sneaky visitors", "Only friendly packets get in.", "Icon_Shield", k_Safe, true, null),
            Option("Makes the road longer", "It does not change the road.", "Icon_Warn", k_Caution, false, "A firewall is a gatekeeper: it keeps sneaky visitors out!"), true);

        static NetworkLesson ShieldGateLesson(string name, int trigger) => Two(name, LessonConcept.SafeConnection, ConceptCatalog.Firewall, trigger,
            "Bouncer: \"Halt! Show me your shield!\"", "What do we show?",
            "The bouncer nods: come on in!", "SHIELD", k_Safe, "Firewall lets safe packets in",
            Option("The HTTPS shield", "It shows we are safe.", "Icon_Lock", k_Safe, true, null),
            Option("Nothing", "Let's just walk in.", "Icon_Warn", k_Caution, false, "The bouncer will not let an unshielded packet in. Show the HTTPS shield!"), false);

        static NetworkLesson SecretCodeLesson(string name, int trigger) => Two(name, LessonConcept.SafeConnection, ConceptCatalog.Codes, trigger,
            "The road ahead is open - anyone could peek:", "How can we keep the message secret?",
            "Now it is scrambled! Only the dino with the key can read it.", "#@%&", new Color(0.78f, 0.62f, 1f), "Encryption - a secret code",
            Option("Lock it with a secret code", "Only the right dino can read it.", "Icon_Key", k_Safe, true, null),
            Option("Send it as it is", "Anyone could read it.", "Icon_Warn", k_Caution, false, "Anyone walking by could read it! Lock it with a secret code."), true);

        static NetworkLesson KeyHolderLesson(string name, int trigger) => Two(name, LessonConcept.SafeConnection, ConceptCatalog.Codes, trigger,
            "Only one dino has the key:", "Who can read the locked message?",
            "Yes! Without the key it is just scrambled nonsense.", "LOCKED", new Color(0.78f, 0.62f, 1f), "Only the key holder can read it",
            Option("Only the dino with the key", "It unlocks the message.", "Icon_Key", k_Safe, true, null),
            Option("Everyone walking by", "They can read it too.", "Icon_Warn", k_Caution, false, "To everyone else it looks like gibberish. Only the key holder can read it!"), false);

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
            rt.sizeDelta = new Vector2(1160f, 880f);
            rt.localScale = Vector3.one * 0.0016f;

            Fill(body.transform, "Background", new Color(0.04f, 0.09f, 0.16f, 0.93f));

            var conceptTag = TopLabel(body.transform, "Concept", "", 34, -26f, 50f, k_Ask);
            var speaker = TopLabel(body.transform, "Speaker", "", 38, -80f, 56f, new Color(0.85f, 0.9f, 1f));
            var prompt = TopLabel(body.transform, "Prompt", "", 52, -140f, 130f, Color.white);
            var feedback = TopLabel(body.transform, "Feedback", "", 40, -666f, 96f, Color.white);

            // Shown once a message has been up for a moment, so a child who already understands
            // does not have to sit through the whole recording.
            var skip = BuildSkipButton(body.transform);

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
            so.FindProperty("m_SkipButton").objectReferenceValue = skip.gameObject;
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

            Bind(skip, panel, "Skip");

            skip.gameObject.SetActive(false);
            body.SetActive(false);
            return panel;
        }

        /// <summary>The small "Got it!" button that cuts a message short.</summary>
        static Button BuildSkipButton(Transform parent)
        {
            var go = new GameObject("Got It Button", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, -806f);
            rt.sizeDelta = new Vector2(320f, 66f);

            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.82f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var label = Label(go.transform, "Label", "Got it!", 36, TextAlignmentOptions.Center, Vector4.zero);
            label.color = new Color(0.08f, 0.1f, 0.12f);
            label.raycastTarget = false;
            return button;
        }

        /// <summary>Points a button at a method on a component, the way Unity's inspector would.</summary>
        static void Bind(Button button, Object target, string method)
        {
            var so = new SerializedObject(button);
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            calls.arraySize = 1;
            var call = calls.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = method;
            call.FindPropertyRelative("m_Mode").enumValueIndex = 1;   // void
            call.FindPropertyRelative("m_CallState").enumValueIndex = 2;   // runtime only
            so.ApplyModifiedPropertiesWithoutUndo();
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
