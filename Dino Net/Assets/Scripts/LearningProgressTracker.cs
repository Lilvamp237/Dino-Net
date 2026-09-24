using System;
using System.Collections.Generic;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Session-wide learning record: how many packets were delivered and which networking
    /// ideas the child actually applied along the way.
    /// </summary>
    public class LearningProgressTracker : MonoBehaviour
    {
        public static LearningProgressTracker Instance { get; private set; }

        public int PacketsDelivered { get; private set; }

        readonly List<string> m_Concepts = new List<string>();

        /// <summary>Networking ideas applied so far this session, in the order they were met.</summary>
        public IReadOnlyList<string> ConceptsLearned => m_Concepts;

        public event Action<int> DeliveryCountChanged;

        public event Action<string> ConceptLearned;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void RecordDelivery()
        {
            PacketsDelivered++;
            DeliveryCountChanged?.Invoke(PacketsDelivered);
        }

        /// <summary>Notes a networking idea the child has just applied, e.g. "HTTP vs HTTPS".</summary>
        public void RecordConceptLearned(string conceptTerm)
        {
            if (string.IsNullOrEmpty(conceptTerm) || m_Concepts.Contains(conceptTerm))
                return;

            m_Concepts.Add(conceptTerm);
            ConceptLearned?.Invoke(conceptTerm);
        }
    }
}
