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
        }

        /// <summary>Route node names per level, in visit order. Level index = nodes - 1.</summary>
        static readonly Dictionary<int, string[]> k_Routes = new Dictionary<int, string[]>
        {
            { 1, new[] { "Hub_A", "Triceratops_EndUser" } },
            { 2, new[] { "Hub_A", "Hub_B", "Triceratops_EndUser" } },
            { 3, new[] { "Hub_A", "Hub_B", "Node_C", "Triceratops_EndUser" } },
            { 4, new[] { "Hub_A", "Hub_B", "Node_C", "Node_D", "Triceratops_EndUser" } },
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

            { 3, new Atmosphere { name = "Sunset", sun = new Color(1f, 0.62f, 0.34f), sunIntensity = 1.05f,
                                  sunAngles = new Vector3(12f, 205f, 0f), ambient = new Color(0.42f, 0.33f, 0.3f),
                                  fog = new Color(0.85f, 0.55f, 0.35f), fogDensity = 0.0075f,
                                  skyTint = new Color(0.85f, 0.5f, 0.35f), groundTint = new Color(0.35f, 0.25f, 0.2f), skyExposure = 1.0f,
                                  fill = new Color(0.75f, 0.55f, 0.85f), fillIntensity = 0.3f } },

            { 4, new Atmosphere { name = "Night", sun = new Color(0.45f, 0.55f, 0.85f), sunIntensity = 0.35f,
                                  sunAngles = new Vector3(65f, 160f, 0f), ambient = new Color(0.16f, 0.19f, 0.28f),
                                  fog = new Color(0.08f, 0.11f, 0.2f), fogDensity = 0.012f,
                                  skyTint = new Color(0.16f, 0.22f, 0.38f), groundTint = new Color(0.1f, 0.12f, 0.16f), skyExposure = 0.55f,
                                  fill = new Color(0.62f, 0.68f, 1f), fillIntensity = 0.8f } },
        };

        [MenuItem("DinoNet/Build All Playable Scenes")]
        public static void BuildAll()
        {
            for (var level = 1; level <= 4; level++)
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

            var route = PrepareRoute(k_Routes[level]);
            ApplyAtmosphere(k_Atmospheres[level], level);
            ThinEnvironment(level);
            FixDinoMovement();
            TidyEditModeBanners();
            AddFireflyTrail();
            var source = SetUpSourceDino();

            var quest = Object.FindFirstObjectByType<DinoQuestManager>();
            var hud = BuildHud(out var levelManagerHolder);

            var manager = levelManagerHolder.AddComponent<LevelManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("m_LevelNumber").intValue = level;
            so.FindProperty("m_Quest").objectReferenceValue = quest;
            so.FindProperty("m_Hud").objectReferenceValue = hud;
            so.FindProperty("m_TimeLimit").floatValue = 300f;
            so.FindProperty("m_UseTimer").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();

            WireHudButtons(hud, manager, null);
            AddRoutePresenter(quest, source);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[DinoNet] Built " + path + " with " + route.Count + " nodes (" + k_Atmospheres[level].name + ").");
        }

        public static void BuildTutorial()
        {
            var path = k_SceneFolder + "Tutorial.unity";
            var scene = Duplicate(path);

            PrepareRoute(k_Routes[1]);
            ApplyAtmosphere(k_Atmospheres[1], 1);
            ThinEnvironment(1);
            FixDinoMovement();
            TidyEditModeBanners();
            AddFireflyTrail();
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

            var director = holder.AddComponent<TutorialDirector>();
            var panel = BuildTutorialPanel(out var panelText, out var finishPanel);
            var dso = new SerializedObject(director);
            dso.FindProperty("m_Quest").objectReferenceValue = quest;
            dso.FindProperty("m_Firefly").objectReferenceValue = Object.FindFirstObjectByType<GuideFirefly>();
            dso.FindProperty("m_Orb").objectReferenceValue = quest != null ? quest.Orb : null;
            dso.FindProperty("m_PanelRoot").objectReferenceValue = panel;
            dso.FindProperty("m_PanelText").objectReferenceValue = panelText;
            dso.FindProperty("m_FinishPanel").objectReferenceValue = finishPanel;
            dso.ApplyModifiedPropertiesWithoutUndo();

            WireTutorialButtons(finishPanel, director);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[DinoNet] Built " + path + " (2 nodes, gameplay-driven steps).");
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

        /// <summary>Cuts the 7-node Demo route down to this level's nodes and deletes the rest.</summary>
        static List<QuestNode> PrepareRoute(string[] names)
        {
            var all = Object.FindObjectsByType<QuestNode>(FindObjectsInactive.Include, FindObjectsSortMode.None).ToList();
            var keep = new List<QuestNode>();
            foreach (var n in names)
            {
                var node = all.FirstOrDefault(q => q.name == n);
                if (node != null)
                    keep.Add(node);
            }

            foreach (var node in all.Where(q => !keep.Contains(q)).ToList())
                Object.DestroyImmediate(node.gameObject);

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

        /// <summary>Varies the greenery per level so the four worlds don't look identical.</summary>
        static void ThinEnvironment(int level)
        {
            var forest = GameObject.Find("Twilight Forest");
            if (forest == null)
                return;

            Random.InitState(level * 977);
            var keepRatio = level switch { 1 => 0.55f, 2 => 0.75f, 3 => 0.65f, _ => 1f };

            foreach (Transform group in forest.transform)
            {
                if (group.name != "Trees" && group.name != "Undergrowth")
                    continue;

                foreach (Transform child in group)
                {
                    // Keep the outer ring of trees so the play area always feels enclosed.
                    var far = new Vector2(child.position.x, child.position.z).magnitude > 24f;
                    child.gameObject.SetActive(far || Random.value <= keepRatio);
                }
            }
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

            var vine = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/EnergyVine.prefab");
            var so = new SerializedObject(presenter);
            so.FindProperty("m_Quest").objectReferenceValue = quest;
            so.FindProperty("m_SourceHighlight").objectReferenceValue = sourceRing;
            so.FindProperty("m_PreviewVinePrefab").objectReferenceValue = vine != null ? vine.GetComponent<EnergyVineVisual>() : null;
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
                new[] { "Next Level", "Play Again", "Return to Main Menu" }, out var completeButtons, out _);
            var failedPanel = BuildResultPanel(cam.transform, "Level Failed Panel", "Level Failed!", new Color(0.35f, 0.06f, 0.05f, 0.92f),
                new[] { "Try Again", "Return to Main Menu" }, out var failedButtons, out var failedReason);

            var hso = new SerializedObject(hud);
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
            out Button[] buttons, out TMP_Text reasonLabel)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0f, 2.2f);
            root.transform.localRotation = Quaternion.identity;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(900f, 620f);
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
            reasonRt.anchoredPosition = new Vector2(0f, -170f);
            reasonRt.sizeDelta = new Vector2(-40f, 70f);

            buttons = new Button[buttonLabels.Length];
            for (var i = 0; i < buttonLabels.Length; i++)
                buttons[i] = BuildButton(root.transform, buttonLabels[i], new Vector2(0f, -280f - i * 105f));

            return root;
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

            ApplyAtmosphere(k_Atmospheres[1], 1);
            FixDinoMovement();
            EnsureEventSystem();

            var holder = new GameObject("Main Menu");
            var controller = holder.AddComponent<MainMenuController>();

            var cam = Camera.main;
            var panel = BuildResultPanel(cam.transform, "Main Menu Panel", "Dino Net", new Color(0.05f, 0.18f, 0.3f, 0.93f),
                new[] { "Play Game", "Play Tutorial", "Exit" }, out var buttons, out var subtitle);
            subtitle.text = "Help messages travel through the dino network!";
            panel.SetActive(true);

            // Detach so the menu stays put instead of following the headset.
            panel.transform.SetParent(null, true);

            Bind(buttons[0], controller, "PlayGame");
            Bind(buttons[1], controller, "PlayTutorial");
            Bind(buttons[2], controller, "ExitGame");

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
