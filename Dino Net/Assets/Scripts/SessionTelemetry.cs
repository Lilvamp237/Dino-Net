using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace DinoNet
{
    /// <summary>
    /// Records what happens in a play session - how long the child plays, which levels they try,
    /// what they answer - and sends it to the teacher/parent dashboard.
    ///
    /// There is deliberately no login: a child is an anonymous device id, shown to a parent as a
    /// short family code. Events are saved on the headset first and uploaded whenever the
    /// dashboard is reachable, so a missing network never interrupts play.
    /// </summary>
    public class SessionTelemetry : MonoBehaviour
    {
        [Serializable]
        class Config
        {
            public string url = "http://localhost:3000";
            public string apiKey = "dino-demo-key";
            public bool enabled = true;
        }

        [Serializable]
        class QueuedEvent
        {
            public string sessionId;
            public long t;
            public string type;
            public int level;
            public string data;
        }

        [Serializable]
        class QueueFile
        {
            public List<QueuedEvent> events = new List<QueuedEvent>();
        }

        const string k_DeviceKey = "dn_device_id";
        const string k_TotalKey = "dn_total_seconds";
        const int k_MaxQueued = 4000;
        const float k_HeartbeatSeconds = 30f;

        static readonly string[] s_Adjectives = { "Brave", "Speedy", "Sunny", "Clever", "Happy", "Mighty", "Gentle", "Curious", "Jolly", "Zippy", "Cozy", "Bold" };
        static readonly string[] s_Dinos = { "Raptor", "Stego", "Trike", "Ptero", "Rex", "Brachio", "Anky", "Para", "Diplo", "Spino" };
        const string k_CodeChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        public static SessionTelemetry Instance { get; private set; }

        readonly List<QueuedEvent> m_Queue = new List<QueuedEvent>();
        Config m_Config = new Config();
        string m_QueuePath;
        bool m_Dirty;
        float m_ActiveSeconds;
        float m_BaseTotalSeconds;
        float m_NextHeartbeat = k_HeartbeatSeconds;
        float m_NextSave;
        float m_Backoff = 6f;
        bool m_Uploading;
        bool m_Ended;

        public string DeviceId { get; private set; }
        public string SessionId { get; private set; }
        public string FamilyCode { get; private set; }
        public string Nickname { get; private set; }

        /// <summary>The level currently being played (0 = menu or tutorial).</summary>
        public int CurrentLevel { get; set; }

        /// <summary>Seconds actually spent playing this session (paused/unfocused time doesn't count).</summary>
        public float ActiveSeconds => m_ActiveSeconds;

        /// <summary>Lifetime seconds played on this headset, including this session.</summary>
        public float TotalActiveSeconds => m_BaseTotalSeconds + m_ActiveSeconds;

        public int PendingEvents => m_Queue.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (Instance != null)
                return;

            var go = new GameObject("Session Telemetry");
            DontDestroyOnLoad(go);
            go.AddComponent<SessionTelemetry>();
        }

        /// <summary>Records an event. Key/value pairs follow the type, e.g. Log("wrong_node", 3, "node", "Crest Head").</summary>
        public static void Log(string type, int level, params object[] keyValues)
        {
            if (Instance != null)
                Instance.Record(type, level, keyValues);
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            LoadConfig();
            LoadIdentity();
            m_QueuePath = Path.Combine(Application.persistentDataPath, "dn_queue.json");
            LoadQueue();
            m_BaseTotalSeconds = PlayerPrefs.GetFloat(k_TotalKey, 0f);

            SessionId = Guid.NewGuid().ToString("N").Substring(0, 16);
            ProgressStore.TouchStreak();

            Record("session_start", 0, "platform", PlatformName(), "version", Application.version, "streak", ProgressStore.Streak);
            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(UploadLoop());
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        void OnDestroy()
        {
            if (Instance == this)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Instance = null;
            }
        }

        void Update()
        {
            if (Application.isFocused)
                m_ActiveSeconds += Mathf.Min(Time.unscaledDeltaTime, 1f);

            if (m_ActiveSeconds >= m_NextHeartbeat)
            {
                m_NextHeartbeat = m_ActiveSeconds + k_HeartbeatSeconds;
                Record("heartbeat", CurrentLevel, "activeSeconds", (int)m_ActiveSeconds, "scene", SceneManager.GetActiveScene().name);
                PlayerPrefs.SetFloat(k_TotalKey, TotalActiveSeconds);
            }

            if (m_Dirty && Time.unscaledTime >= m_NextSave)
                SaveQueue();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused)
                return;

            // The headset came off or the app was backgrounded: bank what we have.
            Record("heartbeat", CurrentLevel, "activeSeconds", (int)m_ActiveSeconds, "scene", SceneManager.GetActiveScene().name);
            PlayerPrefs.SetFloat(k_TotalKey, TotalActiveSeconds);
            PlayerPrefs.Save();
            SaveQueue();
        }

        void OnApplicationQuit()
        {
            EndSession();
        }

        /// <summary>Marks the session finished, saves everything and makes one quick last upload attempt.</summary>
        public void EndSession()
        {
            if (m_Ended)
                return;

            m_Ended = true;
            Record("session_end", 0, "activeSeconds", (int)m_ActiveSeconds);
            PlayerPrefs.SetFloat(k_TotalKey, TotalActiveSeconds);
            PlayerPrefs.Save();
            SaveQueue();
            FlushBlocking();
        }

        int m_LastSceneHandle = -1;
        int m_LastSceneFrame = -1;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // The first scene can be reported twice (bootstrap + the event that created us).
            if (scene.handle == m_LastSceneHandle && Time.frameCount == m_LastSceneFrame)
                return;

            m_LastSceneHandle = scene.handle;
            m_LastSceneFrame = Time.frameCount;
            CurrentLevel = 0;
            Record("scene_enter", 0, "scene", scene.name);

            var holder = new GameObject("Telemetry Bridge");
            holder.AddComponent<TelemetryBridge>();
        }

        // ---------------------------------------------------------------- identity & config

        void LoadConfig()
        {
            try
            {
                var asset = Resources.Load<TextAsset>("telemetry");
                if (asset != null)
                    m_Config = JsonUtility.FromJson<Config>(asset.text) ?? m_Config;
            }
            catch (Exception)
            {
                // Keep defaults.
            }

            if (string.IsNullOrEmpty(m_Config.url))
                m_Config.enabled = false;

            m_Config.url = (m_Config.url ?? string.Empty).TrimEnd('/');
        }

        void LoadIdentity()
        {
            DeviceId = PlayerPrefs.GetString(k_DeviceKey, string.Empty);
            if (string.IsNullOrEmpty(DeviceId))
            {
                DeviceId = Guid.NewGuid().ToString("N").Substring(0, 16);
                PlayerPrefs.SetString(k_DeviceKey, DeviceId);
                PlayerPrefs.Save();
            }

            // Same recipe as the dashboard (dashboard/lib/analytics.js), so the code a parent
            // sees in the game is the code the dashboard recognises.
            using (var sha = SHA1.Create())
            {
                var h = sha.ComputeHash(Encoding.UTF8.GetBytes(DeviceId));
                var code = new StringBuilder();
                for (var i = 0; i < 6; i++)
                    code.Append(k_CodeChars[h[i + 2] % k_CodeChars.Length]);

                FamilyCode = code.ToString();
                Nickname = s_Adjectives[h[0] % s_Adjectives.Length] + " " + s_Dinos[h[1] % s_Dinos.Length];
            }
        }

        static string PlatformName()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android: return "Quest";
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.WindowsPlayer: return "Windows PC";
                default: return Application.platform.ToString();
            }
        }

        // ---------------------------------------------------------------- recording

        void Record(string type, int level, params object[] keyValues)
        {
            if (m_Queue.Count >= k_MaxQueued)
                m_Queue.RemoveAt(0);

            m_Queue.Add(new QueuedEvent
            {
                sessionId = SessionId,
                t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                type = type,
                level = level,
                data = BuildData(keyValues),
            });

            m_Dirty = true;
            if (m_NextSave < Time.unscaledTime)
                m_NextSave = Time.unscaledTime + 5f;
        }

        static string BuildData(object[] kv)
        {
            if (kv == null || kv.Length < 2)
                return "{}";

            var sb = new StringBuilder("{");
            for (var i = 0; i + 1 < kv.Length; i += 2)
            {
                if (i > 0)
                    sb.Append(',');

                sb.Append('"').Append(Escape(Convert.ToString(kv[i], CultureInfo.InvariantCulture))).Append("\":");
                AppendValue(sb, kv[i + 1]);
            }

            return sb.Append('}').ToString();
        }

        static void AppendValue(StringBuilder sb, object v)
        {
            switch (v)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case long l: sb.Append(l.ToString(CultureInfo.InvariantCulture)); break;
                case float f: sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("0.###", CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(double.IsNaN(d) || double.IsInfinity(d) ? "null" : d.ToString("0.###", CultureInfo.InvariantCulture)); break;
                default: sb.Append('"').Append(Escape(Convert.ToString(v, CultureInfo.InvariantCulture))).Append('"'); break;
            }
        }

        static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return string.Empty;

            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ").Replace("\t", " ");
        }

        // ---------------------------------------------------------------- persistence

        void LoadQueue()
        {
            try
            {
                if (!File.Exists(m_QueuePath))
                    return;

                var file = JsonUtility.FromJson<QueueFile>(File.ReadAllText(m_QueuePath));
                if (file != null && file.events != null)
                    m_Queue.AddRange(file.events);
            }
            catch (Exception)
            {
                // A damaged queue file is not worth failing the game over.
            }
        }

        void SaveQueue()
        {
            m_Dirty = false;
            m_NextSave = Time.unscaledTime + 10f;

            try
            {
                File.WriteAllText(m_QueuePath, JsonUtility.ToJson(new QueueFile { events = m_Queue }));
            }
            catch (Exception)
            {
                // Ignore - the in-memory queue still uploads.
            }
        }

        // ---------------------------------------------------------------- upload

        string BuildBatch(string sessionId, List<QueuedEvent> events)
        {
            var sb = new StringBuilder(events.Count * 96);
            sb.Append("{\"deviceId\":\"").Append(Escape(DeviceId)).Append("\",\"sessionId\":\"").Append(Escape(sessionId)).Append("\",\"events\":[");
            for (var i = 0; i < events.Count; i++)
            {
                var e = events[i];
                if (i > 0)
                    sb.Append(',');

                sb.Append("{\"t\":").Append(e.t.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"type\":\"").Append(Escape(e.type)).Append('"')
                    .Append(",\"level\":").Append(e.level.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"data\":").Append(string.IsNullOrEmpty(e.data) ? "{}" : e.data).Append('}');
            }

            return sb.Append("]}").ToString();
        }

        /// <summary>Takes up to 200 queued events that share one session id (the oldest session first).</summary>
        List<QueuedEvent> NextBatch()
        {
            var batch = new List<QueuedEvent>();
            if (m_Queue.Count == 0)
                return batch;

            var session = m_Queue[0].sessionId;
            foreach (var e in m_Queue)
            {
                if (e.sessionId != session)
                    continue;

                batch.Add(e);
                if (batch.Count >= 200)
                    break;
            }

            return batch;
        }

        IEnumerator UploadLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(m_Backoff);
                if (!m_Config.enabled || m_Uploading || m_Queue.Count == 0)
                    continue;

                m_Uploading = true;
                var batch = NextBatch();
                var body = Encoding.UTF8.GetBytes(BuildBatch(batch[0].sessionId, batch));

                using (var request = new UnityWebRequest(m_Config.url + "/api/ingest", "POST"))
                {
                    request.uploadHandler = new UploadHandlerRaw(body);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");
                    request.SetRequestHeader("x-api-key", m_Config.apiKey);
                    request.timeout = 8;
                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200)
                    {
                        foreach (var sent in batch)
                            m_Queue.Remove(sent);

                        m_Backoff = m_Queue.Count > 0 ? 1.5f : 6f;
                        m_Dirty = true;
                    }
                    else
                    {
                        // Offline or the dashboard is down: keep everything and try again later.
                        m_Backoff = Mathf.Min(m_Backoff * 2f, 60f);
                    }
                }

                m_Uploading = false;
            }
        }

        /// <summary>One short synchronous attempt when the app closes, so the tail of a session isn't left behind.</summary>
        void FlushBlocking()
        {
            if (!m_Config.enabled || m_Queue.Count == 0)
                return;

            try
            {
                for (var pass = 0; pass < 4 && m_Queue.Count > 0; pass++)
                {
                    var batch = NextBatch();
                    var body = Encoding.UTF8.GetBytes(BuildBatch(batch[0].sessionId, batch));
                    var request = (HttpWebRequest)WebRequest.Create(m_Config.url + "/api/ingest");
                    request.Method = "POST";
                    request.ContentType = "application/json";
                    request.Headers["x-api-key"] = m_Config.apiKey;
                    request.Timeout = 1500;
                    request.ReadWriteTimeout = 1500;
                    using (var stream = request.GetRequestStream())
                        stream.Write(body, 0, body.Length);

                    using (var response = (HttpWebResponse)request.GetResponse())
                    {
                        if ((int)response.StatusCode != 200)
                            return;
                    }

                    foreach (var sent in batch)
                        m_Queue.Remove(sent);
                }

                SaveQueue();
            }
            catch (Exception)
            {
                // Offline: everything stays queued on the headset for next time.
            }
        }
    }
}
