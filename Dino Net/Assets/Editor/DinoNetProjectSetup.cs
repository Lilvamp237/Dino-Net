using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DinoNetEditor
{
    /// <summary>
    /// One-time project plumbing for the new features: the prehistoric sky materials for each level
    /// mood, and making sure every shader the game creates from code is bundled into headset
    /// builds (Shader.Find only works for shaders that are included, and unreferenced ones are
    /// stripped - which shows up as pink materials on the Quest).
    /// </summary>
    public static class DinoNetProjectSetup
    {
        const string k_Materials = "Assets/Materials/";

        sealed class SkyPreset
        {
            public Color zenith, mid, horizon, haze, ground, sunColor, cloud, cloudShade, smoke;
            public Vector3 sunDir;
            public float sunSize = 0.02f, cover = 0.45f, smokeAmount = 0.8f, birds = 0.8f, stars;
        }

        static readonly Dictionary<string, SkyPreset> k_Presets = new Dictionary<string, SkyPreset>
        {
            { "Level1", new SkyPreset { zenith = C(0.28f, 0.5f, 0.9f), mid = C(0.62f, 0.8f, 0.98f), horizon = C(1f, 0.92f, 0.75f), haze = C(0.82f, 0.9f, 0.98f),
                ground = C(0.4f, 0.5f, 0.35f), sunColor = C(1f, 0.95f, 0.8f), cloud = C(1f, 1f, 1f), cloudShade = C(0.72f, 0.78f, 0.9f), smoke = C(0.3f, 0.26f, 0.26f),
                sunDir = new Vector3(0.3f, 0.7f, 0.65f), cover = 0.5f, smokeAmount = 0.7f } },
            { "Level2", new SkyPreset { zenith = C(0.3f, 0.5f, 0.85f), mid = C(0.75f, 0.8f, 0.85f), horizon = C(1f, 0.88f, 0.6f), haze = C(0.88f, 0.85f, 0.7f),
                ground = C(0.5f, 0.45f, 0.3f), sunColor = C(1f, 0.9f, 0.7f), cloud = C(1f, 0.96f, 0.86f), cloudShade = C(0.78f, 0.72f, 0.66f), smoke = C(0.32f, 0.27f, 0.24f),
                sunDir = new Vector3(0.8f, 0.55f, 0.3f), cover = 0.55f, smokeAmount = 0.75f } },
            { "Level3", new SkyPreset { zenith = C(0.25f, 0.22f, 0.5f), mid = C(0.75f, 0.4f, 0.45f), horizon = C(1f, 0.55f, 0.25f), haze = C(0.85f, 0.55f, 0.35f),
                ground = C(0.3f, 0.2f, 0.18f), sunColor = C(1f, 0.6f, 0.3f), cloud = C(1f, 0.72f, 0.52f), cloudShade = C(0.55f, 0.3f, 0.4f), smoke = C(0.25f, 0.15f, 0.16f),
                sunDir = new Vector3(-0.5f, 0.2f, 0.85f), sunSize = 0.03f, cover = 0.5f, smokeAmount = 0.9f } },
            { "Level5", new SkyPreset { zenith = C(0.3f, 0.45f, 0.8f), mid = C(0.9f, 0.75f, 0.6f), horizon = C(1f, 0.8f, 0.45f), haze = C(0.95f, 0.8f, 0.55f),
                ground = C(0.45f, 0.38f, 0.25f), sunColor = C(1f, 0.85f, 0.5f), cloud = C(1f, 0.85f, 0.6f), cloudShade = C(0.75f, 0.55f, 0.45f), smoke = C(0.3f, 0.22f, 0.2f),
                sunDir = new Vector3(0.6f, 0.4f, 0.65f), sunSize = 0.028f, cover = 0.5f, smokeAmount = 0.8f } },
            { "Level6", new SkyPreset { zenith = C(0.45f, 0.55f, 0.8f), mid = C(0.8f, 0.75f, 0.85f), horizon = C(1f, 0.8f, 0.8f), haze = C(0.85f, 0.82f, 0.9f),
                ground = C(0.42f, 0.42f, 0.4f), sunColor = C(1f, 0.85f, 0.85f), cloud = C(1f, 0.94f, 0.96f), cloudShade = C(0.78f, 0.74f, 0.86f), smoke = C(0.35f, 0.3f, 0.32f),
                sunDir = new Vector3(-0.4f, 0.3f, 0.85f), cover = 0.7f, smokeAmount = 0.5f, birds = 0.9f } },
            { "Level7", new SkyPreset { zenith = C(0.15f, 0.4f, 0.9f), mid = C(0.5f, 0.75f, 1f), horizon = C(0.8f, 0.92f, 1f), haze = C(0.8f, 0.92f, 1f),
                ground = C(0.4f, 0.5f, 0.4f), sunColor = C(1f, 0.98f, 0.9f), cloud = C(1f, 1f, 1f), cloudShade = C(0.7f, 0.78f, 0.92f), smoke = C(0.32f, 0.28f, 0.28f),
                sunDir = new Vector3(0.2f, 0.9f, 0.4f), cover = 0.4f, smokeAmount = 0.6f } },
            { "Level8", new SkyPreset { zenith = C(0.15f, 0.1f, 0.2f), mid = C(0.45f, 0.2f, 0.25f), horizon = C(0.9f, 0.35f, 0.2f), haze = C(0.6f, 0.28f, 0.25f),
                ground = C(0.2f, 0.1f, 0.1f), sunColor = C(1f, 0.4f, 0.2f), cloud = C(0.62f, 0.36f, 0.34f), cloudShade = C(0.3f, 0.16f, 0.2f), smoke = C(0.16f, 0.1f, 0.1f),
                sunDir = new Vector3(0.1f, 0.12f, 0.95f), sunSize = 0.035f, cover = 0.75f, smokeAmount = 1f, birds = 0.6f } },
            { "Level9", new SkyPreset { zenith = C(0.02f, 0.03f, 0.12f), mid = C(0.08f, 0.1f, 0.28f), horizon = C(0.25f, 0.25f, 0.5f), haze = C(0.18f, 0.2f, 0.4f),
                ground = C(0.06f, 0.07f, 0.14f), sunColor = C(0.2f, 0.2f, 0.35f), cloud = C(0.35f, 0.4f, 0.6f), cloudShade = C(0.12f, 0.14f, 0.28f), smoke = C(0.1f, 0.08f, 0.14f),
                sunDir = new Vector3(0f, -0.5f, 1f), sunSize = 0.002f, cover = 0.25f, smokeAmount = 0.7f, birds = 0.2f, stars = 2.6f } },
            { "Level10", new SkyPreset { zenith = C(0.05f, 0.05f, 0.22f), mid = C(0.3f, 0.16f, 0.42f), horizon = C(1f, 0.45f, 0.35f), haze = C(0.42f, 0.24f, 0.4f),
                ground = C(0.29f, 0.17f, 0.28f), sunColor = C(1f, 0.6f, 0.4f), cloud = C(0.75f, 0.45f, 0.5f), cloudShade = C(0.25f, 0.14f, 0.3f), smoke = C(0.14f, 0.09f, 0.13f),
                sunDir = new Vector3(0.5f, 0.1f, -0.86f), sunSize = 0.03f, cover = 0.3f, smokeAmount = 0.9f, birds = 0.5f, stars = 2f } },
            { "Sandbox", new SkyPreset { zenith = C(0.3f, 0.5f, 0.88f), mid = C(0.85f, 0.85f, 0.8f), horizon = C(1f, 0.9f, 0.65f), haze = C(0.9f, 0.88f, 0.75f),
                ground = C(0.45f, 0.5f, 0.35f), sunColor = C(1f, 0.92f, 0.7f), cloud = C(1f, 0.97f, 0.9f), cloudShade = C(0.78f, 0.78f, 0.85f), smoke = C(0.3f, 0.26f, 0.26f),
                sunDir = new Vector3(0.5f, 0.5f, 0.7f), cover = 0.5f, smokeAmount = 0.6f } },
        };

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        [MenuItem("DinoNet/Setup Shaders and Skies")]
        public static void Setup()
        {
            EnsureShadersIncluded();
            BuildSkyMaterials();
        }

        public static string SkyPath(string key) => k_Materials + "PrehistoricSky_" + key + ".mat";

        // ------------------------------------------------------------------ skies

        public static void BuildSkyMaterials()
        {
            Directory.CreateDirectory(Application.dataPath + "/Materials");
            var shader = Shader.Find("DinoNet/PrehistoricSky");
            if (shader == null)
            {
                Debug.LogError("[DinoNet] PrehistoricSky shader not found (did it compile?).");
                return;
            }

            foreach (var pair in k_Presets)
            {
                var path = SkyPath(pair.Key);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(shader);
                    AssetDatabase.CreateAsset(material, path);
                }

                Apply(material, shader, pair.Value);
            }

            // The first three levels already point at their own generated sky assets; restyle those
            // in place so the existing scenes pick up the new sky without being rebuilt.
            foreach (var level in new[] { 1, 2, 3 })
            {
                var existing = AssetDatabase.LoadAssetAtPath<Material>(k_Materials + "Sky_Level" + level + ".mat");
                if (existing != null)
                    Apply(existing, shader, k_Presets["Level" + level]);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[DinoNet] Prehistoric sky materials ready.");
        }

        static void Apply(Material m, Shader shader, SkyPreset p)
        {
            m.shader = shader;
            m.SetColor("_Zenith", p.zenith);
            m.SetColor("_Mid", p.mid);
            m.SetColor("_Horizon", p.horizon);
            m.SetColor("_Haze", p.haze);
            m.SetColor("_Ground", p.ground);
            m.SetColor("_SunColor", p.sunColor);
            m.SetColor("_CloudColor", p.cloud);
            m.SetColor("_CloudShade", p.cloudShade);
            m.SetColor("_SmokeColor", p.smoke);
            var d = p.sunDir.normalized;
            m.SetVector("_SunDir", new Vector4(d.x, d.y, d.z, 0f));
            m.SetFloat("_SunSize", p.sunSize);
            m.SetFloat("_CloudCover", p.cover);
            m.SetFloat("_SmokeAmount", p.smokeAmount);
            // Points at the volcano as seen from the player's start (the volcano sits about 29m ahead, 4m right).
            m.SetFloat("_SmokeAzimuth", Mathf.Atan2(29f, 4f));
            m.SetFloat("_BirdAmount", p.birds);
            m.SetFloat("_StarStrength", p.stars);
            EditorUtility.SetDirty(m);
        }

        // ------------------------------------------------------------------ shader inclusion

        public static void EnsureShadersIncluded()
        {
            var names = new[]
            {
                "DinoNet/EnergyRoad", "DinoNet/TwilightSky", "DinoNet/PrehistoricSky", "Sprites/Default",
                "Universal Render Pipeline/Unlit", "Universal Render Pipeline/Particles/Unlit",
            };

            var settings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (settings == null)
            {
                Debug.LogWarning("[DinoNet] Could not open GraphicsSettings to add always-included shaders.");
                return;
            }

            var so = new SerializedObject(settings);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            var added = 0;
            foreach (var name in names)
            {
                var shader = Shader.Find(name);
                if (shader == null)
                    continue;

                var present = false;
                for (var i = 0; i < list.arraySize; i++)
                {
                    if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                    {
                        present = true;
                        break;
                    }
                }

                if (present)
                    continue;

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                added++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[DinoNet] Always-included shaders: added " + added + ".");
        }
    }
}
