using IAPPurchasing;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;

public class AdFreeView : View, IPurchaseListeners
{
    private static AdFreeView _instance;
    public GameObject popup;
    private Action _viewHideAction;
    [SerializeField] private string _nonConsumableProductId;

    public override void Awake()
    {
        base.Awake();
        _instance = this;
    }

    public static AdFreeView GetInstance()
    {
        return _instance;
    }
    
    protected override void OnViewShow()
    {
        base.OnViewShow();

        PopUpAnimation.ShowAnimation(popup);
    }

    protected override void OnViewHide()
    {
        base.OnViewHide();

        _viewHideAction?.Invoke();

        _viewHideAction = null;
    }


    public void OnLaterButtonClick()
    {
        Hide();
    }

    public void OnBuyButtonClick()
    {
        Hide();

        LoadingView.GetInstance().ShowLoading("Please wait...");

        PurchaseController.GetInstance().BuyNonConsumableProduct();
    }

    public override void OnBackeyPressed()
    {
        Hide();
    }

    public void ShowAdFreeView(Action hideCallBack)
    {
        _viewHideAction = hideCallBack;

        Show();
    }

    public void OnProductPurchased(Order order)
    {
        throw new NotImplementedException();
    }

    public void OnPurchaseFailed(FailedOrder order)
    {
        throw new NotImplementedException();
    }

    public void OnPurchasePending(PendingOrder order)
    {
        throw new NotImplementedException();
    }

    public void OnPurchaseDeffered(DeferredOrder order)
    {
        throw new NotImplementedException();
    }

    public void OnPurchaseFetched(Orders purchases)
    {
        throw new NotImplementedException();
    }

    public void OnRestoreSuccess(string result)
    {
        throw new NotImplementedException();
    }

    public void OnRestoreFailed(string result)
    {
        throw new NotImplementedException();
    }
}
