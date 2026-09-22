using UnityEngine;
using UnityEngine.SceneManagement;

namespace DinoNet
{
    /// <summary>
    /// One place that knows the scene names and how the game moves between them, so no level
    /// has to hardcode its own navigation.
    /// </summary>
    public static class GameFlow
    {
        public const string MainMenuScene = "MainMenu";
        public const string TutorialScene = "Tutorial";

        /// <summary>Highest level that exists. Level N lives in the scene "LevelN".</summary>
        public const int LastLevel = 4;

        public static string LevelScene(int levelNumber) => "Level" + levelNumber;

        public static bool HasLevel(int levelNumber) => levelNumber >= 1 && levelNumber <= LastLevel;

        public static void LoadMainMenu() => Load(MainMenuScene);

        public static void LoadTutorial() => Load(TutorialScene);

        public static void LoadLevel(int levelNumber)
        {
            if (!HasLevel(levelNumber))
            {
                LoadMainMenu();
                return;
            }

            Load(LevelScene(levelNumber));
        }

        public static void ReloadCurrentScene() => Load(SceneManager.GetActiveScene().name);

        /// <summary>Leaves play mode in the editor; quits the built application.</summary>
        public static void QuitGame()
        {
#if UNITY_EDITOR
            Debug.Log("[DinoNet] Quit requested - this only exits the game in a built player.");
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        static void Load(string sceneName)
        {
            // Everything lives in the scene, so a plain single load fully resets level state.
            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        }
    }
}
