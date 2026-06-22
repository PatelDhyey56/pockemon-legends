using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using Firebase.Analytics;
using AdsManager;

public class SplashManager : MonoBehaviour
{
    public float delay;
    public CanvasGroup canvasGroup;
    public GameSettings gameSettings;

    /// <summary>
    /// Prefab (or in-scene GameObject) that holds the PlayerProfileManager component.
    /// If the manager already exists (DontDestroyOnLoad), this is skipped.
    /// </summary>
    [SerializeField] private GameObject playerProfileManagerPrefab;

    private void Start()
    {
        // Set target frame rate to 60 FPS for buttery smooth animations and responsiveness
        Application.targetFrameRate = 60;
        FadeEffect();

        // Bootstrap the profile manager early so it persists across scenes
        EnsureProfileManager();

        StartCoroutine(LoadScene());
        FirebaseManager.CheckFireBaseDependency();
    }

    private void EnsureProfileManager()
    {
        if (PlayerProfileManager.GetInstance() == null)
        {
            if (playerProfileManagerPrefab != null)
            {
                Instantiate(playerProfileManagerPrefab);
            }
            else
            {
                // Fallback: create a bare GameObject with the component
                GameObject go = new GameObject("PlayerProfileManager");
                go.AddComponent<PlayerProfileManager>();
            }
        }
    }

    private void GameLaunchFirebaseEvent()
    {
        FirebaseManager.LogEvent(Constants.EVENT_GAME_LAUNCH);
    }

    public IEnumerator LoadScene()
    {
        GameLaunchFirebaseEvent();
        yield return new WaitForSeconds(delay);

        // Route based on whether a profile has been created
        var profile = PlayerProfileManager.GetInstance();
        if (profile == null || !profile.IsProfileCreated)
        {
            SceneManager.LoadScene(Constants.SCENE_PROFILE_SETUP);
        }
        else
        {
            SceneManager.LoadScene(Constants.SCENE_MENU);
        }
    }

    public void FadeEffect()
    {
        canvasGroup.DOFade(1, 0.5f);
    }
}
