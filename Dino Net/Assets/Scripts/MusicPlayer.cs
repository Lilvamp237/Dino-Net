using UnityEngine;
using UnityEngine.SceneManagement;

namespace DinoNet
{
    /// <summary>
    /// Quiet background music that changes with the level's mood and crossfades between scenes.
    /// Tracks are generated clips in Resources/DinoAudio; a missing track just means silence.
    /// </summary>
    public class MusicPlayer : MonoBehaviour
    {
        const string k_Pref = "dn_music_on";
        const float k_Volume = 0.2f;

        static MusicPlayer s_Instance;

        AudioSource[] m_Sources;
        int m_Active;
        string m_Current;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(k_Pref, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(k_Pref, value ? 1 : 0);
                PlayerPrefs.Save();
                if (s_Instance != null)
                    s_Instance.Refresh();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (s_Instance != null)
                return;

            var go = new GameObject("Music Player");
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<MusicPlayer>();
        }

        void Awake()
        {
            m_Sources = new AudioSource[2];
            for (var i = 0; i < 2; i++)
            {
                m_Sources[i] = gameObject.AddComponent<AudioSource>();
                m_Sources[i].loop = true;
                m_Sources[i].playOnAwake = false;
                m_Sources[i].spatialBlend = 0f;
                m_Sources[i].volume = 0f;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (s_Instance == this)
                s_Instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            m_Current = TrackFor(scene.name);
            Refresh();
        }

        void Refresh()
        {
            var clip = Enabled ? Sfx.Get(m_Current) : null;
            var current = m_Sources[m_Active];
            if (clip != null && current.clip == clip && current.isPlaying)
                return;

            var next = m_Sources[1 - m_Active];
            if (clip != null)
            {
                next.clip = clip;
                next.time = 0f;
                next.Play();
            }

            m_Active = 1 - m_Active;
        }

        void Update()
        {
            // Fade the active source up and the other one down.
            for (var i = 0; i < 2; i++)
            {
                var target = (i == m_Active && m_Sources[i].clip != null && Enabled) ? k_Volume : 0f;
                m_Sources[i].volume = Mathf.MoveTowards(m_Sources[i].volume, target, Time.unscaledDeltaTime * 0.15f);
                if (m_Sources[i].volume <= 0.001f && i != m_Active && m_Sources[i].isPlaying)
                    m_Sources[i].Stop();
            }
        }

        static string TrackFor(string scene)
        {
            switch (scene)
            {
                case "MainMenu": return "music_menu";
                case "Tutorial": return "music_calm";
                case "Sandbox": return "music_calm";
                case "Level1":
                case "Level2":
                case "Level3": return "music_bright";
                case "Level4":
                case "Level5":
                case "Level6": return "music_warm";
                case "Level7":
                case "Level8": return "music_mystery";
                case "Level9":
                case "Level10": return "music_night";
                default: return null;
            }
        }
    }
}
