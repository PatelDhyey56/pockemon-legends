using AdsManager;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ExitView : View
{
    public static ExitView _instance;
    public TMP_Text gameNameText;
    public GameSettings gameSettings;
    public GameObject popup;

    public override void Awake()
    {
        base.Awake();
        _instance = this;
    }

    public static ExitView GetInstance()
    {
        if (_instance == null)
        {
            _instance = FindFirstObjectByType<ExitView>(FindObjectsInactive.Include);
        }
        return _instance;
    }

    private void Start()
    {
        gameNameText.text = gameSettings.GetGameName();
        DisableButtonTextRaycasts(gameObject);
    }

    private void DisableButtonTextRaycasts(GameObject parent)
    {
        if (parent == null) return;
        var buttons = parent.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        foreach (var btn in buttons)
        {
            var texts = btn.GetComponentsInChildren<TMPro.TMP_Text>(true);
            foreach (var txt in texts)
            {
                txt.raycastTarget = false;
            }
            var images = btn.GetComponentsInChildren<UnityEngine.UI.Image>(true);
            foreach (var img in images)
            {
                if (img.gameObject != btn.gameObject)
                {
                    img.raycastTarget = false;
                }
            }
        }
    }

    public override void OnBackeyPressed()
    {
        AdMobManager.GetInstance().ShowBanner();
        Hide();
    }

    protected override void OnViewShow()
    {
        AdMobManager.GetInstance().HideBanner();

        popup.transform.localScale = Vector3.zero;

        base.OnViewShow();

        PopUpAnimation.ShowAnimation(popup);
    }

    public void OnYesButtonClick()
    {
        Application.Quit();
    }

    public void OnNoButtonClick()
    {
        AdMobManager.GetInstance().ShowBanner();
        Hide();
    }
}
