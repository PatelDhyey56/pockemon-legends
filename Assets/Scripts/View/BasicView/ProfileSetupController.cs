using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using DG.Tweening;

/// <summary>
/// First-run profile setup screen.
/// Shown only when no profile exists. Player enters a username to begin.
/// Scene name: "ProfileSetupScene"
/// </summary>
public class ProfileSetupController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private Button         confirmButton;
    [SerializeField] private TextMeshProUGUI errorText;
    [SerializeField] private CanvasGroup    panelGroup;

    [Header("Animation")]
    [SerializeField] private RectTransform cardRect;

    private void Start()
    {
        // Ensure timeScale is active on entering profile setup
        Time.timeScale = 1f;

        // This scene should only be entered if no profile exists.
        // If a profile is somehow present, skip straight to menu.
        if (PlayerProfileManager.GetInstance() != null &&
            PlayerProfileManager.GetInstance().IsProfileCreated)
        {
            SceneManager.LoadScene(Constants.SCENE_MENU);
            return;
        }

        if (errorText != null) errorText.text = "";

        FirebaseManager.LogEvent(Constants.EVENT_PROFILE_CREATE_OPEN);

        // Bounce-in animation using unscaled update to prevent freeze
        if (cardRect != null)
        {
            cardRect.localScale = Vector3.zero;
            DG.Tweening.DOTween.Sequence()
                .Append(cardRect.DOScale(1.05f, 0.35f).SetEase(DG.Tweening.Ease.OutBack))
                .Append(cardRect.DOScale(1f,    0.1f))
                .SetUpdate(true);
        }

        if (panelGroup != null)
        {
            panelGroup.alpha = 0f;
            panelGroup.DOFade(1f, 0.4f).SetUpdate(true);
        }

        // Limit username to 16 chars
        if (usernameInput != null)
            usernameInput.characterLimit = 16;

        if (confirmButton != null)
            confirmButton.onClick.AddListener(OnConfirmButtonClick);
    }

    public void OnConfirmButtonClick()
    {
        string name = usernameInput != null ? usernameInput.text.Trim() : "";

        if (string.IsNullOrEmpty(name))
        {
            ShowError("Please enter a username!");
            return;
        }

        if (name.Length < 2)
        {
            ShowError("Username must be at least 2 characters.");
            return;
        }

        if (name.Length > 12)
        {
            ShowError("Username cannot exceed 12 characters.");
            return;
        }

        // Create the profile
        PlayerProfileManager.GetInstance()?.CreateProfile(name);

        // Navigate to menu
        SceneManager.LoadScene(Constants.SCENE_MENU);
    }

    private void ShowError(string msg)
    {
        if (errorText == null) return;
        errorText.text = msg;
        errorText.color = Color.red;

        // Shake the input field for emphasis
        if (usernameInput != null)
        {
            usernameInput.transform.DOShakePosition(0.4f, 8f, 15, 90f, false, true);
        }
    }
}
