using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DinoNetEditor
{
    /// <summary>
    /// Runs build and check tasks from a file, so the scene generators can be driven without
    /// clicking through the menus. Drop a task name into DinoNetTasks/request.txt and the result
    /// appears in DinoNetTasks/result.txt.
    /// </summary>
    [InitializeOnLoad]
    public static class DinoNetTaskRunner
    {
        const string k_Folder = "DinoNetTasks/";
        const string k_Request = k_Folder + "request.txt";
        const string k_Result = k_Folder + "result.txt";
        const double k_PollSeconds = 0.5;

        static double s_NextPoll;

        static DinoNetTaskRunner()
        {
            EditorApplication.update -= Poll;
            EditorApplication.update += Poll;
        }

        static void Poll()
        {
            if (EditorApplication.timeSinceStartup < s_NextPoll)
                return;

            s_NextPoll = EditorApplication.timeSinceStartup + k_PollSeconds;

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || !File.Exists(k_Request))
                return;

            string request;
            try
            {
                request = File.ReadAllText(k_Request).Trim();
            }
            catch (IOException)
            {
                // Still being written; try again on the next tick.
                return;
            }

            File.Delete(k_Request);
            Run(request);
        }

        static void Run(string request)
        {
            var parts = request.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            var task = parts.Length > 0 ? parts[0] : string.Empty;
            var argument = parts.Length > 1 ? parts[1].Trim() : string.Empty;

            try
            {
                switch (task)
                {
                    case "refresh":
                        AssetDatabase.Refresh();
                        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
                        Write("OK refresh requested");
                        break;

                    case "icons":
                        LessonIconBuilder.BuildAll();
                        Write("OK icons rebuilt");
                        break;

                    case "build":
                        LevelSceneBuilder.BuildAll();
                        Write("OK all scenes built");
                        break;

                    case "level":
                        LevelSceneBuilder.BuildLevel(int.Parse(argument));
                        Write("OK level " + argument + " built");
                        break;

                    case "preview":
                    {
                        var bits = argument.Split(' ');
                        var index = bits.Length > 1 ? int.Parse(bits[1]) : 0;
                        Write(SceneReport.Preview(bits[0], index, Path.GetFullPath(k_Folder + "preview.png")));
                        break;
                    }

                    case "inspect":
                        Write(SceneReport.Describe(argument));
                        break;

                    case "play":
                        StartPlaytest(argument);
                        break;

                    default:
                        Write("ERROR unknown task: " + task);
                        break;
                }
            }
            catch (Exception e)
            {
                Write("ERROR " + e);
            }
        }

        /// <summary>
        /// Opens a scene and enters play mode, leaving a note the runtime driver picks up. The
        /// driver builds itself once the scene is live, so nothing is ever added to the saved
        /// scene and no generated scene is left dirty.
        /// </summary>
        static void StartPlaytest(string argument)
        {
            var parts = argument.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            var sceneName = parts[0];
            var script = parts.Length > 1 ? parts[1] : sceneName.ToLowerInvariant();

            Write("RUNNING playtest " + sceneName + " / " + script);

            Directory.CreateDirectory(k_Folder);
            File.WriteAllText(k_Folder + "playtest.txt", script + "\n" + k_Result);

            EditorSceneManager.OpenScene("Assets/Scenes/" + sceneName + ".unity", OpenSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        static void Write(string text)
        {
            Directory.CreateDirectory(k_Folder);
            File.WriteAllText(k_Result, text);
        }
    }
}
