using System;
using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Minimal v1 stub: counts successful packet deliveries for a future HUD/objectives system.
    /// </summary>
    public class LearningProgressTracker : MonoBehaviour
    {
        public static LearningProgressTracker Instance { get; private set; }

        public int PacketsDelivered { get; private set; }

        public event Action<int> DeliveryCountChanged;

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
    }
}
