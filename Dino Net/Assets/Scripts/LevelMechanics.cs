using UnityEngine;
using UnityEngine.SceneManagement;

namespace DinoNet
{
    /// <summary>
    /// Attaches the shared feature layers to whichever scene just loaded: living dinosaurs
    /// everywhere, and in the playable levels the Golden Firefly analysis, the unsafe-internet
    /// lesson, the adaptive coach, rewards, and each level's own mechanic. Doing it at runtime
    /// means the level scenes stay simple and no hand-placed objects can go missing.
    /// </summary>
    public class LevelMechanics : MonoBehaviour
    {
        static bool s_Registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (s_Registered)
                return;

            s_Registered = true;
            SceneManager.sceneLoaded += (scene, mode) => new GameObject("Level Mechanics").AddComponent<LevelMechanics>();
        }

        void Start()
        {
            foreach (var anim in FindObjectsByType<Animation>(FindObjectsSortMode.None))
            {
                if (anim.GetComponent<DinoAnimator>() == null)
                    anim.gameObject.AddComponent<DinoAnimator>();
            }

            var level = FindFirstObjectByType<LevelManager>();
            if (level == null)
                return;

            var number = level.LevelNumber;

            var firefly = FindFirstObjectByType<GuideFirefly>();
            if (firefly != null && firefly.GetComponent<FireflyAnalysis>() == null)
                firefly.gameObject.AddComponent<FireflyAnalysis>();

            if (number <= 0)
                return;

            gameObject.AddComponent<UnsafeInternetDirector>();
            gameObject.AddComponent<AdaptiveCoach>();
            gameObject.AddComponent<RewardPresenter>();

            switch (number)
            {
                case 5:
                    gameObject.AddComponent<SignalMechanic>();
                    gameObject.AddComponent<PiecesMechanic>();
                    break;

                case 6:
                    gameObject.AddComponent<LostPacketMechanic>().LostIndex = 1;
                    gameObject.AddComponent<BlockedRoadMechanic>().AfterNodes = 3;
                    break;

                case 7:
                    gameObject.AddComponent<AddressTags>();
                    break;

                case 9:
                    gameObject.AddComponent<GateMechanic>().AfterNodes = 2;
                    break;

                case 10:
                    gameObject.AddComponent<SignalMechanic>();
                    gameObject.AddComponent<LostPacketMechanic>().LostIndex = 2;
                    gameObject.AddComponent<BlockedRoadMechanic>().AfterNodes = 3;
                    gameObject.AddComponent<GateMechanic>().AfterNodes = 4;
                    break;
            }
        }
    }
}
