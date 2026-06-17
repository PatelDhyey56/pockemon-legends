using UnityEngine;
using AdsManager;
using UnityEngine.Rendering;
public class AdsCreator : MonoBehaviour
{

    public void LoadBannerAd()
    {
        AdMobManager.GetInstance().RequestBanner(BannerAdPosition.Bottom);
    }

    public void ShowBanner()
    {
        AdMobManager.GetInstance().ShowBanner();
    }

    public void RemoveBannerAd()
    {
        AdMobManager.GetInstance().DestroyBanner();
    }

    public void LoadIntestitialAd()
    {
        AdMobManager.GetInstance().RequestInterstitial();
    }

    public void ShowInterStitialAd()
    {
        AdMobManager.GetInstance().ShowInterstitial();
    }

    public void LoadrewardedAd()
    {
        AdMobManager.GetInstance().RequestRewardedAd(AdStatusCallBack);
    }

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

}
