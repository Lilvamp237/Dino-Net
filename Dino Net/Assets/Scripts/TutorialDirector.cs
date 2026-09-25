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

        [SerializeField, Tooltip("The decoy dinosaur the child is sent to on purpose, to learn what a wrong node looks like.")]
        QuestNode m_DecoyNode;

        [SerializeField, Tooltip("Volcano hazard used for the 'stay away' lesson.")]
        DangerZone m_Volcano;

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
        bool m_VisitedWrongNode;

        void OnEnable()
        {
            if (m_Quest != null)
            {
                m_Quest.QuestStarted += OnQuestStarted;
                m_Quest.NodeConnected += OnNodeConnected;
                m_Quest.QuestCompleted += OnQuestCompleted;
                m_Quest.WrongNodeVisited += OnWrongNodeVisited;
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
                m_Quest.WrongNodeVisited -= OnWrongNodeVisited;
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

            // Step 3 - the goal.
            yield return Say("Your job is to help the message get to the other dinosaur.");

            // Step 3b - on purpose, send them to a dinosaur that is NOT on the route, so they
            // learn what a wrong node looks like. Reaching the real node also moves things on,
            // so a child who ignores the detour can never get stuck here.
            if (m_DecoyNode != null && !m_VisitedWrongNode)
            {
                yield return Say("Let's try that other dinosaur first. Take the message to it!");
                yield return new WaitUntil(() => m_VisitedWrongNode || m_NodesConnected >= 1);

                if (m_VisitedWrongNode)
                {
                    yield return Say("Oops! That's not the node we need.");
                    yield return Say("The floor didn't turn green, so no connection was made.");
                    yield return Say("You went to the wrong node. Let's find the right one!");
                }
            }

            // Step 3c - the volcano is dangerous and costs you time.
            if (m_Volcano != null)
            {
                yield return Say("See the glowing volcano? Take a peek - but don't get too close!");
                yield return new WaitUntil(() => m_Volcano.PlayerInside || m_NodesConnected >= 1);

                if (m_Volcano.PlayerInside)
                {
                    yield return Say("Careful! Don't get too close to the volcano!");
                    yield return Say("Near a volcano your timer runs down faster.");
                    yield return Say("Let's stay away from dangerous areas.");
                }
            }

            yield return Say("Now follow the Firefly to the blue node!");
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

        void OnWrongNodeVisited(QuestNode node) => m_VisitedWrongNode = true;

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
