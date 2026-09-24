using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using DinoNet;

namespace DinoNetEditor
{
    /// <summary>
    /// Reads a generated scene back and describes what is in it, so a build can be checked without
    /// entering play mode.
    /// </summary>
    public static class SceneReport
    {
        public static string Describe(string sceneName)
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
            var report = new StringBuilder();
            report.AppendLine("scene: " + scene.name);

            var quest = Object.FindFirstObjectByType<DinoQuestManager>();
            report.AppendLine("route nodes: " + (quest != null ? quest.RouteCount.ToString() : "no quest"));

            var manager = Object.FindFirstObjectByType<LevelManager>();
            if (manager != null)
            {
                var so = new SerializedObject(manager);
                report.AppendLine("level number: " + so.FindProperty("m_LevelNumber").intValue
                                  + "  timer: " + so.FindProperty("m_TimeLimit").floatValue
                                  + "s  useTimer: " + so.FindProperty("m_UseTimer").boolValue
                                  + "  dangerMultiplier: " + so.FindProperty("m_DangerTimeMultiplier").floatValue);
            }

            var director = Object.FindFirstObjectByType<LessonDirector>();
            if (director != null)
            {
                var so = new SerializedObject(director);
                var lessons = so.FindProperty("m_Lessons");
                report.AppendLine("lessons: " + lessons.arraySize
                                  + "  intro: \"" + so.FindProperty("m_IntroMessage").stringValue + "\"");

                for (var i = 0; i < lessons.arraySize; i++)
                {
                    var lesson = lessons.GetArrayElementAtIndex(i).objectReferenceValue as NetworkLesson;
                    if (lesson == null)
                    {
                        report.AppendLine("  [" + i + "] MISSING");
                        continue;
                    }

                    report.AppendLine("  [" + i + "] " + lesson.name + "  term=" + lesson.conceptTerm
                                      + "  afterNodes=" + lesson.triggerAfterNodes
                                      + "  packet=" + lesson.packetLabel);
                    foreach (var option in lesson.options)
                    {
                        report.AppendLine("        " + (option.correct ? "SAFE  " : "other ") + option.label
                                          + " | " + option.sublabel
                                          + " | icon=" + (option.icon != null ? option.icon.name : "NONE"));
                    }
                }
            }
            else
            {
                report.AppendLine("lessons: none (no lesson director)");
            }

            report.AppendLine("decision panel: " + Describe(Find("Decision Panel")));
            report.AppendLine("packet tag: " + (quest != null && quest.Orb != null
                ? Describe(quest.Orb.GetComponentInChildren<PacketLabel>(true)?.gameObject)
                : "no packet"));

            var hud = Object.FindFirstObjectByType<LevelHud>();
            if (hud != null)
            {
                var so = new SerializedObject(hud);
                report.AppendLine("concept chip: title=\"" + LabelText(so, "m_ConceptTitle")
                                  + "\"  result=\"" + LabelText(so, "m_ConceptResult") + "\"");
            }

            var fact = Find("Level Complete Panel/Level Complete Panel Canvas/Fun Fact/Fact");
            report.AppendLine("fun fact: \"" + (fact != null ? fact.GetComponent<TMP_Text>().text : "MISSING") + "\"");

            var strip = Find("Level Complete Panel/Level Complete Panel Canvas/Fun Fact/Strip");
            if (strip != null)
            {
                var captions = strip.GetComponentsInChildren<TMP_Text>(true)
                    .Where(t => t.name == "Caption").Select(t => t.text);
                report.AppendLine("fun fact strip: " + string.Join("  ->  ", captions));
            }

            report.AppendLine("connections in scene at rest: " + Object.FindObjectsByType<NetworkConnection>(FindObjectsSortMode.None).Length);
            report.AppendLine("wandering NPC dinosaurs: " + Object.FindObjectsByType<RoadWanderer>(FindObjectsSortMode.None).Length);
            report.AppendLine("danger zones: " + Object.FindObjectsByType<DangerZone>(FindObjectsSortMode.None).Length);
            report.AppendLine("firefly: " + (Object.FindFirstObjectByType<GuideFirefly>() != null ? "present" : "MISSING"));
            report.AppendLine("legacy above-floor vines: " + Object.FindObjectsByType<EnergyVineVisual>(FindObjectsSortMode.None).Length);

            var sky = UnityEngine.RenderSettings.skybox;
            report.AppendLine("skybox: " + (sky != null ? sky.name : "none"));

            foreach (var panel in new[] { "Level Complete Panel", "Level Failed Panel" })
            {
                var root = Find(panel + "/" + panel + " Canvas");
                if (root == null)
                    continue;

                var buttons = root.GetComponentsInChildren<Button>(true)
                    .Select(b => b.name + "(" + b.onClick.GetPersistentEventCount() + ")");
                report.AppendLine(panel + " buttons: " + string.Join(", ", buttons));
            }

            return report.ToString();
        }


        /// <summary>
        /// Renders one of a level's decision panels to a PNG, so the layout can be eyeballed
        /// without putting a headset on. Nothing is saved back into the scene.
        /// </summary>
        public static string Preview(string sceneName, int lessonIndex, string outputPath)
        {
            EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);

            Transform body;
            string subject;

            // A negative index renders the completion panel with its reward instead of a question.
            if (lessonIndex < 0)
            {
                var complete = Find("Level Complete Panel/Level Complete Panel Canvas");
                if (complete == null)
                    return "ERROR no completion panel in " + sceneName;

                complete.transform.parent.gameObject.SetActive(true);
                complete.SetActive(true);

                // ShowComplete hides the HUD at runtime; do the same so the preview matches.
                var hudRoot = Find("Level HUD");
                if (hudRoot != null)
                    hudRoot.SetActive(false);
                body = complete.transform;
                subject = "the completion panel";
            }
            else
            {
                var panel = Object.FindFirstObjectByType<DecisionPanel>();
                var director = Object.FindFirstObjectByType<LessonDirector>();
                if (panel == null || director == null)
                    return "ERROR no decision panel in " + sceneName;

                var lessons = new SerializedObject(director).FindProperty("m_Lessons");
                if (lessonIndex >= lessons.arraySize)
                    return "ERROR level has only " + lessons.arraySize + " lessons";

                var lesson = lessons.GetArrayElementAtIndex(lessonIndex).objectReferenceValue as NetworkLesson;
                panel.Show(lesson);
                body = panel.transform.GetChild(0);
                subject = lesson.name;
            }

            var rect = body.GetComponent<RectTransform>();
            var size = rect.sizeDelta * rect.localScale.x;

            var holder = new GameObject("Preview Camera");
            var camera = holder.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.12f, 0.16f);
            camera.fieldOfView = 45f;
            var distance = size.y * 0.5f / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.06f;
            holder.transform.position = rect.position - body.forward * distance;
            holder.transform.rotation = Quaternion.LookRotation(body.forward, body.up);

            var target = new RenderTexture(1160, 800, 24);
            camera.targetTexture = target;
            camera.Render();

            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            System.IO.File.WriteAllBytes(outputPath, texture.EncodeToPNG());

            Object.DestroyImmediate(holder);
            Object.DestroyImmediate(texture);
            target.Release();

            return "OK preview of " + subject + " written to " + outputPath;
        }

        static string LabelText(SerializedObject so, string property)
        {
            var label = so.FindProperty(property).objectReferenceValue as TMP_Text;
            return label != null ? label.text : "MISSING";
        }

        static string Describe(GameObject go)
        {
            if (go == null)
                return "MISSING";

            var children = go.GetComponentsInChildren<Transform>(true).Length;
            return go.name + " (" + children + " transforms, active=" + go.activeSelf + ")";
        }

        /// <summary>
        /// Finds an object by a slash path. The first name is matched anywhere in the scene, not
        /// just at the root, because the panels hang off the camera until they are shown.
        /// </summary>
        static GameObject Find(string path)
        {
            var parts = path.Split('/');
            GameObject current = null;

            foreach (var transform in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
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
    }
}
