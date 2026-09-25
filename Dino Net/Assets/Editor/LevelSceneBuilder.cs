using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using DinoNet;

namespace DinoNetEditor
{
    /// <summary>
    /// Generates the playable scenes from the untouched Demo scene. Every level is produced the
    /// same way - duplicate Demo, trim the route to the level's node count, restyle the world,
    /// then attach the shared level systems - so no level carries bespoke logic.
    /// </summary>
    public static class LevelSceneBuilder
    {
        const string k_SourceScene = "Assets/Scenes/Demo.unity";
        const string k_SceneFolder = "Assets/Scenes/";

        public class Atmosphere
        {
            public string name;
            public Color sun = Color.white;
            public float sunIntensity = 1.3f;
            public Vector3 sunAngles = new Vector3(50f, -30f, 0f);
            public Color ambient = new Color(0.55f, 0.58f, 0.62f);
            public Color fog = new Color(0.7f, 0.8f, 0.9f);
            public float fogDensity = 0.004f;
            public Color skyTint = new Color(0.55f, 0.65f, 0.8f);
            public Color groundTint = new Color(0.45f, 0.5f, 0.4f);
            public float skyExposure = 1.3f;

            /// <summary>The scene's second directional light, which otherwise washes out every mood.</summary>
            public Color fill = new Color(0.62f, 0.68f, 1f);
            public float fillIntensity = 0.25f;

            /// <summary>Set to reuse an existing sky asset instead of generating a procedural one.</summary>
            public string skyboxAsset;
        }

        /// <summary>Route node names per level, in visit order. Level index = nodes - 1.</summary>
        static readonly Dictionary<int, string[]> k_Routes = new Dictionary<int, string[]>
        {
            { 1, new[] { "Hub_A", "Triceratops_EndUser" } },
            { 2, new[] { "Hub_A", "Hub_B", "Triceratops_EndUser" } },
            { 3, new[] { "Hub_A", "Hub_B", "Node_C", "Triceratops_EndUser" } },
            { 4, new[] { "Hub_A", "Hub_B", "Node_C", "Node_D", "Triceratops_EndUser" } },
            { 5, new[] { "Hub_A", "Hub_B", "Node_C", "Triceratops_EndUser" } },
            { 6, new[] { "Hub_A", "Hub_B", "Node_C", "Node_D", "Triceratops_EndUser" } },
            { 7, new[] { "Hub_A", "Hub_B", "Node_C", "Triceratops_EndUser" } },
            { 8, new[] { "Hub_A", "Hub_B", "Node_C", "Node_D", "Triceratops_EndUser" } },
            { 9, new[] { "Hub_A", "Hub_B", "Node_C", "Node_D", "Triceratops_EndUser" } },
            { 10, new[] { "Hub_A", "Node_E", "Hub_B", "Node_C", "Node_D", "Node_F", "Triceratops_EndUser" } },
        };

        /// <summary>Countdown per level in seconds; later levels ask more questions so they get more time.</summary>
        static readonly Dictionary<int, float> k_TimeLimits = new Dictionary<int, float>
        {
            { 1, 300f }, { 2, 300f }, { 3, 300f }, { 4, 300f },
            { 5, 330f }, { 6, 360f }, { 7, 330f }, { 8, 360f }, { 9, 390f }, { 10, 480f },
        };

        static readonly Dictionary<int, Atmosphere> k_Atmospheres = new Dictionary<int, Atmosphere>
        {
            { 1, new Atmosphere { name = "Bright morning", sun = new Color(1f, 0.98f, 0.92f), sunIntensity = 1.6f,
                                  sunAngles = new Vector3(55f, -25f, 0f), ambient = new Color(0.78f, 0.81f, 0.84f),
                                  fog = new Color(0.82f, 0.9f, 0.98f), fogDensity = 0.0025f,
                                  skyTint = new Color(0.78f, 0.87f, 1f), groundTint = new Color(0.52f, 0.56f, 0.44f), skyExposure = 1.7f,
                                  fill = new Color(1f, 0.97f, 0.9f), fillIntensity = 0.35f } },

            { 2, new Atmosphere { name = "Warm afternoon", sun = new Color(1f, 0.95f, 0.82f), sunIntensity = 1.4f,
                                  sunAngles = new Vector3(38f, 70f, 0f), ambient = new Color(0.72f, 0.68f, 0.58f),
                                  fog = new Color(0.88f, 0.85f, 0.7f), fogDensity = 0.0045f,
                                  skyTint = new Color(0.9f, 0.85f, 0.72f), groundTint = new Color(0.55f, 0.5f, 0.34f), skyExposure = 1.5f,
                                  fill = new Color(1f, 0.94f, 0.78f), fillIntensity = 0.25f } },

            { 3, new Atmosphere { name = "Sunset", sun = new Color(1f, 0.62f, 0.34f), sunIntensity = 1.25f,
                                  // Lifted off pure silhouette so the route stays readable at dusk.
                                  sunAngles = new Vector3(18f, 205f, 0f), ambient = new Color(0.56f, 0.46f, 0.42f),
                                  fog = new Color(0.85f, 0.55f, 0.35f), fogDensity = 0.0075f,
                                  skyTint = new Color(0.85f, 0.5f, 0.35f), groundTint = new Color(0.35f, 0.25f, 0.2f), skyExposure = 1.0f,
                                  fill = new Color(0.75f, 0.55f, 0.85f), fillIntensity = 0.3f } },

            // Level 4 reuses the original scene's purple starry sky rather than a generated one.
            { 4, new Atmosphere { name = "Twilight night", sun = new Color(0.45f, 0.55f, 0.85f), sunIntensity = 0.35f,
                                  sunAngles = new Vector3(65f, 160f, 0f), ambient = new Color(0.30f, 0.42f, 0.62f),
                                  fog = new Color(0.42f, 0.24f, 0.40f), fogDensity = 0.016f,
                                  skyTint = new Color(0.16f, 0.22f, 0.38f), groundTint = new Color(0.1f, 0.12f, 0.16f), skyExposure = 0.55f,
                                  fill = new Color(0.62f, 0.68f, 1f), fillIntensity = 0.8f,
                                  skyboxAsset = "Assets/Twilight/TwilightSky.mat" } },

            { 5, new Atmosphere { name = "Golden hour", sun = new Color(1f, 0.85f, 0.5f), sunIntensity = 1.4f,
                                  sunAngles = new Vector3(28f, 60f, 0f), ambient = new Color(0.7f, 0.62f, 0.5f),
                                  fog = new Color(0.95f, 0.8f, 0.55f), fogDensity = 0.006f,
                                  fill = new Color(1f, 0.85f, 0.6f), fillIntensity = 0.3f,
                                  skyboxAsset = "Assets/Materials/PrehistoricSky_Level5.mat" } },

            { 6, new Atmosphere { name = "Misty dawn", sun = new Color(1f, 0.85f, 0.85f), sunIntensity = 1.1f,
                                  sunAngles = new Vector3(15f, -40f, 0f), ambient = new Color(0.66f, 0.64f, 0.74f),
                                  fog = new Color(0.85f, 0.82f, 0.9f), fogDensity = 0.011f,
                                  fill = new Color(0.85f, 0.8f, 1f), fillIntensity = 0.35f,
                                  skyboxAsset = "Assets/Materials/PrehistoricSky_Level6.mat" } },

            { 7, new Atmosphere { name = "Bright noon", sun = new Color(1f, 0.98f, 0.9f), sunIntensity = 1.7f,
                                  sunAngles = new Vector3(70f, 20f, 0f), ambient = new Color(0.8f, 0.84f, 0.9f),
                                  fog = new Color(0.8f, 0.92f, 1f), fogDensity = 0.0025f,
                                  fill = new Color(1f, 1f, 0.95f), fillIntensity = 0.3f,
                                  skyboxAsset = "Assets/Materials/PrehistoricSky_Level7.mat" } },

            { 8, new Atmosphere { name = "Stormy dusk", sun = new Color(1f, 0.4f, 0.2f), sunIntensity = 0.95f,
                                  sunAngles = new Vector3(10f, 190f, 0f), ambient = new Color(0.5f, 0.34f, 0.34f),
                                  fog = new Color(0.6f, 0.28f, 0.25f), fogDensity = 0.01f,
                                  fill = new Color(0.8f, 0.4f, 0.5f), fillIntensity = 0.35f,
                                  skyboxAsset = "Assets/Materials/PrehistoricSky_Level8.mat" } },

            { 9, new Atmosphere { name = "Deep night", sun = new Color(0.4f, 0.45f, 0.8f), sunIntensity = 0.3f,
                                  sunAngles = new Vector3(60f, 150f, 0f), ambient = new Color(0.28f, 0.34f, 0.55f),
                                  fog = new Color(0.18f, 0.2f, 0.4f), fogDensity = 0.014f,
                                  fill = new Color(0.5f, 0.6f, 1f), fillIntensity = 0.7f,
                                  skyboxAsset = "Assets/Materials/PrehistoricSky_Level9.mat" } },

            { 10, new Atmosphere { name = "Twilight finale", sun = new Color(0.9f, 0.5f, 0.35f), sunIntensity = 0.6f,
                                   sunAngles = new Vector3(12f, 160f, 0f), ambient = new Color(0.32f, 0.36f, 0.5f),
                                   fog = new Color(0.42f, 0.24f, 0.4f), fogDensity = 0.016f,
                                   fill = new Color(0.62f, 0.68f, 1f), fillIntensity = 0.8f,
                                   skyboxAsset = "Assets/Materials/PrehistoricSky_Level10.mat" } },
        };

        [MenuItem("DinoNet/Build All Playable Scenes")]
        public static void BuildAll()
        {
            for (var level = 1; level <= GameFlow.LastLevel; level++)
                BuildLevel(level);

            BuildTutorial();
            BuildMainMenu();
            RegisterBuildScenes();
            Debug.Log("[DinoNet] All playable scenes built.");
        }

        // ------------------------------------------------------------------ levels

        public static void BuildLevel(int level)
        {
            var path = k_SceneFolder + "Level" + level + ".unity";
            var scene = Duplicate(path);

            var route = PrepareRoute(k_Routes[level], out _);
            PlaceNodesOnJunctions(level, route);
            ApplyAtmosphere(k_Atmospheres[level], level);
            ApplyGroundTint(level, k_Atmospheres[level].groundTint);
            ThinEnvironment(level);
            FixDinoMovement();
            AddEnvironmentColliders();
            ClearSpawnOverlaps();
            TidyEditModeBanners();
            AddFireflyTrail();
            AddArrivalReactions();
            var source = SetUpSourceDino();

            var quest = Object.FindFirstObjectByType<DinoQuestManager>();
            var hud = BuildHud(out var levelManagerHolder);

            var manager = levelManagerHolder.AddComponent<LevelManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("m_LevelNumber").intValue = level;
            so.FindProperty("m_Quest").objectReferenceValue = quest;
            so.FindProperty("m_Hud").objectReferenceValue = hud;
            so.FindProperty("m_TimeLimit").floatValue = k_TimeLimits.TryGetValue(level, out var limit) ? limit : 300f;
            so.FindProperty("m_UseTimer").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            WireHudButtons(hud, manager, null);
            AddRoutePresenter(quest, source);
            WireConnections(quest, null);
            FixPlayerCollider();

            // The networking lesson sits on top of the finished level rather than replacing any
            // of it: same nodes, same packet, same firefly, with decisions along the route.
            LessonContentBuilder.Apply(level, quest, manager, hud, levelManagerHolder);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[DinoNet] Built " + path + " with " + route.Count + " nodes (" + k_Atmospheres[level].name + ").");
        }

        public static void BuildTutorial()
        {
            var path = k_SceneFolder + "Tutorial.unity";
            var scene = Duplicate(path);

            // The tutorial keeps one spare dinosaur as a decoy for the wrong-node lesson.
            var tutorialRoute = PrepareRoute(k_Routes[1], out var decoy, keepDecoy: true);
            PlaceNodesOnJunctions(1, tutorialRoute);
            ApplyAtmosphere(k_Atmospheres[1], 1);
            ApplyGroundTint(1, k_Atmospheres[1].groundTint);
            ThinEnvironment(1);
            FixDinoMovement();
            AddEnvironmentColliders();
            ClearSpawnOverlaps();
            TidyEditModeBanners();
            AddFireflyTrail();
            AddArrivalReactions();
            var source = SetUpSourceDino();

            var quest = Object.FindFirstObjectByType<DinoQuestManager>();
            var hud = BuildHud(out var holder);

            // The tutorial keeps the progress bar but drops the countdown and win/fail panels.
            var manager = holder.AddComponent<LevelManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("m_LevelNumber").intValue = 0;
            so.FindProperty("m_Quest").objectReferenceValue = quest;
            so.FindProperty("m_Hud").objectReferenceValue = hud;
            so.FindProperty("m_UseTimer").boolValue = false;
            so.FindProperty("m_ShowResultPanels").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            WireHudButtons(hud, manager, null);
            AddRoutePresenter(quest, source);
            WireConnections(quest, decoy);
            FixPlayerCollider();
            PlaceDecoyNearRoute(decoy, tutorialRoute);
            MoveVolcanoWithinReach(tutorialRoute);

            var director = holder.AddComponent<TutorialDirector>();
            var panel = BuildTutorialPanel(out var panelText, out var finishPanel);
            var dso = new SerializedObject(director);
            dso.FindProperty("m_Quest").objectReferenceValue = quest;
            dso.FindProperty("m_Firefly").objectReferenceValue = Object.FindFirstObjectByType<GuideFirefly>();
            dso.FindProperty("m_Orb").objectReferenceValue = quest != null ? quest.Orb : null;
            dso.FindProperty("m_PanelRoot").objectReferenceValue = panel;
            dso.FindProperty("m_PanelText").objectReferenceValue = panelText;
            dso.FindProperty("m_FinishPanel").objectReferenceValue = finishPanel;
            dso.FindProperty("m_DecoyNode").objectReferenceValue = decoy;

            // Prefer the actual volcano for the "stay away" lesson, not one of the T-Rex hazards.
            var start = tutorialRoute.Count > 0 ? tutorialRoute[0].transform.position : Vector3.zero;
            var zones = Object.FindObjectsByType<DangerZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var volcano = zones.FirstOrDefault(z => z.name.IndexOf("Volcano", System.StringComparison.OrdinalIgnoreCase) >= 0)
                ?? zones.OrderBy(z => Vector3.Distance(z.transform.position, start)).FirstOrDefault();
            dso.FindProperty("m_Volcano").objectReferenceValue = volcano;
            dso.ApplyModifiedPropertiesWithoutUndo();

            WireTutorialButtons(finishPanel, director);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[DinoNet] Built " + path + " (2 nodes, gameplay-driven steps).");
        }

        // ------------------------------------------------------------- sandbox

        /// <summary>
        /// The free-play sandbox: the same world, but no quest or hazards. The dinosaur models are
        /// kept as hidden templates the sandbox controller copies when the child places a dino.
        /// </summary>
        [MenuItem("DinoNet/Build Sandbox Scene")]
        public static void BuildSandbox()
        {
            var path = k_SceneFolder + "Sandbox.unity";
            var scene = Duplicate(path);

            var templates = new GameObject("Sandbox Templates");
            var stego = MakeTemplate("Hub_A", "Template_Stego", templates.transform);
            var para = MakeTemplate("Hub_B", "Template_Parasaurolophus", templates.transform);
            var trike = MakeTemplate("Triceratops_EndUser", "Template_Triceratops", templates.transform);
            var raptor = MakeTemplate("Velociraptor", "Template_Raptor", templates.transform);
            var rex = MakeTemplate("BadNode_T-Rex_West", "Template_Rex", templates.transform);

            // Spare quest dinosaurs become wandering NPCs so the world stays lively.
            foreach (var node in Object.FindObjectsByType<QuestNode>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (node.transform.IsChildOf(templates.transform))
                    continue;

                ConvertToWanderingNpc(node);
            }

            foreach (var name in new[] { "DinoNet Systems", "Start Podium", "Data Packet Orb", "Quest Banner", "Guide Firefly" })
            {
                var go = FindByName(name);
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            // No hazards in free play: a calm place to experiment.
            foreach (var zone in Object.FindObjectsByType<DangerZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (zone != null)
                    Object.DestroyImmediate(zone.gameObject);
            }

            foreach (var bad in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (bad != null && bad.name.StartsWith("BadNode_") && !bad.IsChildOf(templates.transform))
                    Object.DestroyImmediate(bad.gameObject);
            }

            foreach (var legacy in Object.FindObjectsByType<NetworkNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(legacy);

            var atmosphere = new Atmosphere
            {
                name = "Sandbox",
                sun = new Color(1f, 0.95f, 0.85f), sunIntensity = 1.6f, sunAngles = new Vector3(55f, 20f, 0f),
                ambient = new Color(0.8f, 0.82f, 0.86f), fog = new Color(0.9f, 0.88f, 0.75f), fogDensity = 0.0035f,
                groundTint = new Color(0.5f, 0.54f, 0.4f), fill = new Color(1f, 0.97f, 0.9f), fillIntensity = 0.32f,
                skyboxAsset = "Assets/Materials/PrehistoricSky_Sandbox.mat",
            };
            ApplyAtmosphere(atmosphere, 7);
            ApplyGroundTint(7, atmosphere.groundTint);
            FixDinoMovement();
            AddEnvironmentColliders();
            EnsureEventSystem();
            FixPlayerCollider();

            var holder = new GameObject("Sandbox");
            var controller = holder.AddComponent<SandboxController>();
            var so = new SerializedObject(controller);
            so.FindProperty("m_TemplateStego").objectReferenceValue = stego;
            so.FindProperty("m_TemplatePara").objectReferenceValue = para;
            so.FindProperty("m_TemplateTrike").objectReferenceValue = trike;
            so.FindProperty("m_TemplateRaptor").objectReferenceValue = raptor;
            so.FindProperty("m_TemplateRex").objectReferenceValue = rex;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[DinoNet] Built " + path + ".");
        }

        static GameObject FindByName(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t != null && t.name == name)
                    return t.gameObject;
            }

            return null;
        }

        /// <summary>A hidden, behaviour-free copy of a dinosaur that the sandbox can stamp out.</summary>
        static GameObject MakeTemplate(string sourceName, string templateName, Transform parent)
        {
            var source = FindByName(sourceName);
            if (source == null)
            {
                Debug.LogWarning("[DinoNet] Sandbox template source not found: " + sourceName);
                return null;
            }

            var copy = Object.Instantiate(source);
            copy.name = templateName;
            copy.transform.SetParent(parent, true);

            foreach (var t in new System.Type[]
            {
                typeof(QuestNode), typeof(VisualFeedbackController), typeof(NodeArrivalReaction), typeof(RoadWanderer),
                typeof(NetworkNode), typeof(DinoAnimator),
                typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable),
            })
            {
                foreach (var c in copy.GetComponentsInChildren(t, true))
                    Object.DestroyImmediate(c);
            }

            // The delivery and vine anchors were only meaningful to the quest.
            foreach (var childName in new[] { "DeliveryAnchor", "VineAnchor", "ConnectionAnchor" })
            {
                var child = copy.transform.Find(childName);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }

            copy.SetActive(false);
            return copy;
        }

        // ------------------------------------------------------------- scene plumbing

        static Scene Duplicate(string destination)
        {
            if (File.Exists(destination))
                AssetDatabase.DeleteAsset(destination);

            AssetDatabase.CopyAsset(k_SourceScene, destination);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
            return EditorSceneManager.OpenScene(destination, OpenSceneMode.Single);
        }

        /// <summary>
        /// Cuts the 7-node Demo route down to this level's nodes. Surplus node dinosaurs are not
        /// thrown away - they become wandering NPCs, which fills the world with life and makes the
        /// point that not every dinosaur is part of the route. One can be held back as a decoy for
        /// the tutorial's wrong-node lesson.
        /// </summary>
        static List<QuestNode> PrepareRoute(string[] names, out QuestNode decoy, bool keepDecoy = false)
        {
            decoy = null;
            var all = Object.FindObjectsByType<QuestNode>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
            var keep = new List<QuestNode>();
            foreach (var n in names)
            {
                var node = all.FirstOrDefault(q => q.name == n);
                if (node != null)
                    keep.Add(node);
            }

            var surplus = all.Where(q => !keep.Contains(q)).ToList();

            if (keepDecoy && surplus.Count > 0 && keep.Count > 0)
            {
                // The decoy should be somewhere the child can plausibly wander to.
                var firstStop = keep[0].transform.position;
                decoy = surplus.OrderBy(q => Vector3.Distance(q.transform.position, firstStop)).First();
                surplus.Remove(decoy);
            }

            foreach (var node in surplus)
                ConvertToWanderingNpc(node);

            var quest = Object.FindFirstObjectByType<DinoQuestManager>();
            if (quest != null)
                SetList(quest, "m_Route", keep.Cast<Object>().ToList());

            var firefly = Object.FindFirstObjectByType<GuideFirefly>();
            if (firefly != null)
                SetList(firefly, "m_Route", keep.Cast<Object>().ToList());

            // The road-glow segments were authored against the full 7-node route; the grown
            // energy vines show the connections in these levels instead.
            var glow = Object.FindFirstObjectByType<RoadGlowController>();
            if (glow != null)
                glow.enabled = false;

            return keep;
        }

        /// <summary>
        /// Turns a spare node dinosaur into a wandering NPC: drops its node markers and gives it
        /// the same road-walking behaviour the other roaming dinos already use.
        /// </summary>
        static void ConvertToWanderingNpc(QuestNode node)
        {
            var go = node.gameObject;

            // Strip the things that made it look like a route node.
            foreach (var childName in new[] { "NodeRing", "CelebrationVfx", "DeliveryAnchor", "VineAnchor" })
            {
                var child = go.transform.Find(childName);
                if (child != null)
                    Object.DestroyImmediate(child.gameObject);
            }

            Object.DestroyImmediate(node);

            var reaction = go.GetComponent<NodeArrivalReaction>();
            if (reaction != null)
                Object.DestroyImmediate(reaction);

            var wanderer = go.GetComponent<RoadWanderer>();
            if (wanderer == null)
                wanderer = go.AddComponent<RoadWanderer>();

            var so = new SerializedObject(wanderer);
            so.FindProperty("m_Network").objectReferenceValue = Object.FindFirstObjectByType<RoadNetwork>();
            so.FindProperty("m_Animation").objectReferenceValue = go.GetComponent<Animation>();
            so.FindProperty("m_WalkSpeed").floatValue = Random.Range(0.8f, 1.5f);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetList(Object target, string property, List<Object> values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(property);
            prop.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ApplyAtmosphere(Atmosphere atmosphere, int level)
        {
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional)
                    continue;

                // The scene carries a key light plus a "Moonlight Fill"; both need restyling or
                // the fill flattens whatever mood the key light is set to.
                if (light.name.Contains("Moonlight") || light.name.Contains("Fill"))
                {
                    light.color = atmosphere.fill;
                    light.intensity = atmosphere.fillIntensity;
                }
                else
                {
                    light.color = atmosphere.sun;
                    light.intensity = atmosphere.sunIntensity;
                    light.transform.rotation = Quaternion.Euler(atmosphere.sunAngles);
                }
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = atmosphere.ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = atmosphere.fog;
            RenderSettings.fogDensity = atmosphere.fogDensity;

            // Some levels reuse an authored sky asset (shared read-only, never modified here).
            if (!string.IsNullOrEmpty(atmosphere.skyboxAsset))
            {
                var authored = AssetDatabase.LoadAssetAtPath<Material>(atmosphere.skyboxAsset);
                if (authored != null)
                {
                    RenderSettings.skybox = authored;
                    return;
                }

                Debug.LogWarning("[DinoNet] Sky asset not found: " + atmosphere.skyboxAsset + " - falling back to a generated sky.");
            }

            // Each level gets its own sky material so restyling one never touches another.
            var skyPath = "Assets/Materials/Sky_Level" + level + ".mat";
            Directory.CreateDirectory(Path.GetDirectoryName(Application.dataPath + "/../" + skyPath));
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, skyPath);
            }

            sky.SetColor("_SkyTint", atmosphere.skyTint);
            sky.SetColor("_GroundColor", atmosphere.groundTint);
            sky.SetFloat("_Exposure", atmosphere.skyExposure);
            sky.SetFloat("_AtmosphereThickness", level == 3 ? 1.8f : 1.0f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
        }

        /// <summary>
        /// Gives each level its own route shape by standing the node dinosaurs on different road
        /// junctions. Placing them *on* junctions means the dirt roads really do join the nodes and
        /// <see cref="GuideFirefly"/> keeps pathing correctly, since it snaps to the nearest junction.
        /// </summary>
        static void PlaceNodesOnJunctions(int level, List<QuestNode> route)
        {
            var network = Object.FindFirstObjectByType<RoadNetwork>();
            var podium = GameObject.Find("Start Podium");
            if (network == null || podium == null || route.Count == 0)
                return;

            var start = podium.transform.position;

            // Hazard areas are off limits for a node the child has to walk to.
            var hazards = Object.FindObjectsByType<DangerZone>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Select(z => (centre: z.transform.position, radius: new SerializedObject(z).FindProperty("m_Radius").floatValue + 3f))
                .ToList();

            bool Safe(Vector3 p) => hazards.All(h => Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(h.centre.x, 0f, h.centre.z)) > h.radius);

            var candidates = network.Junctions
                .Where(j => j.wanderable)
                .Select(j => j.position)
                .Where(p =>
                {
                    var flat = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(start.x, 0f, start.z));
                    return flat > 5f && flat < 26f && Safe(p);
                })
                .ToList();

            if (candidates.Count < route.Count)
            {
                Debug.LogWarning("[DinoNet] Level " + level + ": only " + candidates.Count + " usable junctions for " + route.Count + " nodes - leaving node positions as they are.");
                return;
            }

            // Deterministic per level, so every rebuild reproduces the same layout.
            Random.InitState(level * 7919 + 13);
            var shuffled = candidates.OrderBy(_ => Random.value).ToList();

            var chosen = new List<Vector3>();
            foreach (var p in shuffled)
            {
                if (chosen.Count == route.Count)
                    break;

                // Keep nodes clearly apart so their rings never overlap and the route stays readable.
                if (chosen.All(c => Vector3.Distance(c, p) > 7f))
                    chosen.Add(p);
            }

            if (chosen.Count < route.Count)
            {
                Debug.LogWarning("[DinoNet] Level " + level + ": could not space " + route.Count + " nodes apart - leaving positions as they are.");
                return;
            }

            // Walking outward from the podium reads as a journey rather than a random hop order.
            chosen = chosen.OrderBy(c => Vector3.Distance(c, start)).ToList();

            var previous = start;
            for (var i = 0; i < route.Count; i++)
            {
                var node = route[i];
                var target = chosen[i];
                node.transform.position = new Vector3(target.x, node.transform.position.y, target.z);

                var look = previous - node.transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f)
                    node.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

                SnapToGround(node.gameObject);
                previous = node.transform.position;
            }

            Debug.Log("[DinoNet] Level " + level + " node layout: " + string.Join(" -> ", route.Select(n => n.name + n.transform.position.ToString("F0"))));
        }

        static void SnapToGround(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return;

            var b = renderers[0].bounds;
            foreach (var r in renderers)
                b.Encapsulate(r.bounds);

            go.transform.position += Vector3.up * (0f - b.min.y);
        }

        /// <summary>Tints the ground differently per level, using a material variant so no scene shares it.</summary>
        static void ApplyGroundTint(int level, Color tint)
        {
            foreach (var name in new[] { "Ground Tint", "Forest Floor" })
            {
                var go = GameObject.Find(name);
                var renderer = go != null ? go.GetComponent<Renderer>() : null;
                if (renderer == null || renderer.sharedMaterial == null)
                    continue;

                var path = "Assets/Materials/" + name.Replace(" ", "") + "_Level" + level + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(renderer.sharedMaterial);
                    AssetDatabase.CreateAsset(mat, path);
                }

                mat.SetColor("_BaseColor", tint);
                EditorUtility.SetDirty(mat);
                renderer.sharedMaterial = mat;
            }
        }

        /// <summary>Varies the greenery per level so the four worlds don't look identical.</summary>
        static void ThinEnvironment(int level)
        {
            var forest = GameObject.Find("Twilight Forest");
            if (forest == null)
                return;

            Random.InitState(level * 977);

            // Every level thins differently, so no two clearings have the same shape - and Level 4
            // gets a real pass too rather than being left identical to Demo.
            var treeKeep = level switch { 1 => 0.50f, 2 => 0.80f, 3 => 0.60f, _ => 0.70f };
            var brushKeep = level switch { 1 => 0.45f, 2 => 0.85f, 3 => 0.55f, _ => 0.75f };

            // Each level opens up a different side of the map, so the clearing itself differs.
            var openDirection = (level switch
            {
                1 => new Vector2(0f, 1f),
                2 => new Vector2(1f, 0.3f),
                3 => new Vector2(-1f, 0.4f),
                _ => new Vector2(0f, -1f),
            }).normalized;

            var cleared = 0;
            foreach (Transform group in forest.transform)
            {
                var isTrees = group.name == "Trees";
                var isBrush = group.name == "Undergrowth" || group.name == "Rocks & Logs";
                if (!isTrees && !isBrush)
                    continue;

                foreach (Transform child in group)
                {
                    var flat = new Vector2(child.position.x, child.position.z);

                    // Keep the outer ring so the play area always feels enclosed.
                    if (flat.magnitude > 24f)
                    {
                        child.gameObject.SetActive(true);
                        continue;
                    }

                    // Clear harder along this level's open direction to carve a distinct clearing.
                    var alignment = Mathf.Clamp01(Vector2.Dot(flat.normalized, openDirection));
                    var keep = (isTrees ? treeKeep : brushKeep) * Mathf.Lerp(1f, 0.35f, alignment);

                    var active = Random.value <= keep;
                    child.gameObject.SetActive(active);
                    if (!active)
                        cleared++;
                }
            }

            Debug.Log("[DinoNet] Level " + level + " cleared " + cleared + " foliage objects.");
        }

        /// <summary>
        /// Turns on real obstacle avoidance and fits each dinosaur's capsule to its body, so
        /// wandering dinos stop walking through each other, the nodes and the scenery.
        /// </summary>
        static void FixDinoMovement()
        {
            foreach (var wanderer in Object.FindObjectsByType<RoadWanderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var radius = FitBodyCollider(wanderer.gameObject);
                var so = new SerializedObject(wanderer);
                so.FindProperty("m_AvoidObstacles").boolValue = true;
                so.FindProperty("m_BodyRadius").floatValue = radius;
                so.FindProperty("m_LookAhead").floatValue = Mathf.Max(1.6f, radius * 3.5f);
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            // Node dinosaurs and the idle scenery dinos need bodies too, or wanderers walk
            // straight through them.
            foreach (var node in Object.FindObjectsByType<QuestNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                FitBodyCollider(node.gameObject);

            foreach (var anim in Object.FindObjectsByType<Animation>(FindObjectsSortMode.None))
            {
                if (anim.GetComponent<RoadWanderer>() == null && anim.GetComponent<QuestNode>() == null)
                    FitBodyCollider(anim.gameObject);
            }
        }

        /// <summary>
        /// The environment models are all imported with addColliders off, so only the forest trees
        /// had colliders and everything else could be walked through. This gives the solid props a
        /// body so dinosaurs (and the player) go around them.
        /// Undergrowth and the pond are deliberately left alone - you walk through grass and ferns.
        /// </summary>
        static int AddEnvironmentColliders()
        {
            var added = 0;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            var forest = GameObject.Find("Twilight Forest");
            if (forest != null)
            {
                var rocks = forest.transform.Find("Rocks & Logs");
                if (rocks != null)
                {
                    foreach (Transform child in rocks)
                        added += AddPropCollider(child.gameObject) ? 1 : 0;
                }
            }

            // The older root-level props carried over from the original environment.
            string[] solidPrefixes = { "Rock_", "WoodLog", "TreeStump", "BushBerries_", "Cactus_", "CommonTree_", "BirchTree_" };
            foreach (var root in scene.GetRootGameObjects())
            {
                if (solidPrefixes.Any(p => root.name.StartsWith(p)))
                    added += AddPropCollider(root) ? 1 : 0;
            }

            // The volcano gets an exact mesh collider - a box round its bounds would wall off far
            // more ground than the cone actually occupies.
            var volcano = GameObject.Find("Volcano by Poly by Google - 8gkFBBcS6aM");
            if (volcano != null && volcano.GetComponent<Collider>() == null)
            {
                var filter = volcano.GetComponentInChildren<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    var mc = volcano.AddComponent<MeshCollider>();
                    mc.sharedMesh = filter.sharedMesh;
                    // Convex so the avoidance code's Collider.ClosestPoint works on it; a hull of
                    // the cone is plenty accurate for walking around.
                    mc.convex = true;
                    added++;
                }
            }

            return added;
        }

        /// <summary>
        /// Now that the props are solid, a dinosaur's authored start spot may sit inside one.
        /// Nudges any that do out to the nearest clear patch so nothing begins the level embedded
        /// in a rock.
        /// </summary>
        static void ClearSpawnOverlaps()
        {
            foreach (var wanderer in Object.FindObjectsByType<RoadWanderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var capsule = wanderer.GetComponent<CapsuleCollider>();
                if (capsule == null)
                    continue;

                var radius = capsule.radius * Mathf.Max(Mathf.Abs(wanderer.transform.lossyScale.x), Mathf.Abs(wanderer.transform.lossyScale.z));
                var start = wanderer.transform.position;
                if (IsClear(start, radius, wanderer.transform))
                    continue;

                // Spiral outwards for the closest spot that fits.
                var moved = false;
                for (var ring = 1; ring <= 6 && !moved; ring++)
                {
                    for (var step = 0; step < 12; step++)
                    {
                        var angle = step * 30f * Mathf.Deg2Rad;
                        var candidate = start + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (ring * radius * 1.5f);
                        if (!IsClear(candidate, radius, wanderer.transform))
                            continue;

                        wanderer.transform.position = candidate;
                        Debug.Log("[DinoNet] Moved " + wanderer.name + " out of scenery to " + candidate.ToString("F1"));
                        moved = true;
                        break;
                    }
                }
            }
        }

        static bool IsClear(Vector3 position, float radius, Transform self)
        {
            var chest = position + Vector3.up * radius;
            foreach (var hit in Physics.OverlapSphere(chest, radius, 1, QueryTriggerInteraction.Ignore))
            {
                if (hit.transform.IsChildOf(self) || hit.bounds.size.y < 0.35f)
                    continue;

                return false;
            }

            return true;
        }

        /// <summary>Fits a box collider to a prop's mesh in its own local space, so rotation is respected.</summary>
        static bool AddPropCollider(GameObject go)
        {
            if (go.GetComponentInChildren<Collider>() != null)
                return false;

            var filters = go.GetComponentsInChildren<MeshFilter>();
            if (filters.Length == 0)
                return false;

            var toLocal = go.transform.worldToLocalMatrix;
            var has = false;
            var bounds = new Bounds();

            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null)
                    continue;

                var mb = filter.sharedMesh.bounds;
                var toWorld = filter.transform.localToWorldMatrix;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3(
                        (corner & 1) == 0 ? mb.min.x : mb.max.x,
                        (corner & 2) == 0 ? mb.min.y : mb.max.y,
                        (corner & 4) == 0 ? mb.min.z : mb.max.z);
                    var local = toLocal.MultiplyPoint3x4(toWorld.MultiplyPoint3x4(point));
                    if (!has) { bounds = new Bounds(local, Vector3.zero); has = true; }
                    else bounds.Encapsulate(local);
                }
            }

            if (!has)
                return false;

            // Anything flatter than the movement code's floor threshold would be ignored anyway.
            var worldHeight = bounds.size.y * Mathf.Abs(go.transform.lossyScale.y);
            if (worldHeight < 0.35f)
                return false;

            var box = go.AddComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size * 0.9f;   // slightly inside the silhouette so it never feels bigger than it looks
            return true;
        }

        /// <summary>Resizes the object's capsule to match the visible body. Returns its radius.</summary>
        static float FitBodyCollider(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return 0.5f;

            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            var capsule = go.GetComponent<CapsuleCollider>();
            if (capsule == null)
                capsule = go.AddComponent<CapsuleCollider>();

            // Torso-sized rather than nose-to-tail, so animals can still pass side by side.
            var worldRadius = Mathf.Clamp(Mathf.Min(bounds.size.x, bounds.size.z) * 0.45f, 0.25f, 1.2f);
            var worldHeight = Mathf.Max(bounds.size.y, worldRadius * 2f);
            var scale = go.transform.lossyScale;
            var horizontal = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));

            capsule.direction = 1;
            capsule.center = go.transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y + worldHeight * 0.5f, bounds.center.z));
            capsule.radius = worldRadius / Mathf.Max(horizontal, 0.0001f);
            capsule.height = worldHeight / Mathf.Max(Mathf.Abs(scale.y), 0.0001f);
            capsule.isTrigger = false;
            return worldRadius;
        }

        /// <summary>
        /// Points the quest manager at the road graph and the floor-strand prefab, so each
        /// connection is laid along the real roads the moment a node is reached.
        /// </summary>
        static void WireConnections(DinoQuestManager quest, QuestNode decoy)
        {
            if (quest == null)
                return;

            var so = new SerializedObject(quest);
            so.FindProperty("m_Roads").objectReferenceValue = Object.FindFirstObjectByType<RoadNetwork>();
            so.FindProperty("m_ConnectionPrefab").objectReferenceValue = LoadOrCreateConnectionPrefab();
            so.FindProperty("m_ConnectionMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/Twilight/EnergyRoad.mat");

            // The old above-floor vine is gone; connections are floor strands now.
            var legacyVine = so.FindProperty("m_VinePrefab");
            if (legacyVine != null)
                legacyVine.objectReferenceValue = null;

            var decoys = so.FindProperty("m_DecoyNodes");
            decoys.arraySize = decoy != null ? 1 : 0;
            if (decoy != null)
                decoys.GetArrayElementAtIndex(0).objectReferenceValue = decoy;

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static NetworkConnection LoadOrCreateConnectionPrefab()
        {
            const string path = "Assets/Prefabs/NetworkConnection.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                return existing.GetComponent<NetworkConnection>();

            var temp = new GameObject("NetworkConnection");
            var line = temp.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.alignment = LineAlignment.TransformZ;
            line.widthMultiplier = 0.55f;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Twilight/EnergyRoad.mat");
            temp.AddComponent<NetworkConnection>();

            var saved = PrefabUtility.SaveAsPrefabAsset(temp, path);
            Object.DestroyImmediate(temp);
            return saved.GetComponent<NetworkConnection>();
        }

        /// <summary>Gives every node dinosaur a happy hop for when the message reaches it.</summary>
        static void AddArrivalReactions()
        {
            foreach (var node in Object.FindObjectsByType<QuestNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var reaction = node.GetComponent<NodeArrivalReaction>();
                if (reaction == null)
                    reaction = node.gameObject.AddComponent<NodeArrivalReaction>();

                var so = new SerializedObject(node);
                so.FindProperty("m_Reaction").objectReferenceValue = reaction;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// Gives the guide firefly a short glowing trail, so it reads as something tracing a
        /// route for the message rather than a floating spark.
        /// </summary>
        static void AddFireflyTrail()
        {
            var firefly = Object.FindFirstObjectByType<GuideFirefly>();
            if (firefly == null)
                return;

            var trail = firefly.GetComponent<TrailRenderer>();
            if (trail == null)
                trail = firefly.gameObject.AddComponent<TrailRenderer>();

            trail.time = 1.1f;
            trail.startWidth = 0.09f;
            trail.endWidth = 0.0f;
            trail.minVertexDistance = 0.08f;
            trail.numCapVertices = 4;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;

            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/VineGlow.mat");
            if (mat != null)
                trail.sharedMaterial = mat;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(1f, 0.95f, 0.5f), 0f), new GradientColorKey(new Color(0.5f, 1f, 0.6f), 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) });
            trail.colorGradient = gradient;
        }

        /// <summary>
        /// The quest and danger banners are switched off by their own scripts at runtime, but
        /// they sit visible in the editor. Start them hidden so the scene reads cleanly.
        /// </summary>
        static void TidyEditModeBanners()
        {
            var quest = GameObject.Find("Quest Banner");
            if (quest != null)
                quest.SetActive(false);

            foreach (var zone in Object.FindObjectsByType<DangerZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var banner = zone.transform.Find("Warning Banner");
                if (banner != null)
                    banner.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// In the tutorial only, brings the volcano and its hazard ring within a short walk so the
        /// "stay away" lesson is actually reachable. Skipped if no safe spot clears the route.
        /// </summary>
        static void MoveVolcanoWithinReach(List<QuestNode> route)
        {
            var zone = Object.FindObjectsByType<DangerZone>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(z => z.name.IndexOf("Volcano", System.StringComparison.OrdinalIgnoreCase) >= 0);
            var mesh = GameObject.Find("Volcano by Poly by Google - 8gkFBBcS6aM");
            var podium = GameObject.Find("Start Podium");
            if (zone == null || podium == null)
                return;

            var radius = new SerializedObject(zone).FindProperty("m_Radius").floatValue;
            var start = podium.transform.position;

            // Try a few spots around the podium and take the first that clears every node.
            for (var angle = 0; angle < 360; angle += 30)
            {
                var rad = angle * Mathf.Deg2Rad;
                var candidate = start + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * (radius + 6f);

                var clearsNodes = route.All(n => Vector3.Distance(
                    new Vector3(candidate.x, 0f, candidate.z),
                    new Vector3(n.transform.position.x, 0f, n.transform.position.z)) > radius + 5f);
                var clearsPodium = Vector3.Distance(candidate, start) > radius + 3f;

                if (!clearsNodes || !clearsPodium)
                    continue;

                var delta = new Vector3(candidate.x - zone.transform.position.x, 0f, candidate.z - zone.transform.position.z);
                zone.transform.position += delta;
                if (mesh != null)
                {
                    mesh.transform.position += delta;
                    SnapToGround(mesh);
                }

                Debug.Log("[DinoNet] Tutorial volcano moved to " + zone.transform.position.ToString("F1")
                    + " (" + Vector3.Distance(zone.transform.position, start).ToString("F1") + "m from the podium).");
                return;
            }

            Debug.LogWarning("[DinoNet] No clear spot for the tutorial volcano - left where it was.");
        }

        /// <summary>Stands the decoy dinosaur within easy reach of the route so the lesson is quick.</summary>
        static void PlaceDecoyNearRoute(QuestNode decoy, List<QuestNode> route)
        {
            if (decoy == null || route.Count == 0)
                return;

            var podium = GameObject.Find("Start Podium");
            var from = podium != null ? podium.transform.position : Vector3.zero;
            var to = route[0].transform.position;

            // Off to one side of the walk between the podium and the first real node.
            var along = to - from;
            along.y = 0f;
            var side = Vector3.Cross(along.normalized, Vector3.up);
            var spot = from + along * 0.55f + side * 5f;

            decoy.transform.position = new Vector3(spot.x, decoy.transform.position.y, spot.z);
            var look = from - decoy.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                decoy.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

            SnapToGround(decoy.gameObject);
            Debug.Log("[DinoNet] Tutorial decoy " + decoy.name + " at " + decoy.transform.position.ToString("F1"));
        }

        /// <summary>
        /// The rig ships with a 0.1m capsule, which is thin enough to slip between a dinosaur's
        /// legs. Widens it so the player bumps into things, without being so fat they snag.
        /// </summary>
        static void FixPlayerCollider()
        {
            var xr = GameObject.Find("Complete XR Origin Set Up Variant");
            var controller = xr != null ? xr.GetComponent<CharacterController>() : null;
            if (controller == null)
                return;

            controller.radius = 0.28f;
            controller.skinWidth = 0.03f;
            controller.stepOffset = 0.4f;
            controller.detectCollisions = true;
        }

        /// <summary>Stands the source dinosaur beside the podium so the packet visibly comes from it.</summary>
        static Renderer SetUpSourceDino()
        {
            var source = GameObject.Find("Volcano_Source");
            var podium = GameObject.Find("Start Podium");
            if (source == null || podium == null)
                return null;

            var offset = podium.transform.position + new Vector3(-1.8f, 0f, 0.6f);
            source.transform.position = new Vector3(offset.x, source.transform.position.y, offset.z);

            var look = podium.transform.position - source.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f)
                source.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);

            // A warm ring marks it as the sender, distinct from the blue destination rings.
            var ringName = "SourceRing";
            var ring = source.transform.Find(ringName);
            if (ring == null)
            {
                var prim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                prim.name = ringName;
                Object.DestroyImmediate(prim.GetComponent<Collider>());
                prim.transform.SetParent(source.transform, false);
                ring = prim.transform;
            }

            ring.position = new Vector3(source.transform.position.x, 0.04f, source.transform.position.z);
            ring.rotation = Quaternion.identity;
            var s = source.transform.lossyScale;
            ring.localScale = new Vector3(3f / s.x, 0.02f / s.y, 3f / s.z);

            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/SourceRing.mat");
            if (mat == null)
            {
                AssetDatabase.CopyAsset("Assets/NodeRing.mat", "Assets/SourceRing.mat");
                AssetDatabase.ImportAsset("Assets/SourceRing.mat");
                mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/SourceRing.mat");
            }

            mat.SetColor("_BaseColor", new Color(1f, 0.82f, 0.35f, 0.45f));
            mat.SetColor("_EmissionColor", new Color(1f, 0.8f, 0.35f) * 1.6f);
            EditorUtility.SetDirty(mat);

            var renderer = ring.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            return renderer;
        }

        static void AddRoutePresenter(DinoQuestManager quest, Renderer sourceRing)
        {
            if (quest == null)
                return;

            var presenter = quest.gameObject.GetComponent<RoutePresenter>();
            if (presenter == null)
                presenter = quest.gameObject.AddComponent<RoutePresenter>();

            // No route preview any more - the firefly is the hint, so only the sender is lit.
            var so = new SerializedObject(presenter);
            so.FindProperty("m_Quest").objectReferenceValue = quest;
            so.FindProperty("m_SourceHighlight").objectReferenceValue = sourceRing;
            so.FindProperty("m_SourceAnchor").objectReferenceValue = sourceRing != null ? sourceRing.transform : null;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ UI

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return;

            var go = new GameObject("EventSystem");
            go.AddComponent<UnityEngine.EventSystems.EventSystem>();
            go.AddComponent<XRUIInputModule>();
        }

        public static LevelHud BuildHud(out GameObject systemsHolder)
        {
            EnsureEventSystem();

            systemsHolder = GameObject.Find("DinoNet Systems");
            if (systemsHolder == null)
                systemsHolder = new GameObject("DinoNet Systems");

            var cam = Camera.main;
            var hudRoot = new GameObject("Level HUD");
            hudRoot.transform.SetParent(cam.transform, false);
            hudRoot.transform.localPosition = new Vector3(0f, 0f, 2f);
            hudRoot.transform.localRotation = Quaternion.identity;

            var canvas = hudRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = hudRoot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1600f, 900f);
            rt.localScale = Vector3.one * 0.001f;

            var hud = systemsHolder.AddComponent<LevelHud>();

            // Progress, top-left.
            var progressPanel = Panel(rt, "Progress", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(420f, 110f));
            var progressLabel = Label(progressPanel, "Label", "0 / 0 nodes", 34, TextAlignmentOptions.TopLeft, new Vector4(20f, 8f, 20f, 8f));
            var barBg = new GameObject("BarBackground", typeof(Image));
            barBg.transform.SetParent(progressPanel, false);
            var barBgRt = barBg.GetComponent<RectTransform>();
            barBgRt.anchorMin = new Vector2(0f, 0f); barBgRt.anchorMax = new Vector2(1f, 0f);
            barBgRt.pivot = new Vector2(0.5f, 0f);
            barBgRt.anchoredPosition = new Vector2(0f, 16f);
            barBgRt.sizeDelta = new Vector2(-40f, 34f);
            barBg.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

            var fill = new GameObject("BarFill", typeof(Image));
            fill.transform.SetParent(barBg.transform, false);
            var fillRt = fill.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 3f); fillRt.offsetMax = new Vector2(-3f, -3f);
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = new Color(0.35f, 1f, 0.5f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = 0f;

            // Timer, top-right (opposite corner).
            var timerPanel = Panel(rt, "Timer", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -30f), new Vector2(240f, 110f));
            var timerLabel = Label(timerPanel, "Label", "5:00", 60, TextAlignmentOptions.Center, new Vector4(10f, 6f, 10f, 6f));

            var completePanel = BuildResultPanel(cam.transform, "Level Complete Panel", "Level Complete!", new Color(0.05f, 0.3f, 0.15f, 0.92f),
                new[] { "Next Level", "Play Again", "Return to Main Menu" }, out var completeButtons, out _,
                height: 1020f, firstButtonY: -626f);
            var failedPanel = BuildResultPanel(cam.transform, "Level Failed Panel", "Level Failed!", new Color(0.35f, 0.06f, 0.05f, 0.92f),
                new[] { "Try Again", "Return to Main Menu" }, out var failedButtons, out var failedReason);

            var hso = new SerializedObject(hud);
            hso.FindProperty("m_HudRoot").objectReferenceValue = hudRoot;
            hso.FindProperty("m_ProgressFill").objectReferenceValue = fillImage;
            hso.FindProperty("m_ProgressLabel").objectReferenceValue = progressLabel;
            hso.FindProperty("m_TimerRoot").objectReferenceValue = timerPanel.gameObject;
            hso.FindProperty("m_TimerLabel").objectReferenceValue = timerLabel;
            hso.FindProperty("m_CompletePanel").objectReferenceValue = completePanel;
            hso.FindProperty("m_FailedPanel").objectReferenceValue = failedPanel;
            hso.FindProperty("m_FailedReason").objectReferenceValue = failedReason;
            hso.FindProperty("m_NextLevelButton").objectReferenceValue = completeButtons[0].gameObject;
            hso.ApplyModifiedPropertiesWithoutUndo();

            hud.gameObject.SetActive(true);
            completePanel.SetActive(false);
            failedPanel.SetActive(false);

            s_CompleteButtons = completeButtons;
            s_FailedButtons = failedButtons;
            return hud;
        }

        static Button[] s_CompleteButtons;
        static Button[] s_FailedButtons;

        static void WireHudButtons(LevelHud hud, LevelManager manager, TutorialDirector tutorial)
        {
            if (s_CompleteButtons != null && s_CompleteButtons.Length == 3)
            {
                Bind(s_CompleteButtons[0], manager, "NextLevel");
                Bind(s_CompleteButtons[1], manager, "Retry");
                Bind(s_CompleteButtons[2], manager, "ReturnToMainMenu");
            }

            if (s_FailedButtons != null && s_FailedButtons.Length == 2)
            {
                Bind(s_FailedButtons[0], manager, "Retry");
                Bind(s_FailedButtons[1], manager, "ReturnToMainMenu");
            }
        }

        static void Bind(Button button, Object target, string method)
        {
            var so = new SerializedObject(button);
            var calls = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            calls.arraySize = 1;
            var call = calls.GetArrayElementAtIndex(0);
            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = target.GetType().AssemblyQualifiedName;
            call.FindPropertyRelative("m_MethodName").stringValue = method;
            call.FindPropertyRelative("m_Mode").enumValueIndex = 1; // void
            call.FindPropertyRelative("m_CallState").enumValueIndex = 2; // runtime only
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject BuildResultPanel(Transform parent, string name, string title, Color colour, string[] buttonLabels,
            out Button[] buttons, out TMP_Text reasonLabel, float height = 620f, float firstButtonY = -325f)
        {
            // The canvas lives under a plain-Transform anchor. Moving a RectTransform directly is
            // unreliable - its position setter routes through anchoredPosition and silently drops
            // the vertical offset - so callers position this anchor instead.
            var anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.localPosition = Vector3.zero;
            anchor.transform.localRotation = Quaternion.identity;

            var root = new GameObject(name + " Canvas");
            root.transform.SetParent(anchor.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, 2.2f);
            root.transform.localRotation = Quaternion.identity;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900f, height);
            rt.localScale = Vector3.one * 0.0016f;

            var bg = new GameObject("Background", typeof(Image));
            bg.transform.SetParent(root.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = colour;

            var titleLabel = Label(root.transform, "Title", title, 76, TextAlignmentOptions.Center, new Vector4(0, 0, 0, 0));
            var titleRt = titleLabel.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f); titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -40f);
            titleRt.sizeDelta = new Vector2(-40f, 120f);

            reasonLabel = Label(root.transform, "Reason", "", 44, TextAlignmentOptions.Center, new Vector4(0, 0, 0, 0));
            var reasonRt = reasonLabel.GetComponent<RectTransform>();
            reasonRt.anchorMin = new Vector2(0f, 1f); reasonRt.anchorMax = new Vector2(1f, 1f);
            reasonRt.pivot = new Vector2(0.5f, 1f);
            reasonRt.anchoredPosition = new Vector2(0f, -165f);
            reasonRt.sizeDelta = new Vector2(-60f, 120f);   // room for two wrapped lines

            buttons = new Button[buttonLabels.Length];
            for (var i = 0; i < buttonLabels.Length; i++)
                buttons[i] = BuildButton(root.transform, buttonLabels[i], new Vector2(0f, firstButtonY - i * 105f));

            return anchor;
        }

        static Button BuildButton(Transform parent, string text, Vector2 anchored)
        {
            var go = new GameObject(text + " Button", typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = new Vector2(620f, 88f);

            var image = go.GetComponent<Image>();
            image.color = new Color(1f, 1f, 1f, 0.92f);

            var button = go.GetComponent<Button>();
            button.targetGraphic = image;

            var label = Label(go.transform, "Label", text, 40, TextAlignmentOptions.Center, Vector4.zero);
            label.color = new Color(0.08f, 0.1f, 0.12f);
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            return button;
        }

        static Transform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.pivot = new Vector2(anchorMin.x, anchorMax.y);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.42f);
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
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            return tmp;
        }

        // --------------------------------------------------------------- tutorial UI

        static GameObject BuildTutorialPanel(out TMP_Text text, out GameObject finishPanel)
        {
            var cam = Camera.main;
            var root = new GameObject("Tutorial Panel");
            root.transform.SetParent(cam.transform, false);
            root.transform.localPosition = new Vector3(0f, -0.35f, 2f);
            root.transform.localRotation = Quaternion.identity;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(1100f, 220f);
            rt.localScale = Vector3.one * 0.0012f;

            var bg = new GameObject("Background", typeof(Image));
            bg.transform.SetParent(root.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.06f, 0.12f, 0.2f, 0.88f);

            text = Label(root.transform, "Text", "", 52, TextAlignmentOptions.Center, new Vector4(30f, 20f, 30f, 20f));

            finishPanel = BuildResultPanel(cam.transform, "Tutorial Finish Panel", "Tutorial Complete!", new Color(0.06f, 0.24f, 0.36f, 0.94f),
                new[] { "Yes, Let's Play", "Play Tutorial Again", "Yes, But Return to Main Menu" }, out s_TutorialButtons, out _);
            finishPanel.SetActive(false);
            return root;
        }

        static Button[] s_TutorialButtons;

        static void WireTutorialButtons(GameObject finishPanel, TutorialDirector director)
        {
            if (s_TutorialButtons == null || s_TutorialButtons.Length != 3)
                return;

            Bind(s_TutorialButtons[0], director, "PlayLevelOne");
            Bind(s_TutorialButtons[1], director, "RestartTutorial");
            Bind(s_TutorialButtons[2], director, "ReturnToMainMenu");
        }

        // -------------------------------------------------------------- main menu

        public static void BuildMainMenu()
        {
            var path = k_SceneFolder + "MainMenu.unity";
            var scene = Duplicate(path);

            // The menu keeps the world as a backdrop but none of the gameplay systems.
            foreach (var name in new[] { "DinoNet Systems", "Start Podium", "Data Packet Orb", "Quest Banner", "Guide Firefly" })
            {
                var go = GameObject.Find(name);
                if (go != null)
                    Object.DestroyImmediate(go);
            }

            foreach (var node in Object.FindObjectsByType<QuestNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(node);

            // These belong to the original prototype and expect a topology manager that the
            // menu scene no longer has, so they'd throw on load.
            foreach (var legacy in Object.FindObjectsByType<NetworkNode>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(legacy);

            // A calm backdrop: no hazard areas or their warning banners in the menu.
            foreach (var zone in Object.FindObjectsByType<DangerZone>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(zone.gameObject);

            ApplyAtmosphere(k_Atmospheres[1], 1);
            FixDinoMovement();
            AddEnvironmentColliders();
            ClearSpawnOverlaps();
            EnsureEventSystem();

            var holder = new GameObject("Main Menu");
            var controller = holder.AddComponent<MainMenuController>();

            // Built at the scene root, never parented to the camera: re-parenting a RectTransform
            // with worldPositionStays writes the offset into anchoredPosition in canvas units, which
            // previously flung the panel ~851m into the sky. MainMenuController places it in front
            // of the real headset on the first frame instead.
            var panel = BuildResultPanel(null, "Main Menu Panel", "Dino Net", new Color(0.05f, 0.18f, 0.3f, 0.93f),
                new[] { "Play Game", "Choose Level", "My Progress", "Play Tutorial", "Sandbox", "Exit" }, out var buttons, out var subtitle,
                height: 960f, firstButtonY: -330f);
            subtitle.text = "Help messages travel through the dino network!";
            panel.SetActive(true);

            var cam = Camera.main;
            if (cam != null)
            {
                var forward = cam.transform.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f)
                    forward = Vector3.forward;
                forward.Normalize();
                // The anchor sits at the head; its canvas child carries the 2.2m forward offset.
                panel.transform.position = cam.transform.position + Vector3.down * 0.15f;
                panel.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            }

            var cso = new SerializedObject(controller);
            cso.FindProperty("m_Panel").objectReferenceValue = panel.transform;
            cso.ApplyModifiedPropertiesWithoutUndo();

            var select = holder.AddComponent<LevelSelectMenu>();
            var sso = new SerializedObject(select);
            sso.FindProperty("m_MainPanel").objectReferenceValue = panel;
            sso.ApplyModifiedPropertiesWithoutUndo();

            Bind(buttons[0], controller, "PlayGame");
            Bind(buttons[1], select, "OpenLevels");
            Bind(buttons[2], select, "OpenProgress");
            Bind(buttons[3], controller, "PlayTutorial");
            Bind(buttons[4], controller, "PlaySandbox");
            Bind(buttons[5], controller, "ExitGame");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[DinoNet] Built " + path + ".");
        }

        // ---------------------------------------------------------- build settings

        public static void RegisterBuildScenes()
        {
            var order = new[]
            {
                k_SceneFolder + "MainMenu.unity",
                k_SceneFolder + "Tutorial.unity",
                k_SceneFolder + "Level1.unity",
                k_SceneFolder + "Level2.unity",
                k_SceneFolder + "Level3.unity",
                k_SceneFolder + "Level4.unity",
                k_SceneFolder + "Level5.unity",
                k_SceneFolder + "Level6.unity",
                k_SceneFolder + "Level7.unity",
                k_SceneFolder + "Level8.unity",
                k_SceneFolder + "Level9.unity",
                k_SceneFolder + "Level10.unity",
                k_SceneFolder + "Sandbox.unity",
                k_SourceScene,
            };

            EditorBuildSettings.scenes = order
                .Where(File.Exists)
                .Select(p => new EditorBuildSettingsScene(p, true))
                .ToArray();

            Debug.Log("[DinoNet] Build settings: " + string.Join(", ", EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path))));
        }
    }
}
