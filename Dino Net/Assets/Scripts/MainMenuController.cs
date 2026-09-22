using UnityEngine;

namespace DinoNet
{
    /// <summary>Buttons on the main menu. Wired to <see cref="GameFlow"/> so scene names live in one place.</summary>
    public class MainMenuController : MonoBehaviour
    {
        public void PlayGame() => GameFlow.LoadLevel(1);

        public void PlayTutorial() => GameFlow.LoadTutorial();

        public void ExitGame() => GameFlow.QuitGame();
    }
}
