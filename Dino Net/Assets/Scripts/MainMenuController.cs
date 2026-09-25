using UnityEngine;

namespace DinoNet
{
    /// <summary>
    /// Buttons on the main menu. Wired to <see cref="GameFlow"/> so scene names live in one place.
    /// Also drops the menu panel in front of wherever the headset is actually looking on the first
    /// frame - a fixed position baked in the editor lands in the wrong place once the rig is tracked.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField, Tooltip("Plain-transform anchor for the menu. Its canvas child carries the forward offset.")]
        Transform m_Panel;

        [SerializeField, Tooltip("Height of the anchor relative to the player's eyes.")]
        float m_HeightOffset = -0.15f;

        void Start()
        {
            PlaceInFrontOfPlayer();
        }

        public void PlaceInFrontOfPlayer()
        {
            if (m_Panel == null)
                return;

            var cam = Camera.main;
            if (cam == null)
                return;

            var forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            m_Panel.position = cam.transform.position + Vector3.up * m_HeightOffset;
            m_Panel.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>Starts the level the adaptive system suggests (Level 1 for a new player).</summary>
        public void PlayGame() => GameFlow.LoadLevel(ProgressStore.RecommendedLevel());

        public void PlaySandbox() => GameFlow.LoadSandbox();

        public void PlayTutorial() => GameFlow.LoadTutorial();

        public void ExitGame() => GameFlow.QuitGame();
    }
}
