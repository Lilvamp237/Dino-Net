using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DinoNetEditor
{
    /// <summary>
    /// Synthesises the game's sound effects and background music as WAV clips in
    /// Assets/Resources/DinoAudio. Everything is generated from maths, so there are no licences to
    /// track and the sounds can be tuned by editing numbers here and regenerating.
    /// </summary>
    public static class ProceduralAudioBuilder
    {
        const string k_Folder = "Assets/Resources/DinoAudio/";
        const int k_SfxRate = 32000;
        const int k_MusicRate = 22050;

        [MenuItem("DinoNet/Generate Audio")]
        public static void Generate()
        {
            Directory.CreateDirectory(k_Folder);
            var rng = new System.Random(4242);

            Save("ui_click", Render(0.09f, k_SfxRate, t => Sine(1250f, t) * Mathf.Exp(-t * 55f) * 0.5f), k_SfxRate);
            Save("ui_pop", Render(0.14f, k_SfxRate, t => Sine(Mathf.Lerp(520f, 900f, t / 0.14f), t) * Mathf.Exp(-t * 30f) * 0.55f), k_SfxRate);

            Save("sparkle", Render(1.0f, k_SfxRate, t =>
            {
                var starts = new[] { 0f, 0.07f, 0.15f, 0.24f, 0.33f, 0.45f };
                var freqs = new[] { 1568f, 2093f, 1760f, 2349f, 2637f, 3136f };
                var sum = 0f;
                for (var i = 0; i < starts.Length; i++)
                {
                    if (t >= starts[i])
                        sum += Sine(freqs[i], t) * Mathf.Exp(-(t - starts[i]) * 12f) * 0.22f;
                }

                return sum;
            }), k_SfxRate);

            Save("ack_ding", Render(0.9f, k_SfxRate, t =>
                (Sine(880f, t) + Sine(1320f, t) * 0.4f + Sine(1760f, t) * 0.25f) * Mathf.Exp(-t * 5f) * 0.4f), k_SfxRate);

            Save("lost_whoosh", RenderState(1.0f, k_SfxRate, rng, (t, r, lp) =>
            {
                var n = (float)(r.NextDouble() * 2.0 - 1.0);
                var cutoff = Mathf.Lerp(3000f, 250f, t);
                lp.value += Alpha(cutoff, k_SfxRate) * (n - lp.value);
                var env = Mathf.Sin(Mathf.Clamp01(t / 1.0f) * Mathf.PI) * 0.9f;
                return (lp.value * 1.2f + Sine(Mathf.Lerp(420f, 110f, t), t) * 0.25f) * env * 0.5f;
            }), k_SfxRate);

            Save("gate_open", RenderState(1.1f, k_SfxRate, rng, (t, r, lp) =>
            {
                var vib = Mathf.Sin(t * 2f * Mathf.PI * 6f) * 6f;
                var raw = Saw(85f + vib + t * 25f, t) + ((float)r.NextDouble() - 0.5f) * 0.25f;
                lp.value += Alpha(650f, k_SfxRate) * (raw - lp.value);
                var env = Mathf.Clamp01(t / 0.12f) * Mathf.Clamp01((1.1f - t) / 0.35f);
                return lp.value * env * 0.6f;
            }), k_SfxRate);

            Save("shield_up", Render(0.55f, k_SfxRate, t =>
                Sine(Mathf.Lerp(380f, 1250f, t / 0.55f), t) * (0.7f + 0.3f * Mathf.Sin(t * 70f)) * Mathf.Clamp01((0.55f - t) / 0.2f) * 0.45f), k_SfxRate);

            Save("warning_pulse", Render(1.2f, k_SfxRate, t =>
            {
                var pulse = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f * 1.6f)), 2f);
                return (Sine(196f, t) + Sine(294f, t) * 0.4f) * pulse * Mathf.Clamp01((1.2f - t) / 0.3f) * 0.35f;
            }), k_SfxRate);

            Save("footstep", RenderState(0.22f, k_SfxRate, rng, (t, r, lp) =>
            {
                var n = (float)(r.NextDouble() * 2.0 - 1.0);
                lp.value += Alpha(400f, k_SfxRate) * (n - lp.value);
                return (Sine(64f, t) * Mathf.Exp(-t * 26f) * 0.9f + lp.value * Mathf.Exp(-t * 45f) * 1.4f) * 0.7f;
            }), k_SfxRate);

            Save("grunt_small", RenderState(0.4f, k_SfxRate, rng, (t, r, lp) =>
            {
                var f = Mathf.Lerp(250f, 170f, t / 0.4f) + Mathf.Sin(t * 60f) * 8f;
                lp.value += Alpha(1000f, k_SfxRate) * (Saw(f, t) - lp.value);
                return lp.value * Mathf.Clamp01(t / 0.04f) * Mathf.Exp(-t * 5f) * 0.7f;
            }), k_SfxRate);

            Save("grunt_large", RenderState(0.7f, k_SfxRate, rng, (t, r, lp) =>
            {
                var f = Mathf.Lerp(118f, 68f, t / 0.7f) + Mathf.Sin(t * 45f) * 5f;
                lp.value += Alpha(520f, k_SfxRate) * (Saw(f, t) - lp.value);
                return lp.value * Mathf.Clamp01(t / 0.06f) * Mathf.Exp(-t * 3.2f) * 0.85f;
            }), k_SfxRate);

            // A cartoon "rawr": friendly rather than frightening.
            Save("roar", RenderState(1.3f, k_SfxRate, rng, (t, r, lp) =>
            {
                var n = (float)(r.NextDouble() * 2.0 - 1.0);
                var f = Mathf.Lerp(140f, 88f, t / 1.3f) + Mathf.Sin(t * 30f) * 14f;
                var raw = Saw(f, t) * 0.7f + n * 0.25f;
                lp.value += Alpha(1200f, k_SfxRate) * (raw - lp.value);
                var trem = 0.7f + 0.3f * Mathf.Sin(t * 2f * Mathf.PI * 22f);
                var env = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((1.3f - t) / 0.5f);
                return lp.value * trem * env * 0.8f;
            }), k_SfxRate);

            Save("booster", Render(0.65f, k_SfxRate, t =>
                (Sine(Mathf.Lerp(500f, 1500f, Mathf.Pow(t / 0.65f, 1.4f)), t) + Sine(Mathf.Lerp(750f, 2250f, t / 0.65f), t) * 0.3f) * Mathf.Exp(-t * 3.5f) * 0.45f), k_SfxRate);

            Save("power_up", Render(1.6f, k_SfxRate, t =>
            {
                var notes = new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
                var sum = 0f;
                for (var i = 0; i < notes.Length; i++)
                {
                    var start = i * 0.17f;
                    if (t >= start)
                        sum += (Sine(notes[i], t) + Sine(notes[i] * 2f, t) * 0.3f) * Mathf.Exp(-(t - start) * 3.2f) * 0.3f;
                }

                return sum;
            }), k_SfxRate);

            // Background music: soft looping pentatonic patterns, one mood per group of levels.
            Save("music_menu", Music(84f, 8, 60, false, 11, 0.9f, 1.15f), k_MusicRate);
            Save("music_calm", Music(72f, 8, 62, false, 22, 0.8f, 1.4f), k_MusicRate);
            Save("music_bright", Music(100f, 8, 67, false, 33, 1.0f, 1.0f), k_MusicRate);
            Save("music_warm", Music(88f, 8, 57, true, 44, 0.9f, 1.3f), k_MusicRate);
            Save("music_mystery", Music(76f, 8, 64, true, 55, 0.7f, 1.7f), k_MusicRate);
            Save("music_night", Music(68f, 8, 50, true, 66, 0.75f, 1.8f), k_MusicRate);

            AssetDatabase.Refresh();
            Configure();
            Debug.Log("[DinoNet] Generated audio into " + k_Folder);
        }

        static void Configure()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { k_Folder.TrimEnd('/') }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                    continue;

                var music = Path.GetFileNameWithoutExtension(path).StartsWith("music_");
                var settings = importer.defaultSampleSettings;
                settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = music ? 0.45f : 0.6f;
                importer.defaultSampleSettings = settings;
                importer.forceToMono = true;
                importer.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------------ synthesis

        sealed class Filter
        {
            public float value;
        }

        static float Sine(float freq, float t) => Mathf.Sin(2f * Mathf.PI * freq * t);

        static float Saw(float freq, float t) => 2f * (freq * t - Mathf.Floor(freq * t + 0.5f));

        static float Tri(float freq, float t) => 2f * Mathf.Abs(Saw(freq, t)) - 1f;

        static float Alpha(float cutoff, int rate) => 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / rate);

        static float[] Render(float seconds, int rate, Func<float, float> f)
        {
            var n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            for (var i = 0; i < n; i++)
                data[i] = f(i / (float)rate);
            return data;
        }

        static float[] RenderState(float seconds, int rate, System.Random rng, Func<float, System.Random, Filter, float> f)
        {
            var n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            var filter = new Filter();
            for (var i = 0; i < n; i++)
                data[i] = f(i / (float)rate, rng, filter);
            return data;
        }

        static float Midi(float note) => 440f * Mathf.Pow(2f, (note - 69f) / 12f);

        /// <summary>
        /// A seamless loop: notes that ring past the end wrap round into the start.
        /// </summary>
        static float[] Music(float bpm, int bars, int rootNote, bool minor, int seed, float brightness, float decay)
        {
            var rate = k_MusicRate;
            var beat = 60f / bpm;
            var length = bars * 4 * beat;
            var n = Mathf.CeilToInt(length * rate);
            var buffer = new float[n];
            var rng = new System.Random(seed);
            var scale = minor ? new[] { 0, 3, 5, 7, 10 } : new[] { 0, 2, 4, 7, 9 };

            void Add(float freq, float start, float dur, float amp, float harm)
            {
                var i0 = Mathf.RoundToInt(start * rate);
                var count = Mathf.RoundToInt(dur * rate);
                for (var i = 0; i < count; i++)
                {
                    var t = i / (float)rate;
                    var env = Mathf.Clamp01(t / 0.012f) * Mathf.Exp(-t * (3.2f / decay));
                    var v = (Tri(freq, t) * 0.6f + Sine(freq * 2f, t) * harm) * env * amp;
                    buffer[(i0 + i) % n] += v;
                }
            }

            var step = 0;
            for (var b = 0; b < bars * 8; b++)
            {
                var time = b * beat * 0.5f;
                // Random walk over the scale, with the odd rest so it breathes.
                if (rng.NextDouble() < 0.22)
                    continue;

                step = Mathf.Clamp(step + rng.Next(-2, 3), -2, 9);
                var octave = Mathf.FloorToInt(step / 5f);
                var degree = ((step % 5) + 5) % 5;
                var note = rootNote + 12 + octave * 12 + scale[degree];
                Add(Midi(note), time, 2.2f * decay, 0.16f * brightness, 0.25f * brightness);
            }

            // Slow pad and bass, one chord per two bars.
            for (var bar = 0; bar < bars; bar += 2)
            {
                var start = bar * 4 * beat;
                var chordRoot = rootNote + scale[(bar / 2 * 2) % 5];
                var dur = beat * 8f;
                var count = Mathf.RoundToInt(dur * rate);
                var i0 = Mathf.RoundToInt(start * rate);
                for (var i = 0; i < count; i++)
                {
                    var t = i / (float)rate;
                    var swell = Mathf.Sin(Mathf.PI * t / dur);
                    var pad = (Sine(Midi(chordRoot), t) + Sine(Midi(chordRoot + (minor ? 3 : 4)), t) * 0.7f + Sine(Midi(chordRoot + 7), t) * 0.6f) * 0.045f;
                    var bass = Sine(Midi(chordRoot - 12), t) * 0.09f;
                    buffer[(i0 + i) % n] += (pad + bass) * swell;
                }
            }

            var peak = 0.0001f;
            foreach (var s in buffer)
                peak = Mathf.Max(peak, Mathf.Abs(s));
            for (var i = 0; i < n; i++)
                buffer[i] = buffer[i] / peak * 0.8f;

            return buffer;
        }

        // ------------------------------------------------------------------ files

        static void Save(string name, float[] samples, int rate)
        {
            // Normalise sound effects so none is far louder than another.
            var peak = 0.0001f;
            foreach (var s in samples)
                peak = Mathf.Max(peak, Mathf.Abs(s));
            var gain = name.StartsWith("music_") ? 1f : Mathf.Min(0.9f / peak, 3f);

            using (var stream = new FileStream(k_Folder + name + ".wav", FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                var dataBytes = samples.Length * 2;
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + dataBytes);
                writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(rate);
                writer.Write(rate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(dataBytes);
                foreach (var s in samples)
                    writer.Write((short)(Mathf.Clamp(s * gain, -1f, 1f) * 32000f));
            }
        }
    }
}
