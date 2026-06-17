# purchase-manager-unity-package 

## About
This Package is built for ease of use for developers who want to integrate **Unity In-App Purchase** for Unity. There are some built-in methods that developers can use to reduce the time to integrate the IAP in their Unity projects to generate revenue. This package includes In-app purchasing (v 5.0.1) and some custom-made scripts. For More info about IAP v5 package and Documantation visit: https://docs.unity.com/ugs/en-us/manual/iap/manual/overview

## Installation

* Open Unity's __Package Manager__
* Go To __Install from Git URL__
* Paste the given URL in the Input Box: https://gitlab.com/hitesh.gamepad/purchase-manager.git
* Click Install and wait till all the Installation processes are complete.
* This Package already includes Unity in-app purchasing v5.0.1, so no need to import it separately.

## SetUp
* Open Purchase-Manager Folder in package and in prefab folder you'll find PurchaseManager prefab. 
* Drag and drop it in the Splash scene.
* In The `assets/resources`, Do right click and `Create/Create InAppData`
* Add all the products and their data in the scriptableObject. After that assign the scriptableObject in the prefab.

## Samples
#### Buy Product
* For buying any product call the function and pass the productId for buying the product
```PurchaseManager.GetInstance().BuyProduct(productId);```
* For confirming the purchasing use OnPurchaseProduct Callback. This Callback give ProductData List 
``` PurchaseManager.OnPurchaseProduct ```
* For getting failed purchase info, use the OnPurchaseFailed callback. This Callback gives ProductData List 
```PurchaseManager.OnPurchaseFail;```

#### Get Entitlement
* When purchasing any non-consumable product or subscription product, use this method. It returns a flag that we can use to check if we get the Entitlement of that product
``` PurchaseManager.GetInstance().IsProductEntitled(productId)```

#### Restore Transection
* For restoring non-consumable and subscription Call RestorePurchases method
``` PurchaseManager.GetInstance().RestorePurchases()```
* For getting a Callback of successful and Failed restoration use these callbacks
``` PurchaseManager.OnRestoreSuccess ```

``` PurchaseManager.OnRestoreFailed ``` 

## Extras
* For getting the localized prize call GetProductLocalizedPrice() method
``` PurchaseManager.GetInstance().GetProductLocalizedPrice(productId) ``` 