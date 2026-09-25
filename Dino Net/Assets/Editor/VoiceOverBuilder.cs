using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using DinoNet;

namespace DinoNetEditor
{
    /// <summary>
    /// Collects every line the game speaks aloud and writes them to a manifest. The script
    /// tools/generate_voiceover.ps1 then records each line with the offline Windows voice into
    /// Assets/Resources/VO, named by <see cref="VoiceOver.Slug"/> so the game finds it at runtime.
    /// Re-run it after changing any spoken text; lines that already have a clip are skipped.
    /// </summary>
    public static class VoiceOverBuilder
    {
        static readonly Regex k_Calls = new Regex(@"(?:Speak|Say|Announce)\(\s*""((?:[^""\\]|\\.)*)""\s*\)");
        static readonly Regex k_Messages = new Regex(@"(?:Message|intro)\s*=\s*""((?:[^""\\]|\\.)*)""");

        public static string ManifestPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/voice_manifest.json"));

        [MenuItem("DinoNet/Write Voice-Over Manifest")]
        public static int WriteManifest()
        {
            var lines = new SortedDictionary<string, string>();

            void Add(string text)
            {
                var normalised = VoiceOver.Normalise(text);
                if (normalised.Length < 2)
                    return;

                lines[VoiceOver.Slug(text)] = normalised;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:NetworkLesson"))
            {
                var lesson = AssetDatabase.LoadAssetAtPath<NetworkLesson>(AssetDatabase.GUIDToAssetPath(guid));
                if (lesson == null)
                    continue;

                Add(lesson.speaker + " " + lesson.prompt);
                Add(lesson.correctFeedback);
                if (lesson.options == null)
                    continue;

                foreach (var option in lesson.options)
                {
                    if (option != null)
                        Add(option.wrongFeedback);
                }
            }

            var files = new List<string>(Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories));
            files.Add("Assets/Editor/LessonContentBuilder.cs");
            foreach (var file in files)
            {
                var source = File.ReadAllText(file);
                foreach (Match m in k_Calls.Matches(source))
                    Add(Regex.Unescape(m.Groups[1].Value));
                foreach (Match m in k_Messages.Matches(source))
                    Add(Regex.Unescape(m.Groups[1].Value));
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath));
            var sb = new StringBuilder("[\n");
            var first = true;
            foreach (var pair in lines)
            {
                if (!first)
                    sb.Append(",\n");

                first = false;
                sb.Append("  {\"slug\":\"").Append(pair.Key).Append("\",\"text\":\"").Append(Escape(pair.Value)).Append("\"}");
            }

            sb.Append("\n]\n");
            File.WriteAllText(ManifestPath, sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[DinoNet] Voice-over manifest: " + lines.Count + " lines -> " + ManifestPath);
            return lines.Count;
        }

        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
