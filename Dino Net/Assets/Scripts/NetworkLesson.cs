using System;
using UnityEngine;

namespace DinoNet
{
    /// <summary>Which networking idea a lesson is teaching. Drives the colour language only.</summary>
    public enum LessonConcept
    {
        SafeConnection,
        RequestType,
        PrivateData,
    }

    /// <summary>One of the two choices a child can make at a decision point.</summary>
    [Serializable]
    public class LessonOption
    {
        [Tooltip("Big word on the button, e.g. \"HTTPS\" or \"GET\". Kept as the real technical term.")]
        public string label;

        [Tooltip("The child-friendly line underneath, e.g. \"A safer way to connect.\"")]
        public string sublabel;

        public Sprite icon;

        [Tooltip("Icon and border colour. Green reads as safe, amber as \"think again\", blue as neutral.")]
        public Color tint = Color.white;

        public bool correct;

        [Tooltip("Shown if this option is picked and it is not the right one. Explains rather than scolds.")]
        public string wrongFeedback;
    }

    /// <summary>
    /// A single decision the child meets while routing the packet: the dinosaur's question, the
    /// two choices, and what to say either way. Authored as an asset so the same decision can be
    /// reused across levels - Level 4 revisits the earlier ones.
    /// </summary>
    [CreateAssetMenu(menuName = "DinoNet/Network Lesson", fileName = "NetworkLesson")]
    public class NetworkLesson : ScriptableObject
    {
        public LessonConcept concept;

        [Tooltip("The technical term shown on the HUD so the concept being taught is visible, e.g. \"HTTP vs HTTPS\".")]
        public string conceptTerm;

        [Tooltip("0 asks the question at the podium before the first hop; 1 after one node is connected, and so on.")]
        public int triggerAfterNodes;

        [Tooltip("Who is asking, e.g. \"Spiky Stego asks:\"")]
        public string speaker;

        public string prompt;

        [Tooltip("Shown when the right choice is made.")]
        public string correctFeedback;

        [Tooltip("What the floating tag above the packet reads once this decision is made, e.g. \"HTTPS\".")]
        public string packetLabel = "DATA";

        [Tooltip("Colour of that packet tag.")]
        public Color packetTint = new Color(0.4f, 0.85f, 1f);

        [Tooltip("Short line shown on the HUD concept chip afterwards, e.g. \"HTTPS - secure connection\".")]
        public string conceptResult;

        [Tooltip("Exactly two options. Whichever has 'correct' ticked is the one that lets the route continue.")]
        public LessonOption[] options = new LessonOption[2];
    }
}
