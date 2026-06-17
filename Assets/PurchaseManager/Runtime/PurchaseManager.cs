using IAPPurchasing.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Purchasing;

namespace IAPPurchasing
{
    [System.Serializable]
    public class EntitlementProduct
    {
        public string productId;
        public bool isEntitled;
    }

    public interface IPurchaseListeners
    {
        void OnPurchaseFetched(Orders purchases);
        void OnProductPurchased(Order order);
        void OnPurchaseFailed(FailedOrder order);
        void OnPurchasePending(PendingOrder order);
        void OnPurchaseDeffered(DeferredOrder order);
        void OnRestoreSuccess(string result);
        void OnRestoreFailed(string result);
    }

    public class PurchaseManager : MonoBehaviour
    {
        public bool printLog = true;
        private static PurchaseManager _instance;

        private static Action<Order> OnPurchaseProduct;
        private static Action<FailedOrder> OnPurchaseFail;
        private static Action<PendingOrder> OnPurchaseOrderPending;
        private static Action<DeferredOrder> OnPurchaseProductDeffered;
        private static Action<string> OnRestoreSuccess;
        private static Action<string> OnRestoreFailed;
        private static Action<Orders> OnPurchaseFetched;
        public static Action<Entitlement> OnCheckedEntitlement;

        public bool initializeOnStart = true;
        public InAppData inAppData;
        private StoreController _storeController;
        public List<EntitlementProduct> entitlementProducts;

        private bool _isInitializeCompleted = false;

        private List<ProductDefinition> _productDefinitions = new List<ProductDefinition>();

        private List<PendingOrder> _pedingOrders = new List<PendingOrder>();
        public bool ProductInitCompleted
        {
            get;
            private set;
        } = false;

        #region UNITY_LIFECYCLE_METHODS

        private void Awake()
        {
            IAPDebug.canPrintLog = printLog;

            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(this);
            }
            else
            {
                Destroy(this.gameObject);
            }
        }

        public IEnumerator Start()
        {
            //we will wait for the internet connection for the sdk init
            yield return new WaitUntil(() => Application.internetReachability != NetworkReachability.NotReachable);
            if (initializeOnStart)
            {
                InitializeIAP();
            }
        }

        #endregion

        #region IAP INITIALIZATION

        public static PurchaseManager GetInstance()
        {
            return _instance;
        }

        public void InitializeIAP()
        {
            Dictionary<string, int> allProductsList = new Dictionary<string, int>();
            foreach (var product in inAppData.GetAllProducts())
            {
                allProductsList.Add(product.ProductId, (int)product.productType);
            }
            InitializeIAP(allProductsList);
        }

        /// <summary>
        /// Initialize IAP with product list Key as a product id and Value as a product type 
        /// 0-Consumable,1-NonConsumable,2-Subscription
        /// </summary>
        /// <param name="productsIdType"></param>
        public async void InitializeIAP(Dictionary<string, int> productsIdType)
        {
            try
            {
                if (IsInitialized())
                {
                    IAPDebug.Log("IAP is already initialized.");
                    return;
                }

                IAPDebug.Log("Initializing IAP Started");
                _storeController = UnityIAPServices.StoreController();

                _storeController.OnPurchasePending += OnPurchasePending;
                _storeController.OnProductsFetched += OnProductsFetched;
                _storeController.OnPurchasesFetched += OnPurchasesFetched;
                _storeController.OnCheckEntitlement += OnCheckEntitlementProduct;
                _storeController.OnPurchaseConfirmed += OnProductPurchased;
                _storeController.OnPurchaseFailed += OnPurchaseFailed;
                _storeController.OnProductsFetchFailed += OnProductsFetchFailed;
                _storeController.OnPurchaseDeferred += OnPurchaseDeffered;

                _storeController.OnStoreDisconnected += (message) =>
                {
                    IAPDebug.Log("OnStoreDisconnected: " + message);
                };

                await _storeController.Connect();

                CatalogProvider catalogProvider = new CatalogProvider();
                _productDefinitions = new List<ProductDefinition>();
                foreach (var product in productsIdType)
                {
                    catalogProvider.AddProduct(product.Key, (ProductType)product.Value);

                    _productDefinitions.Add(new ProductDefinition(product.Key, (ProductType)product.Value));

                    IAPDebug.Log("Product added for initialization: " + product.Key + " Type: " + (ProductType)product.Value);
                }

                _storeController.FetchProducts(_productDefinitions);

                _isInitializeCompleted = true;
                IAPDebug.Log("IAP Initialized completed");
            }
            catch (Exception e)
            {
                IAPDebug.Log("INIT IAP: Exception " + e.Message);
            }

        }

        public bool IsInitialized()
        {
            return _storeController != null && _isInitializeCompleted;
        }

        public void AddPurchaseListener(IPurchaseListeners listener)
        {
            OnPurchaseProduct += listener.OnProductPurchased;
            OnPurchaseFail += listener.OnPurchaseFailed;
            OnPurchaseOrderPending += listener.OnPurchasePending;
            OnPurchaseProductDeffered += listener.OnPurchaseDeffered;
            OnRestoreSuccess += listener.OnRestoreSuccess;
            OnRestoreFailed += listener.OnRestoreFailed;
            OnPurchaseFetched += listener.OnPurchaseFetched;
        }

        public void RemovePurchaseListener(IPurchaseListeners listener)
        {
            OnPurchaseProduct -= listener.OnProductPurchased;
            OnPurchaseFail -= listener.OnPurchaseFailed;
            OnPurchaseOrderPending -= listener.OnPurchasePending;
            OnPurchaseProductDeffered -= listener.OnPurchaseDeffered;
            OnRestoreSuccess -= listener.OnRestoreSuccess;
            OnRestoreFailed -= listener.OnRestoreFailed;
            OnPurchaseFetched -= listener.OnPurchaseFetched;
        }
        #endregion

        #region PRODUCT PURCHASE, RESTORE AND STATUS CHECKING

        public void BuyProduct(string productId)
        {
            try
            {
                IAPDebug.Log("BuyProduct: " + productId);
                if (!IsInitialized())
                {
                    OnPurchaseFail?.Invoke(null);
                    IAPDebug.Log("BuyProduct: Not initialized.");
                    return;
                }

                Product product = _storeController.GetProductById(productId);

                // If the look up found a product for this device's store and that product is ready to be sold ... 
                if (product != null && product.availableToPurchase)
                {
                    IAPDebug.Log(string.Format("Purchasing product asychronously: '{0}'", product.definition.id));
                    _storeController.PurchaseProduct(product);
                }
                else
                {
                    OnPurchaseFail?.Invoke(null);
                    IAPDebug.Log("BuyProduct: FAIL. Not purchasing product, either is not found or is not available for purchase " + productId);
                }
            }
            catch (Exception e)
            {
                OnPurchaseFail?.Invoke(null);
                IAPDebug.Log("PurchaseManager BuyProduct: " + e.Message);
            }
        }

        // Restore purchases previously made by this customer. Some platforms automatically restore purchases, like Google. 
        // Apple currently requires explicit purchase restoration for IAP, conditionally displaying a password prompt.
        public void RestorePurchases()
        {
            IAPDebug.Log("RestorePurchases: START");
            // If Purchasing has not yet been set up ...
            if (!IsInitialized())
            {
                // ... report the situation and stop restoring. Consider either waiting longer, or retrying initialization.
                OnRestoreFailed?.Invoke(null);
                IAPDebug.Log("RestorePurchases FAIL. Not initialized.");
                return;
            }

            IAPDebug.Log("RestorePurchases started ...");

            // If we are running on an Apple device ... 
            if (Application.platform == RuntimePlatform.IPhonePlayer ||
                Application.platform == RuntimePlatform.OSXPlayer)
            {
                _storeController.RestoreTransactions((sucess, result) =>
                {
                    IAPDebug.Log("RestorePurchases: result : " + result);
                    if (sucess)
                    {
                        OnRestoreSuccess?.Invoke(result);
                        IAPDebug.Log("RestorePurchases: successful!");
                    }
                    else
                    {
                        OnRestoreFailed?.Invoke(result);
                        IAPDebug.Error("RestorePurchases: failed.");
                    }
                });

            }
            // Otherwise ...
            else
            {
                // We are not running on an Apple device. No work is necessary to restore purchases.
                OnRestoreFailed?.Invoke(null);
                IAPDebug.Log("RestorePurchases FAIL. Not supported on this platform. Current = " + Application.platform);
            }
        }


        public void CheckEntitlement(string productId)
        {
            Product product = _storeController.GetProductById(productId);
            if (product != null)
            {
                _storeController.CheckEntitlement(product);
            }
        }

        private void SaveEntitlementProductData(string productId, int value)
        {
            PlayerPrefs.SetInt(productId, value);
            PlayerPrefs.Save();
        }

        //For Checking Entitlement Product Status
        public bool IsProductEntitled(string productId)
        {
            int value = PlayerPrefs.GetInt(productId, 0);
            return value == 1;
        }

        public void ProcessPurchase(PendingOrder order)
        {
            _storeController.ConfirmPurchase(order);
        }

        #endregion

        #region Product Callbacks

        private void OnProductsFetched(List<Product> products)
        {
            if (products == null || products.Count == 0)
            {
                IAPDebug.Log("OnProductsFetched: No products found");
                return;
            }
            // Handle fetched products  
            _storeController.FetchPurchases();
            IAPDebug.Log("OnProductsFetched: " + products.Count);
            foreach (var product in products)
            {
                IAPDebug.Log("OnProductsFetched: Product: " + product.definition.id + ", Price: " + product.metadata.localizedPriceString);
            }
        }

        private void OnProductsFetchFailed(ProductFetchFailed failed)
        {
            IAPDebug.Error("POnProductsFetchFailed: " + failed.FailureReason);
            StartCoroutine(FetchRetry());
        }

        private IEnumerator FetchRetry()
        {
            if (_productDefinitions == null || _productDefinitions.Count <= 0)
            {
                IAPDebug.Log("Fetch Retry: failed due to no products to fetch");
                yield break;
            }
            yield return new WaitForSeconds(2);
            _storeController.FetchProducts(_productDefinitions);
        }

        private void OnPurchasesFetched(Orders orders)
        {
            try
            {
                OnPurchaseFetched?.Invoke(orders);
                CheckPendingOrdersForPurchasesFetched(orders);
                CheckConfirmedOrdersForPurchasesFetched(orders);

                IAPDebug.Log("On Purchases Fetched Init Completed");
                ProductInitCompleted = true;
            }
            catch (Exception ex)
            {
                IAPDebug.Error("OnPurchaseFetched : Exception : " + ex.Message);
            }
        }

        private void CheckPendingOrdersForPurchasesFetched(Orders orders)
        {
            if (orders == null || orders.ConfirmedOrders == null || orders.ConfirmedOrders.Count == 0 || orders.ConfirmedOrders[0].Info == null || orders.ConfirmedOrders[0].Info.PurchasedProductInfo == null)
            {
                IAPDebug.Log("OnPurchase Fetched orders data  or confirmed orders data is null");
                return;
            }

            foreach (var order in orders.ConfirmedOrders)
            {
                foreach (var product in order.Info.PurchasedProductInfo)
                {
                    IAPDebug.Log("OnPurchasesFetched: Purchased product: " + product.productId);
                }
            }
        }

        private void CheckConfirmedOrdersForPurchasesFetched(Orders orders)
        {
            if (orders == null || orders.PendingOrders == null || orders.PendingOrders.Count == 0 || orders.PendingOrders[0].Info == null || orders.PendingOrders[0].Info.PurchasedProductInfo == null)
            {
                IAPDebug.Log("OnPurchase Fetched orders data  or pending orders data is null");
                return;
            }

            foreach (var order in orders.PendingOrders)
            {
                foreach (var product in order.Info.PurchasedProductInfo)
                {
                    _pedingOrders.Add(order);
                    IAPDebug.Log("OnPurchasesFetched: Purchased product: " + product.productId);
                }
            }
        }

        public void OnProductPurchased(Order order)
        {
            try
            {
                OnPurchaseProduct?.Invoke(order);

                if (order == null || order.Info == null || order.Info.PurchasedProductInfo == null || order.Info.PurchasedProductInfo.Count == 0)
                {
                    IAPDebug.Log("OnProductPurchased: Order or Order data is null");
                    return;
                }

                IAPDebug.Log(string.Format("OnProductPurchased: PASS. Product: '{0}'", order.Info.PurchasedProductInfo[0].productId));
            }
            catch (Exception ex)
            {
                IAPDebug.Error("OnProduct Purchased : Exception : " + ex.Message);
            }
        }

        public void OnPurchaseFailed(FailedOrder order)
        {
            try
            {
                OnPurchaseFail?.Invoke(order);

                if (order == null || order.Info == null || order.Info.PurchasedProductInfo == null || order.Info.PurchasedProductInfo.Count == 0)
                {
                    IAPDebug.Log("OnPurchaseFailed: Order or Order data is null");
                    return;
                }

                IAPDebug.Log(string.Format("OnPurchaseFailed: FAIL. Product: '{0}', PurchaseFailureReason: {1}", order.Info.PurchasedProductInfo[0].productId, order.FailureReason));
            }
            catch (Exception e)
            {
                IAPDebug.Error("OnPurchase Failed : Exception : " + e.Message);
            }
        }

        private void OnPurchasePending(PendingOrder order)
        {
            try
            {
                OnPurchaseOrderPending?.Invoke(order);

                if (order == null || order.Info == null || order.Info.PurchasedProductInfo == null || order.Info.PurchasedProductInfo.Count == 0)
                {
                    IAPDebug.Log("OnPurchasePending: Purchase is pending null order");
                    return;
                }
                IAPDebug.Log("OnPurchasePending: Purchase is pending for product: " + order.Info.Receipt);
            }
            catch (Exception e)
            {
                IAPDebug.Error("OnPurchase pending : Exception : " + e.Message);
            }
        }

        public void OnCheckEntitlementProduct(Entitlement entitlement)
        {
            try
            {
                OnCheckedEntitlement?.Invoke(entitlement);
                if (entitlement == null)
                {
                    return;
                }
                if (entitlement.Status != EntitlementStatus.EntitledUntilConsumed && entitlement.Status != EntitlementStatus.EntitledButNotFinished)
                {
                    IAPDebug.Log("OncheckEntitlement: entitlement is already consumed or entitled " + entitlement.Product.definition.storeSpecificId);
                    return;
                }
                PendingOrder pendingOrder = new PendingOrder(entitlement.Order.CartOrdered, entitlement.Order.Info);
                _storeController.ConfirmPurchase(pendingOrder);

                IAPDebug.Log("OnCheckEntitlementProduct: " + entitlement.Product.definition.id + " Status: " + entitlement.Status);
            }
            catch (Exception e)
            {
                IAPDebug.Error("OnCheckEntitlementProduct: Exception: " + e.Message);
            }


        }

        private void OnPurchaseDeffered(DeferredOrder order)
        {
            OnPurchaseProductDeffered?.Invoke(order);
        }

#endregion

        public List<PendingOrder> GetPendingOrders()
        {
            return _pedingOrders;
        }

        public void RemovePendingOrder(int index)
        {
            try
            {
                _pedingOrders.RemoveAt(index);
            }
            catch (Exception e)
            {
                IAPDebug.Log("RemovePendingOrder Exception " + e.Message);
            }
        }

        #region UTILITY_METHODS

        public string GetProductLocalizedPrice(string productId)
        {
            if (!IsInitialized())
            {
                IAPDebug.Log("GetProductLocalizedPrice: IAP not initialized");
                return null;
            }

            Product product = _storeController.GetProductById(productId);
            if (product != null)
            {
                return product.metadata.localizedPriceString;
            }
            else
            {
                IAPDebug.Log("GetProductLocalizedPrice: Product not found or not available for purchase");
                return null;
            }

        }

        public decimal GetProductLocalizedAmount(string productId)
        {
            if (!IsInitialized())
            {
                IAPDebug.Log("GetProductLocalizedPrice: IAP not initialized");
                return 0;
            }

            Product product = _storeController.GetProductById(productId);

            if (product != null)
            {
                return product.metadata.localizedPrice;
            }
            else
            {
                IAPDebug.Log("GetProductLocalizedPrice: Product " + productId + " not found or not available for purchase");
                return 0;
            }
        }

        public string GetProductLocalizedPriceWithSymbol(string productId)
        {
            if (!IsInitialized())
            {
                IAPDebug.Log("GetProductLocalizedPrice: IAP not initialized");
                return string.Empty;
            }

            Product product = _storeController.GetProductById(productId);

            if (product != null)
            {
                return product.metadata.localizedPriceString;
            }
            else
            {
                IAPDebug.Log("GetProductLocalizedPrice: Product " + productId + " not found or not available for purchase");
                return string.Empty;
            }
        }

        public string GetProductLocalizedISOCurrencyCode(string productId)
        {
            if (!IsInitialized())
            {
                IAPDebug.Log("GetProductLocalizedPrice: IAP not initialized");
                return string.Empty;
            }

            Product product = _storeController.GetProductById(productId);

            if (product != null)
            {
                return product.metadata.isoCurrencyCode;
            }
            else
            {
                IAPDebug.Log("GetProductLocalizedPrice: Product " + productId + " not found or not available for purchase");
                return string.Empty;
            }
        }

        public ReadOnlyObservableCollection<Order> GetAllPurchases()
        {
            ReadOnlyObservableCollection<Order> orders = _storeController.GetPurchases();
            return orders;
        }

        #endregion
    }
}
