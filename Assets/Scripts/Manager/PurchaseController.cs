using IAPPurchasing;
using System;
using UnityEngine;
using UnityEngine.Purchasing;

public class PurchaseController : MonoBehaviour, IPurchaseListeners
{
    private static PurchaseController _instance;

    public static Action OnRemoveAd;
    public static Action OnPurchaseFail;
    public static Action OnRestoreSuccess;
    public static Action OnRestoreFailed;


    [SerializeField] private string _nonConsumableProduct_Android;
    [SerializeField] private string _nonConsumableProduct_iOS;

    public void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }
    private void Start()
    {
        PurchaseManager.GetInstance().AddPurchaseListener(this);
    }

    //private void ()
    //{
    //    PurchaseManager.GetInstance().RemovePurchaseListener(this);
    //    PurchaseManager.OnRestoreSuccess -= OnRestoreComplete;
    //    PurchaseManager.OnRestoreFailed -= OnRestoreFail;
    //}

    public static PurchaseController GetInstance()
    {
        return _instance;
    }

    public void RemoveAd()
    {
        OnRemoveAd?.Invoke();
        // Save the preference
    }

    public string GetNonConsumableProductID()
    {
#if UNITY_ANDROID
        return _nonConsumableProduct_Android;
#elif UNITY_IOS
        return _nonConsumableProduct_iOS;
#endif
    }

    public void BuyNonConsumableProduct()
    {
        PurchaseManager.GetInstance().BuyProduct(GetNonConsumableProductID());
    }

    private void OnRestoreComplete(string result)
    {
        OnRestoreSuccess?.Invoke();
    }

    private void OnRestoreFail(string result)
    {
        OnRestoreFailed?.Invoke();
    }

    public void OnProductPurchased(Order order)
    {
        LogHelper.Info("PurchaseController", "product is purchased : " + order.Info.PurchasedProductInfo[0].productId);
        RemoveAd();
    }

    public void OnPurchaseFailed(FailedOrder order)
    {
        OnPurchaseFail?.Invoke();
    }

    public void OnPurchasePending(PendingOrder order)
    {
        PurchaseManager.GetInstance().ProcessPurchase(order);
    }

    public void OnPurchaseDeffered(DeferredOrder order)
    {
        throw new NotImplementedException();
    }

    public void OnPurchaseFetched(Orders purchases)
    {
        throw new NotImplementedException();
    }

    void IPurchaseListeners.OnRestoreSuccess(string result)
    {
        OnRestoreComplete(result);
    }

    void IPurchaseListeners.OnRestoreFailed(string result)
    {
        OnRestoreFail(result);
    }
}
