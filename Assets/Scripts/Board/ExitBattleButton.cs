using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class ExitBattleButton : MonoBehaviour
{
    private GameObject _confirmPopupInstance;

    public void GoToHomeScene()
    {
        ShowExitConfirmationPopup();
    }

    private void ShowExitConfirmationPopup()
    {
        if (_confirmPopupInstance != null)
        {
            Destroy(_confirmPopupInstance);
        }

        Canvas rootCanvas = FindFirstObjectByType<Canvas>();
        if (rootCanvas == null) return;

        // 1. Blocker Overlay
        _confirmPopupInstance = new GameObject("ExitConfirmPopup", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        _confirmPopupInstance.transform.SetParent(rootCanvas.transform, false);

        RectTransform overlayRt = _confirmPopupInstance.GetComponent<RectTransform>();
        overlayRt.anchorMin = Vector2.zero;
        overlayRt.anchorMax = Vector2.one;
        overlayRt.offsetMin = Vector2.zero;
        overlayRt.offsetMax = Vector2.zero;

        Image overlayImg = _confirmPopupInstance.GetComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.8f);

        // 2. Modal Window
        GameObject modalWindow = new GameObject("ModalWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        modalWindow.transform.SetParent(_confirmPopupInstance.transform, false);

        RectTransform modalRt = modalWindow.GetComponent<RectTransform>();
        modalRt.anchorMin = new Vector2(0.5f, 0.5f);
        modalRt.anchorMax = new Vector2(0.5f, 0.5f);
        modalRt.pivot = new Vector2(0.5f, 0.5f);
        modalRt.sizeDelta = new Vector2(780f, 900f);
        modalRt.anchoredPosition = Vector2.zero;

        Image modalImg = modalWindow.GetComponent<Image>();
        Sprite[] popupSprites = Resources.LoadAll<Sprite>("buttons/popup");
        Sprite bgSprite = System.Array.Find(popupSprites, s => s.name == "popup_2");
        if (bgSprite != null)
        {
            modalImg.sprite = bgSprite;
            modalImg.type = Image.Type.Simple;
        }
        modalImg.color = Color.white;

        // 3. Title Text
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleGo.transform.SetParent(modalWindow.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 1f);
        titleRt.anchorMax = new Vector2(0.5f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(700f, 80f);
        titleRt.anchoredPosition = new Vector2(0f, -55f);

        TextMeshProUGUI titleTxt = titleGo.GetComponent<TextMeshProUGUI>();
        titleTxt.text = "EXIT BATTLE";
        titleTxt.fontSize = 46f;
        titleTxt.fontStyle = FontStyles.Bold;
        titleTxt.alignment = TextAlignmentOptions.Center;
        titleTxt.color = Color.white;

        // 4. Description Text
        GameObject descGo = new GameObject("DescriptionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        descGo.transform.SetParent(modalWindow.transform, false);
        RectTransform descRt = descGo.GetComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0.5f, 0.5f);
        descRt.anchorMax = new Vector2(0.5f, 0.5f);
        descRt.pivot = new Vector2(0.5f, 0.5f);
        descRt.sizeDelta = new Vector2(680f, 200f);
        descRt.anchoredPosition = new Vector2(0f, 20f);

        TextMeshProUGUI descTxt = descGo.GetComponent<TextMeshProUGUI>();
        descTxt.text = "Are you sure you want to quit the battle?";
        descTxt.fontSize = 28f;
        descTxt.enableWordWrapping = true;
        descTxt.lineSpacing = 1.15f;
        descTxt.alignment = TextAlignmentOptions.Center;
        descTxt.color = Color.white;

        // 5. Load button sprites
        Sprite[] popupBtnSprites = Resources.LoadAll<Sprite>("buttons/popup");
        Sprite yesBtnSprite = System.Array.Find(popupBtnSprites, s => s.name == "popup_0");
        Sprite noBtnSprite = System.Array.Find(popupBtnSprites, s => s.name == "popup_1");

        // 6. Yes Button (200x70, posY = 65)
        GameObject yesGo = new GameObject("YesButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
        yesGo.transform.SetParent(modalWindow.transform, false);
        RectTransform yesRt = yesGo.GetComponent<RectTransform>();
        yesRt.anchorMin = new Vector2(0.5f, 0f);
        yesRt.anchorMax = new Vector2(0.5f, 0f);
        yesRt.pivot = new Vector2(0.5f, 0f);
        yesRt.sizeDelta = new Vector2(200f, 70f);
        yesRt.anchoredPosition = new Vector2(-110f, 65f);

        Image yesImg = yesGo.GetComponent<Image>();
        if (yesBtnSprite != null)
        {
            yesImg.sprite = yesBtnSprite;
            yesImg.type = Image.Type.Simple;
        }
        yesImg.color = Color.white;

        UnityEngine.UI.Button yesBtn = yesGo.GetComponent<UnityEngine.UI.Button>();
        yesBtn.onClick.AddListener(() =>
        {
            Destroy(_confirmPopupInstance);

            var profile = PlayerProfileManager.GetInstance();
            if (profile != null)
                profile.RecordBattleResult(false);

            SceneManager.LoadScene(Constants.SCENE_MENU);
        });

        // 7. No Button (200x70, posY = 65)
        GameObject noGo = new GameObject("NoButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
        noGo.transform.SetParent(modalWindow.transform, false);
        RectTransform noRt = noGo.GetComponent<RectTransform>();
        noRt.anchorMin = new Vector2(0.5f, 0f);
        noRt.anchorMax = new Vector2(0.5f, 0f);
        noRt.pivot = new Vector2(0.5f, 0f);
        noRt.sizeDelta = new Vector2(200f, 70f);
        noRt.anchoredPosition = new Vector2(110f, 65f);

        Image noImg = noGo.GetComponent<Image>();
        if (noBtnSprite != null)
        {
            noImg.sprite = noBtnSprite;
            noImg.type = Image.Type.Simple;
        }
        noImg.color = Color.white;

        UnityEngine.UI.Button noBtn = noGo.GetComponent<UnityEngine.UI.Button>();
        noBtn.onClick.AddListener(() =>
        {
            Destroy(_confirmPopupInstance);
        });

        modalWindow.transform.localScale = Vector3.zero;
        modalWindow.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
    }
}
