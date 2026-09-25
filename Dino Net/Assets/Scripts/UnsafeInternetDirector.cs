using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DinoNet
{
    /// <summary>
    /// Turns the volcano and T-Rex hazards into a lesson about the unsafe parts of the internet.
    /// The existing danger zones already drain the clock; this layer explains why in kind words,
    /// gives one repeatable rule (step back, don't click or share, tell a grown-up), and shows
    /// whether the packet is protected by an HTTPS shield.
    /// Nothing frightening: the tone is "this part is not safe yet - here is what to do".
    /// </summary>
    public class UnsafeInternetDirector : MonoBehaviour
    {
        DangerZone[] m_Zones = new DangerZone[0];
        Light m_OrbLight;
        Color m_OrbLightColor;
        CarryableOrb m_Orb;

        GameObject m_Hud;
        CanvasGroup m_HudGroup;
        TMP_Text m_Title;
        TMP_Text m_ShieldLine;
        Image m_ShieldDot;

        GameObject m_Bubble;
        bool m_Inside;
        float m_LastWarning = -100f;
        float m_Show;
        int m_EntriesThisLevel;

        /// <summary>The packet is protected once the child has met the HTTPS idea in this game.</summary>
        public static bool ShieldActive => ProgressStore.Mastery(ConceptCatalog.Https) >= 0f;

        void Start()
        {
            m_Zones = FindObjectsByType<DangerZone>(FindObjectsSortMode.None);
            var quest = FindFirstObjectByType<DinoQuestManager>();
            m_Orb = quest != null ? quest.Orb : null;
            if (m_Orb != null)
            {
                m_OrbLight = m_Orb.GetComponentInChildren<Light>(true);
                if (m_OrbLight != null)
                    m_OrbLightColor = m_OrbLight.color;
            }

            BuildHud();
            BuildBubble();
        }

        void BuildHud()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            var canvas = RuntimeUi.WorldCanvas("Unsafe Internet HUD", new Vector2(1100f, 330f), 0.0011f, cam.transform, false);
            canvas.transform.localPosition = new Vector3(0f, -0.28f, 1.5f);
            canvas.transform.localRotation = Quaternion.identity;
            m_HudGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            m_HudGroup.alpha = 0f;
            m_Hud = canvas.gameObject;

            RuntimeUi.Panel(canvas.transform, "Background", new Color(0.35f, 0.08f, 0.05f, 0.9f), Vector2.zero, new Vector2(1100f, 330f));
            RuntimeUi.Panel(canvas.transform, "Bar", new Color(1f, 0.6f, 0.2f, 1f), new Vector2(0f, 150f), new Vector2(1100f, 16f));
            m_Title = RuntimeUi.Text(canvas.transform, "Title", "UNSAFE INTERNET AREA", 58f, new Color(1f, 0.9f, 0.7f), new Vector2(0f, 96f), new Vector2(1040f, 80f));
            RuntimeUi.Text(canvas.transform, "Rule", "Step back  -  don't click or share  -  tell a grown-up", 42f, Color.white, new Vector2(0f, 20f), new Vector2(1040f, 70f));
            m_ShieldDot = RuntimeUi.Panel(canvas.transform, "ShieldDot", Color.gray, new Vector2(-470f, -85f), new Vector2(46f, 46f), RuntimeUi.Dot);
            m_ShieldLine = RuntimeUi.Text(canvas.transform, "Shield", "", 38f, Color.white, new Vector2(40f, -85f), new Vector2(920f, 70f), TextAlignmentOptions.Left);
        }

        void BuildBubble()
        {
            if (m_Orb == null)
                return;

            m_Bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            m_Bubble.name = "HTTPS Shield Bubble";
            Destroy(m_Bubble.GetComponent<Collider>());
            m_Bubble.transform.SetParent(m_Orb.transform, false);
            m_Bubble.transform.localPosition = Vector3.zero;
            m_Bubble.transform.localScale = Vector3.one * 2.4f;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = 3000;
            var tint = new Color(0.35f, 0.8f, 1f, 0.28f);
            material.SetColor("_BaseColor", tint);
            material.color = tint;
            m_Bubble.GetComponent<Renderer>().sharedMaterial = material;
            m_Bubble.SetActive(false);
        }

        void Update()
        {
            var inside = false;
            foreach (var zone in m_Zones)
            {
                if (zone != null && zone.PlayerInside)
                {
                    inside = true;
                    break;
                }
            }

            if (inside && !m_Inside)
                OnEnterUnsafe();
            else if (!inside && m_Inside)
                OnLeaveUnsafe();

            m_Inside = inside;

            // Fade the warning strip in and out.
            m_Show = Mathf.MoveTowards(m_Show, inside ? 1f : 0f, Time.unscaledDeltaTime * 3f);
            if (m_HudGroup != null)
                m_HudGroup.alpha = m_Show;

            UpdatePacketProtection(inside);
        }

        void OnEnterUnsafe()
        {
            m_EntriesThisLevel++;
            var shield = ShieldActive;
            if (m_Title != null)
                m_Title.text = "UNSAFE INTERNET AREA";

            if (m_ShieldLine != null)
            {
                m_ShieldLine.text = shield
                    ? "Your HTTPS shield helps protect the message - but still don't stay here."
                    : "No HTTPS shield yet! Messages here are easy to peek at.";
                m_ShieldLine.color = shield ? new Color(0.6f, 1f, 0.85f) : new Color(1f, 0.85f, 0.5f);
            }

            if (m_ShieldDot != null)
                m_ShieldDot.color = shield ? new Color(0.35f, 0.9f, 1f) : new Color(1f, 0.6f, 0.2f);

            if (Time.time - m_LastWarning > 20f)
            {
                m_LastWarning = Time.time;
                Sfx.Play2D("warning_pulse", 0.6f);
                VoiceOver.Speak("This is the unsafe part of the internet. Step back and tell a grown-up.");
            }

            SessionTelemetry.Log("unsafe_area", SessionTelemetry.Instance != null ? SessionTelemetry.Instance.CurrentLevel : 0,
                "shield", shield, "entry", m_EntriesThisLevel);
        }

        void OnLeaveUnsafe()
        {
            RuntimeUi.Toast("Good job stepping back to safety!", new Color(0.4f, 1f, 0.6f), 2.5f);
        }

        /// <summary>Shows the packet as protected (blue bubble) or exposed (red glow) while it is in a hazard.</summary>
        void UpdatePacketProtection(bool insideHazard)
        {
            var carrying = m_Orb != null && m_Orb.gameObject.activeInHierarchy;
            var exposedNow = insideHazard && carrying;

            if (m_Bubble != null)
                m_Bubble.SetActive(exposedNow && ShieldActive);

            if (m_OrbLight != null)
            {
                if (exposedNow && !ShieldActive)
                    m_OrbLight.color = Color.Lerp(m_OrbLight.color, new Color(1f, 0.25f, 0.15f), Time.deltaTime * 6f);
                else
                    m_OrbLight.color = Color.Lerp(m_OrbLight.color, m_OrbLightColor, Time.deltaTime * 4f);
            }
        }
    }
}
