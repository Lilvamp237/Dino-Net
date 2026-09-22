using System.Collections;
using TMPro;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Walks a child through one complete delivery, explaining the networking idea in plain
    /// words. Every step waits for something the child actually did - picking the packet up,
    /// reaching a node, seeing the firefly search - rather than running on a timer.
    /// </summary>
    public class TutorialDirector : MonoBehaviour
    {
        [Header("Scene references")]
        [SerializeField]
        DinoQuestManager m_Quest;

        [SerializeField]
        GuideFirefly m_Firefly;

        [SerializeField]
        CarryableOrb m_Orb;

        [Header("Panel")]
        [SerializeField]
        GameObject m_PanelRoot;

        [SerializeField]
        TMP_Text m_PanelText;

        [SerializeField, Tooltip("Shown at the very end with the three choices.")]
        GameObject m_FinishPanel;

        [Header("Pacing")]
        [SerializeField, Tooltip("Shortest time a line stays up before the next one can replace it, so lines aren't missed.")]
        float m_MinimumReadSeconds = 2.5f;

        [Header("Audio")]
        [SerializeField]
        AudioSource m_AudioSource;

        [SerializeField]
        AudioClip m_StepClip;

        bool m_OrbPickedUp;
        bool m_FireflyHinted;
        int m_NodesConnected;
        bool m_Completed;

        void OnEnable()
        {
            if (m_Quest != null)
            {
                m_Quest.QuestStarted += OnQuestStarted;
                m_Quest.NodeConnected += OnNodeConnected;
                m_Quest.QuestCompleted += OnQuestCompleted;
            }

            if (m_Orb != null)
                m_Orb.FirstPickedUp += OnOrbPickedUp;

            if (m_Firefly != null)
                m_Firefly.RouteHintStarted += OnFireflyHint;
        }

        void OnDisable()
        {
            if (m_Quest != null)
            {
                m_Quest.QuestStarted -= OnQuestStarted;
                m_Quest.NodeConnected -= OnNodeConnected;
                m_Quest.QuestCompleted -= OnQuestCompleted;
            }

            if (m_Orb != null)
                m_Orb.FirstPickedUp -= OnOrbPickedUp;

            if (m_Firefly != null)
                m_Firefly.RouteHintStarted -= OnFireflyHint;
        }

        void Start()
        {
            if (m_FinishPanel != null)
                m_FinishPanel.SetActive(false);

            StartCoroutine(RunTutorial());
        }

        IEnumerator RunTutorial()
        {
            // Step 1 - welcome, with both dinosaurs lit up so "network point" has something to point at.
            HighlightNodes(true);
            yield return Say("Welcome! Let's do this!");
            yield return Say("These dinosaurs are connected points in our network.");
            HighlightNodes(false);
            yield return Say("Press the big green button to begin!");

            yield return new WaitUntil(() => m_Quest != null && m_Quest.IsRunning);

            // Step 2 - the packet is a message, and it comes from a dinosaur.
            yield return Say("See this glowing orb? It is a message travelling through the network.");
            yield return Say("Pick the message up!");
            yield return new WaitUntil(() => m_OrbPickedUp);

            // Step 3 - the goal, then wait for them to actually reach the first node.
            yield return Say("Your job is to help the message get to the other dinosaur.");
            yield return Say("Go to the blue node!");
            yield return new WaitUntil(() => m_NodesConnected >= 1);

            // Step 4 - what the colour change meant.
            yield return Say("The floor turned green! That means you found the right node.");
            yield return Say("Now carry the message to the next node.");

            // Step 5 - the firefly, only once it has actually searched for a route.
            if (!m_Completed)
            {
                yield return new WaitUntil(() => m_FireflyHinted || m_Completed);

                if (!m_Completed)
                {
                    yield return Say("The Firefly helps find the way for the message.");
                    yield return Say("The Firefly is looking for a good path!");
                }
            }

            // Step 6 - arrival at the destination.
            yield return new WaitUntil(() => m_Completed);
            yield return Say("Yay! The message reached the right dinosaur!");
            yield return Say("You just sent a message through a network!");

            // Step 7 - what next.
            yield return Say("Tutorial complete! Are you ready to play now?");

            if (m_FinishPanel != null)
                m_FinishPanel.SetActive(true);
        }

        /// <summary>Pulses both node rings so "these dinosaurs are connected points" has something to look at.</summary>
        void HighlightNodes(bool on)
        {
            if (m_Quest == null)
                return;

            foreach (var node in m_Quest.Route)
            {
                if (node != null)
                    node.SetHinted(on);
            }
        }

        IEnumerator Say(string line)
        {
            if (m_PanelRoot != null)
                m_PanelRoot.SetActive(true);

            if (m_PanelText != null)
                m_PanelText.text = line;

            if (m_AudioSource != null && m_StepClip != null)
                m_AudioSource.PlayOneShot(m_StepClip);

            yield return new WaitForSeconds(m_MinimumReadSeconds);
        }

        void OnQuestStarted() { }

        void OnOrbPickedUp() => m_OrbPickedUp = true;

        void OnFireflyHint(QuestNode node) => m_FireflyHinted = true;

        void OnNodeConnected(QuestNode node) => m_NodesConnected++;

        void OnQuestCompleted() => m_Completed = true;

        // ---- Finish panel buttons ----

        public void PlayLevelOne() => GameFlow.LoadLevel(1);

        public void RestartTutorial() => GameFlow.LoadTutorial();

        public void ReturnToMainMenu() => GameFlow.LoadMainMenu();
    }
}
