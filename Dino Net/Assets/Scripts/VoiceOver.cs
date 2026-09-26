using System.Text;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Reads game text aloud, so children who can't read yet can still follow. Each spoken line is
    /// a clip in Resources/VO named after a hash of its text (made by the DinoNet/Generate Voice-Over
    /// editor tool). If a line has no clip it is simply not spoken.
    /// </summary>
    public static class VoiceOver
    {
        const string k_Pref = "dn_voice_on";

        static AudioSource s_Source;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(k_Pref, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(k_Pref, value ? 1 : 0);
                PlayerPrefs.Save();
                if (!value)
                    Stop();
            }
        }

        /// <summary>Stable name for a line's clip. Also used by the editor tool that records the lines.</summary>
        public static string Slug(string text)
        {
            var normalised = Normalise(text);
            // FNV-1a: tiny, deterministic, and identical in the editor and the player.
            var hash = 2166136261u;
            foreach (var b in Encoding.UTF8.GetBytes(normalised))
            {
                hash ^= b;
                hash *= 16777619u;
            }

            return "vo_" + hash.ToString("x8");
        }

        /// <summary>The text exactly as it is recorded: trimmed, single-spaced, without markup.</summary>
        public static string Normalise(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var sb = new StringBuilder(text.Length);
            var inTag = false;
            var lastSpace = true;
            foreach (var c in text)
            {
                if (c == '<')
                {
                    inTag = true;
                    continue;
                }

                if (c == '>' && inTag)
                {
                    inTag = false;
                    continue;
                }

                if (inTag)
                    continue;

                if (char.IsWhiteSpace(c))
                {
                    if (!lastSpace)
                        sb.Append(' ');
                    lastSpace = true;
                }
                else
                {
                    sb.Append(c);
                    lastSpace = false;
                }
            }

            return sb.ToString().Trim();
        }

        /// <summary>True while a line is still being spoken.</summary>
        public static bool IsSpeaking => s_Source != null && s_Source.isPlaying;

        /// <summary>
        /// Speaks a line and reports how long it will take, so the caller can leave the matching
        /// text on screen until the voice has finished instead of talking over itself.
        /// Returns 0 when the line has no recording or the voice is switched off.
        /// </summary>
        public static float Speak(string text)
        {
            if (!Enabled || string.IsNullOrWhiteSpace(text))
                return 0f;

            var clip = Resources.Load<AudioClip>("VO/" + Slug(text));
            if (clip == null)
                return 0f;

            if (s_Source == null)
            {
                var host = new GameObject("Voice Over");
                Object.DontDestroyOnLoad(host);
                s_Source = host.AddComponent<AudioSource>();
                s_Source.spatialBlend = 0f;
                s_Source.playOnAwake = false;
                s_Source.volume = 1f;
            }

            s_Source.Stop();
            s_Source.clip = clip;
            s_Source.Play();
            return clip.length;
        }

        public static void Stop()
        {
            if (s_Source != null)
                s_Source.Stop();
        }
    }
}
