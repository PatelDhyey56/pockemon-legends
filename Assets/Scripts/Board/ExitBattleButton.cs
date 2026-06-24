using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

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
        modalRt.sizeDelta = new Vector2(400f, 220f);
        modalRt.anchoredPosition = Vector2.zero;

        Image modalImg = modalWindow.GetComponent<Image>();
        modalImg.color = new Color(0.12f, 0.12f, 0.16f, 1f);

        // Inner Border
        GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderGo.transform.SetParent(modalWindow.transform, false);
        RectTransform borderRt = borderGo.GetComponent<RectTransform>();
        borderRt.anchorMin = Vector2.zero;
        borderRt.anchorMax = Vector2.one;
        borderRt.offsetMin = new Vector2(4f, 4f);
        borderRt.offsetMax = new Vector2(-4f, -4f);
        Image borderImg = borderGo.GetComponent<Image>();
        borderImg.color = new Color(0.18f, 0.18f, 0.24f, 1f);

        // 3. Title Text
        GameObject titleGo = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        titleGo.transform.SetParent(borderGo.transform, false);
        RectTransform titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.offsetMin = new Vector2(10f, -50f);
        titleRt.offsetMax = new Vector2(-10f, -15f);

        TMPro.TextMeshProUGUI titleTxt = titleGo.GetComponent<TMPro.TextMeshProUGUI>();
        titleTxt.text = "EXIT BATTLE";
        titleTxt.fontSize = 22f;
        titleTxt.fontStyle = TMPro.FontStyles.Bold;
        titleTxt.alignment = TMPro.TextAlignmentOptions.Center;
        titleTxt.color = new Color(0.9f, 0.3f, 0.3f, 1f); // Reddish color

        // 4. Description Text
        GameObject descGo = new GameObject("DescriptionText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        descGo.transform.SetParent(borderGo.transform, false);
        RectTransform descRt = descGo.GetComponent<RectTransform>();
        descRt.anchorMin = new Vector2(0f, 0f);
        descRt.anchorMax = new Vector2(1f, 1f);
        descRt.offsetMin = new Vector2(15f, 70f);
        descRt.offsetMax = new Vector2(-15f, -60f);

        TMPro.TextMeshProUGUI descTxt = descGo.GetComponent<TMPro.TextMeshProUGUI>();
        descTxt.text = "Are you sure you want to quit the battle?";
        descTxt.fontSize = 16f;
        descTxt.alignment = TMPro.TextAlignmentOptions.Center;
        descTxt.color = Color.white;

        // 5. Yes Button
        GameObject yesGo = new GameObject("YesButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
        yesGo.transform.SetParent(borderGo.transform, false);
        RectTransform yesRt = yesGo.GetComponent<RectTransform>();
        yesRt.anchorMin = new Vector2(0.5f, 0f);
        yesRt.anchorMax = new Vector2(0.5f, 0f);
        yesRt.pivot = new Vector2(0.5f, 0f);
        yesRt.sizeDelta = new Vector2(120f, 40f);
        yesRt.anchoredPosition = new Vector2(-70f, 20f);

        Image yesImg = yesGo.GetComponent<Image>();
        yesImg.color = new Color(0.9f, 0.3f, 0.3f, 1f); // Red

        GameObject yesTextGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        yesTextGo.transform.SetParent(yesGo.transform, false);
        RectTransform yesTextRt = yesTextGo.GetComponent<RectTransform>();
        yesTextRt.anchorMin = Vector2.zero;
        yesTextRt.anchorMax = Vector2.one;
        yesTextRt.offsetMin = Vector2.zero;
        yesTextRt.offsetMax = Vector2.zero;
        TMPro.TextMeshProUGUI yesTxt = yesTextGo.GetComponent<TMPro.TextMeshProUGUI>();
        yesTxt.text = "Yes";
        yesTxt.fontSize = 16f;
        yesTxt.fontStyle = TMPro.FontStyles.Bold;
        yesTxt.alignment = TMPro.TextAlignmentOptions.Center;
        yesTxt.color = Color.white;
        yesTxt.raycastTarget = false;

        UnityEngine.UI.Button yesBtn = yesGo.GetComponent<UnityEngine.UI.Button>();
        yesBtn.onClick.AddListener(() =>
        {
            Destroy(_confirmPopupInstance);

            // Record this as a defeat (loss) before returning to the main menu
            var profile = PlayerProfileManager.GetInstance();
            if (profile != null)
            {
                profile.RecordBattleResult(false);
            }

            SceneManager.LoadScene(Constants.SCENE_MENU);
        });

        // 6. No Button
        GameObject noGo = new GameObject("NoButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(UnityEngine.UI.Button));
        noGo.transform.SetParent(borderGo.transform, false);
        RectTransform noRt = noGo.GetComponent<RectTransform>();
        noRt.anchorMin = new Vector2(0.5f, 0f);
        noRt.anchorMax = new Vector2(0.5f, 0f);
        noRt.pivot = new Vector2(0.5f, 0f);
        noRt.sizeDelta = new Vector2(120f, 40f);
        noRt.anchoredPosition = new Vector2(70f, 20f);

        Image noImg = noGo.GetComponent<Image>();
        noImg.color = new Color(0.35f, 0.35f, 0.4f, 1f); // Grey

        GameObject noTextGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMPro.TextMeshProUGUI));
        noTextGo.transform.SetParent(noGo.transform, false);
        RectTransform noTextRt = noTextGo.GetComponent<RectTransform>();
        noTextRt.anchorMin = Vector2.zero;
        noTextRt.anchorMax = Vector2.one;
        noTextRt.offsetMin = Vector2.zero;
        noTextRt.offsetMax = Vector2.zero;
        TMPro.TextMeshProUGUI noTxt = noTextGo.GetComponent<TMPro.TextMeshProUGUI>();
        noTxt.text = "No";
        noTxt.fontSize = 16f;
        noTxt.fontStyle = TMPro.FontStyles.Bold;
        noTxt.alignment = TMPro.TextAlignmentOptions.Center;
        noTxt.color = Color.white;
        noTxt.raycastTarget = false;

        UnityEngine.UI.Button noBtn = noGo.GetComponent<UnityEngine.UI.Button>();
        noBtn.onClick.AddListener(() =>
        {
            Destroy(_confirmPopupInstance);
        });

        modalWindow.transform.localScale = Vector3.zero;
        modalWindow.transform.DOScale(1f, 0.4f).SetEase(Ease.OutBack);
    }
}
