using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DinoNet
{
    /// <summary>
    /// Teaches signal strength. While the child carries the packet its signal slowly weakens with
    /// distance. Glowing booster stones on the road top it back up - like relay towers. If the
    /// signal runs out, the packet fizzles and returns to the last dinosaur (nothing is lost for
    /// good, and the level clock keeps running so it is a gentle setback, not a failure).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class SignalMechanic : MonoBehaviour
    {
        DinoQuestManager m_Quest;
        CarryableOrb m_Orb;
        RoadNetwork m_Roads;

        float m_Strength = 1f;
        float m_Range = 20f;
        float m_Walked;
        Vector3 m_LastPos;
        bool m_HaveLast;
        Vector3 m_LegStart;
        Vector3 m_StartPosition;
        bool m_Running;
        bool m_Explained;
        bool m_WarnedThisLeg;
        bool m_Recovering;

        GameObject m_Booster;
        Vector3 m_BoosterPos;
        Image m_Fill;
        TMP_Text m_Label;
        GameObject m_Bar;

        void Start()
        {
            m_Quest = FindFirstObjectByType<DinoQuestManager>();
            m_Roads = FindFirstObjectByType<RoadNetwork>();
            if (m_Quest == null || m_Quest.Orb == null)
            {
                enabled = false;
                return;
            }

            m_Orb = m_Quest.Orb;
            BuildBar();
            m_Quest.QuestStarted += OnStarted;
            m_Quest.NodeConnected += OnNode;
        }

        void OnDestroy()
        {
            if (m_Quest != null)
            {
                m_Quest.QuestStarted -= OnStarted;
                m_Quest.NodeConnected -= OnNode;
            }

            DestroyBooster();
        }

        void BuildBar()
        {
            var canvas = RuntimeUi.WorldCanvas("Signal Bar", new Vector2(300f, 70f), 0.0016f, m_Orb.transform, false);
            canvas.transform.localPosition = new Vector3(0f, 0.95f, 0f);
            canvas.transform.localScale = Vector3.one * 0.0016f / Mathf.Max(0.05f, m_Orb.transform.lossyScale.x);
            canvas.gameObject.AddComponent<Billboard>();
            RuntimeUi.Panel(canvas.transform, "Background", new Color(0.03f, 0.06f, 0.1f, 0.85f), Vector2.zero, new Vector2(300f, 70f));
            m_Fill = RuntimeUi.Panel(canvas.transform, "Fill", new Color(0.3f, 1f, 0.6f), new Vector2(0f, -10f), new Vector2(270f, 22f), RuntimeUi.Square);
            m_Fill.type = Image.Type.Filled;
            m_Fill.fillMethod = Image.FillMethod.Horizontal;
            m_Fill.fillAmount = 1f;
            m_Label = RuntimeUi.Text(canvas.transform, "Label", "SIGNAL", 30f, Color.white, new Vector2(0f, 16f), new Vector2(280f, 34f));
            m_Bar = canvas.gameObject;
            m_Bar.SetActive(false);
        }

        void OnStarted()
        {
            m_Running = true;
            m_StartPosition = m_Orb.transform.position;
            m_LegStart = m_StartPosition;
            PrepareLeg(0);
            if (m_Bar != null)
                m_Bar.SetActive(true);
        }

        void OnNode(QuestNode node)
        {
            m_LegStart = node.DeliveryAnchor.position;
            PrepareLeg(m_Quest.ConnectedCount);
        }

        void PrepareLeg(int index)
        {
            DestroyBooster();
            m_Strength = 1f;
            m_Walked = 0f;
            m_HaveLast = false;
            m_WarnedThisLeg = false;

            if (index >= m_Quest.Route.Count)
            {
                if (m_Bar != null)
                    m_Bar.SetActive(false);
                return;
            }

            var target = m_Quest.Route[index];
            var path = MechanicsUtil.RoadPath(m_Roads, m_LegStart, target.DeliveryAnchor.position);
            var length = RoadNetwork.PathLength(path);
            m_Range = Mathf.Max(18f, length * 0.9f);

            if (length > 14f)
            {
                m_BoosterPos = MechanicsUtil.PointAlong(path, length * 0.5f, out _);
                BuildBooster(m_BoosterPos);
            }
        }

        void BuildBooster(Vector3 position)
        {
            m_Booster = new GameObject("Signal Booster");
            m_Booster.transform.position = new Vector3(position.x, 0f, position.z);

            var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pad.GetComponent<Collider>());
            pad.transform.SetParent(m_Booster.transform, false);
            pad.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            pad.transform.localScale = new Vector3(2.4f, 0.03f, 2.4f);
            pad.GetComponent<Renderer>().sharedMaterial = MechanicsUtil.GlowMaterial(new Color(0.3f, 1f, 0.75f));

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(beam.GetComponent<Collider>());
            beam.transform.SetParent(m_Booster.transform, false);
            beam.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            beam.transform.localScale = new Vector3(0.35f, 1.6f, 0.35f);
            beam.GetComponent<Renderer>().sharedMaterial = MechanicsUtil.TransparentMaterial(new Color(0.3f, 1f, 0.75f, 0.32f));

            var lightGo = new GameObject("Light");
            lightGo.transform.SetParent(m_Booster.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.4f, 1f, 0.8f);
            light.range = 7f;
            light.intensity = 2.4f;

            var tag = MechanicsUtil.Tag("BOOSTER", new Color(0.5f, 1f, 0.85f), new Vector3(position.x, 3.4f, position.z), 420f, 60f);
            tag.transform.SetParent(m_Booster.transform, true);
        }

        void DestroyBooster()
        {
            if (m_Booster != null)
                Destroy(m_Booster);

            m_Booster = null;
        }

        void Update()
        {
            if (!m_Running || m_Recovering || m_Quest.DeliveryPaused || !m_Quest.IsRunning || m_Quest.CurrentTarget == null)
                return;

            var pos = m_Orb.transform.position;
            pos.y = 0f;

            if (m_Orb.IsHeld)
            {
                if (m_HaveLast)
                    m_Walked += Vector3.Distance(pos, m_LastPos);

                m_LastPos = pos;
                m_HaveLast = true;
            }
            else
            {
                m_HaveLast = false;
            }

            // Passing a booster stone tops the signal back up.
            if (m_Booster != null && m_Strength < 0.98f && Vector3.Distance(pos, new Vector3(m_BoosterPos.x, 0f, m_BoosterPos.z)) < 2.2f)
            {
                m_Walked = 0f;
                Sfx.Play2D("booster", 0.8f);
                RuntimeUi.Toast("Signal boosted!", new Color(0.4f, 1f, 0.8f), 2.2f);
                SessionTelemetry.Log("signal_boost", SessionTelemetry.Instance != null ? SessionTelemetry.Instance.CurrentLevel : 0);
            }

            m_Strength = Mathf.Clamp01(1f - m_Walked / m_Range);
            UpdateBar();

            if (!m_Explained && m_Strength < 0.85f)
            {
                m_Explained = true;
                RuntimeUi.Toast("Long roads make the signal weaker. Boosters make it strong again!", new Color(0.55f, 0.85f, 1f), 5f);
                VoiceOver.Speak("Long roads make the signal weaker. Boosters make it strong again!");
            }

            if (!m_WarnedThisLeg && m_Strength < 0.3f)
            {
                m_WarnedThisLeg = true;
                RuntimeUi.Toast("The signal is getting weak! Find the glowing booster.", new Color(1f, 0.8f, 0.4f), 3.5f);
            }

            if (m_Strength <= 0.001f)
                StartCoroutine(Fizzle());
        }

        void UpdateBar()
        {
            if (m_Fill == null)
                return;

            m_Fill.fillAmount = m_Strength;
            m_Fill.color = m_Strength > 0.55f ? new Color(0.3f, 1f, 0.6f) : m_Strength > 0.25f ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 0.35f, 0.3f);
            if (m_Label != null)
                m_Label.text = m_Strength > 0.55f ? "SIGNAL STRONG" : m_Strength > 0.25f ? "SIGNAL WEAK" : "SIGNAL FADING";
        }

        IEnumerator Fizzle()
        {
            m_Recovering = true;
            m_Quest.DeliveryPaused = true;
            Sfx.Play2D("lost_whoosh", 0.8f);
            RuntimeUi.Toast("The signal ran out! The message goes back to the last dino.", new Color(1f, 0.7f, 0.4f), 4f);
            VoiceOver.Speak("The signal ran out! The message goes back to the last dinosaur.");
            SessionTelemetry.Log("signal_lost", SessionTelemetry.Instance != null ? SessionTelemetry.Instance.CurrentLevel : 0);

            var last = m_Quest.ConnectedCount > 0 ? m_Quest.Route[m_Quest.ConnectedCount - 1].DeliveryAnchor.position : m_StartPosition;
            MechanicsUtil.ReturnOrb(m_Orb, last, m_StartPosition.y);

            yield return new WaitForSeconds(1.6f);

            m_Walked = 0f;
            m_Strength = 1f;
            m_HaveLast = false;
            m_Quest.DeliveryPaused = false;
            m_Recovering = false;
        }
    }
}
