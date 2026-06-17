# ads-manager-unity-package 

## About
This Package is built for ease of use for developers who want to integrate **Google Mobile Ads** for Unity. There are some built-in methods that developers can use to reduce the time to integrate the AdMob ads in their Unity projects to generate revenue through Google Ads. This package includes Google AdMob (v 10.4.1) and some custom-made scripts. For More info about AdMob package and Documantation visit: https://developers.google.com/admob/unity/quick-start

## Installation
* Add the given code to the manifest file of Packages, below dependecies block.
```
"scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.google"
      ],
      "overrideBuiltIns": false
    }
  ]
```
* Open Unity's __Package Manager__
* Go To __Install from Git URL__
* Paste the given URL in the Input Box: https://gitlab.com/hitesh.gamepad/ads-manager.git
* Click Install and wait till all the Installation processes are complete.

## SetUp
* Open ads-manager Folder in package and in prefab folder you'll find AdMobManager prefab. 
* Drag and drop it in the Splash scene.
* In The `assets/resources`, Do right click and `Create/CreateAdData`
* Add all the Ad Ids in the scriptableObject. After that assign the scriptableObject in the prefab.
* RightClick on the asset folder, `select Google Mobile ads/settings...`
* In that scriptable Object, Add Android and iOs App Id
* Also in the User Tracking usage Decription Add: `This identifier will be used to deliver personalized ads to you.`
* In the AdMobManager prefab you'll need to set two enum values in the Inspecter Panel

* TagForChildDirectedTreatment : If your target player is children, then you'll need to mark the flag true else false.


* maxContentRating : You'll need to set this according to your target players

G → General (all ages, kids safe)
PG → Parental Guidance (mild content)
T → Teen (moderate content, 13+)
MA → Mature (adult content, 18+)

## Samples
#### Banner Ad
* For Requesting banner ad, Call this method and in the perams pass the desired values
```
AdMobManager.GetInstance().RequestBanner(BannerAdPosition.Bottom, BannerAdSize.FullWidth, AdStatusDelegate);
```
* After loading the Banner sucessfully, for showing the banner call 
```
AdMobManager.GetInstance().ShowBanner();
```
* For removing the banner call 
```
AdMobManager.GetInstance().DestroyBanner();
```

#### Interstitial Ad
* For Requesting Interstitial ad, Call this method and in the perams pass the desired values
```
AdMobManager.GetInstance().RequestInterstitial();
```
* If you want to add params you can call the function with 
```
AdMobManager.GetInstance().RequestInterstitial(adId,AdStatusDelegate);
``` 
* When you need to show the Interstitial call 
```
AdMobManager.GetInstance().ShowInterstitial();
```

#### Rewarded Ad
* For Requesting Rewarded ad, Call
```
AdMobManager.GetInstance().RequestRewardedAd(AdStatusCallback);
```
* Handle AdStatusCallback
 ```
private void AdStatusCallBack(AdStatusCode adStatusCode)
 {
     switch (adStatusCode)
     {
         case AdStatusCode.NoInternet: //This is the case when there is no internet availaible
             //TODO: Show error message
             break;

         case AdStatusCode.SDKInitFailed://This is the case when there is sdk init failed
         case AdStatusCode.ADLoadFailed: //This is the case when there is ad is not loaded
             //TODO: Show error message
             break;

         case AdStatusCode.ADLoadSuccess: //This is the case when there is ad  loaded
             AdMobManager.GetInstance().ShowRewardAd();
             break;

         case AdStatusCode.RewardGranted: //This is the case when reward ad watched successfully and  
            //TODO: Write code here to give reward
             break;
     }
 }
```
* When you need to show the Rewarded call 
```
AdMobManager.GetInstance().ShowRewardAd();
```

#### RewardedInterstitial Ad
* For Requesting RewardedInterstitial ad, Call this method 
```
AdMobManager.GetInstance().RequestRewardInterstitial(AdStatusDelegate);
```
* When you need to show the RewardedInterstitial call 
```
AdMobManager.GetInstance().ShowRewardInterstitial();
```

#### App-Open Ad
* When app comes in foreground after being on background this ad will shows up automatically.
* For Requesting App Open Ad, Call this method 
```
AdMobManager.GetInstance().LoadAppOpenAd();
```

#### Ract-Banner Ad
* This Banner Ad's size is bigger than the normal Banner Ad.
* For Requesting Rect-banner ad, Call this method and in the perams pass the desired values
```
AdMobManager.GetInstance().RequestRectBanner(BannerAdPosition.Bottom, BannerAdSize.MediumRectangle, AdStatusDelegate);
```
* After loading the Rect-Banner sucessfully, for showing the banner call 
```
AdMobManager.GetInstance().ShowRectBanner();
```
* For removing the Rect-banner call 
```
AdMobManager.GetInstance().DestroyRectBanner();
```
