using System.Collections;
using System.Collections.Generic;
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

    private void Start()
    {
        FadeEffect();

        StartCoroutine(LoadScene());

        FirebaseManager.CheckFireBaseDependency();
    }

    private void GameLaunchFirebaseEvent()
    {
       FirebaseManager.LogEvent(Constants.EVENT_GAME_LAUNCH);
    }

    public IEnumerator LoadScene()
    {
        GameLaunchFirebaseEvent();
        yield return new WaitForSeconds(delay);
        AdMobManager.GetInstance().SetAdmobAdsID();
        SceneManager.LoadScene(Constants.SCENE_MENU);
    }

    public void FadeEffect()
    {
        canvasGroup.DOFade(1, 0.5f);
    }
}
