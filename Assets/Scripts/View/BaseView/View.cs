using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class View : MonoBehaviour
{
    public GameObject canvasGameObject;
    public Camera viewCamera;
    public CanvasGroup viewCanvasGroup;
    protected bool isViewVisible;

    public virtual void Awake()
    {
        OnViewClosed();
    }

    /// <summary>
    /// Do not call this method directly.
    /// </summary>
    public void OnViewClosed()
    {
        canvasGameObject.SetActive(false);
        viewCamera.enabled = false;
        viewCanvasGroup.alpha = 0;
        isViewVisible = false;
        DisbaleUI();

        OnViewHide();
    }

    /// <summary>
    /// This will display the view
    /// </summary>
    public virtual void Show()
    {
        if (isViewVisible)
            return;

        canvasGameObject.SetActive(true);
        viewCamera.enabled = true;
        viewCanvasGroup.alpha = 1;
        isViewVisible = true;
        EnableUI();

        OnViewShow();

        BackKeyHandler.GetInstance().PushView(this);

    }


    /// <summary>
    /// This will hides the view
    /// </summary>
    public virtual void Hide()
    {
        if (isViewVisible)
        {
            BackKeyHandler.GetInstance().PopView();
        }
    }


    /// <summary>
    /// Android back key event
    /// </summary>
    public virtual void OnBackeyPressed()
    {

    }

    /// <summary>
    /// This method will be called when view is show
    /// Use this method to init view
    /// </summary>
    protected virtual void OnViewShow()
    {

    }

    /// <summary>
    /// This method will be calledn when view is hide
    /// Use this method to perform operation on view hide
    /// </summary>
    protected virtual void OnViewHide()
    {

    }

    /// <summary>
    /// This method will Disable Click Operation when UI is hidden.
    /// If Webview is availaible in the scene then enable the visiblity of webview.
    /// </summary>
    protected void DisbaleUI()
    {
        viewCanvasGroup.blocksRaycasts = false;
        viewCanvasGroup.interactable = false;
    }

    /// <summary>
    ///This method will Enable Click Operation when UI will show.
    /// </summary>
    protected void EnableUI()
    {
        viewCanvasGroup.blocksRaycasts = true;
        viewCanvasGroup.interactable = true;
    }

}

