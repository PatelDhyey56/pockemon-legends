using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

public class MessageView : View
{
    private const float _scaleTime = 0.3f;
    public static MessageView _instance;
    private Action _buttonClickAction;
    public TMP_Text gameNameText;
    public TMP_Text buttonText;
    public TMP_Text messageText;
    public GameSettings gameSettings;
    public GameObject popup;

    public override void Awake()
    {
        base.Awake();
        _instance = this;
    }

    private void Start()
    {
        gameNameText.text = gameSettings.GetGameName();
    }

    public static MessageView GetInstance()
    {
        return _instance;
    }

    public override void OnBackeyPressed()
    {
        Hide();
    }

    protected override void OnViewShow()
    {
        base.OnViewShow();
        PopUpAnimation.ShowAnimation(popup);
    }

    public void OnButtonClick()
    {
        _buttonClickAction?.Invoke();

        Hide();
    }

    public void ShowMessageView(string message, string buttonText)
    {
        ShowMessageView(message, buttonText, null);
    }

    public void ShowMessageView(string message, string buttonText, Action action)
    {
        _buttonClickAction = action;

        this.buttonText.text = buttonText;

        this.messageText.text = message;

        Show();
    }
}
