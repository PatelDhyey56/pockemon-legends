using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AdsManager.ScriptableObjects
{

    [CreateAssetMenu(fileName = "AdData", menuName = "Create AdData")]
    public class AdData : ScriptableObject
    {
        public bool isTestMode;
        [Space(2)]
        [Header("AdMob ID Android")]
        [SerializeField] private string androidRewardAdID;
        [SerializeField] private string androidBannerAdID;
        [SerializeField] private string androidInterstitialAdID;
        [SerializeField] private string androidInterstitialSplashAdID;
        [SerializeField] private string androidRewardInterstitialAdID;
        [SerializeField] private string androidOpneAdID;
        [SerializeField] private string androidRectBannerAdID;
        [SerializeField] private List<string> validAndroidStoreList;
        [SerializeField] private List<string> testDeviceHashedIds;

        [Space(2)]
        [Header("AdMob ID iOS")]
        [SerializeField] private string iOSRewardAdID;
        [SerializeField] private string iOSBannerAdID;
        [SerializeField] private string iOSInterstitialAdID;
        [SerializeField] private string iOSInterstitialSplashAdID;
        [SerializeField] private string iOSRewardInterstitialAdID;
        [SerializeField] private string iOSOpneAdID;
        [SerializeField] private string iOSRectBannerAdID;

        private string _testRewardAdID = "ca-app-pub-3940256099942544/5224354917";
        private string _testBannerAdID = "ca-app-pub-3940256099942544/6300978111";
        private string _testInterstitialAdID = "ca-app-pub-3940256099942544/1033173712";
        private string _testInterstitialSplashAdID = "ca-app-pub-3940256099942544/1033173712";
        private string _testOpenAdID = "ca-app-pub-3940256099942544/9257395921";
        private string _testRewardInterstitialAdID = "ca-app-pub-3940256099942544/5354046379";

        private const string SettingsFileName = "AdData";
        private const string SettingsFileExtension = ".asset";
        private const string ResDir = "Assets/Resources";

        public static AdData GetInstance()
        {
            //Read from resources.
            var instance = Resources.Load<AdData>(SettingsFileName);
            //Create instance if null.
            if (instance == null)
            {
                Directory.CreateDirectory(ResDir);
                instance = CreateInstance<AdData>();
                string assetPath = Path.Combine(ResDir, SettingsFileName + SettingsFileExtension);
#if UNITY_EDITOR
                AssetDatabase.CreateAsset(instance, assetPath);
                AssetDatabase.SaveAssets();
#endif
            }
            return instance;
        }

        /// <summary>
        /// return BannerAdID based on current mobile platform.
        /// </summary>
        /// <returns></returns>
        public string GetBannerAdID()
        {
            if (isTestMode)
            {
                return _testBannerAdID;
            }
            else
            {
#if UNITY_ANDROID
                return androidBannerAdID;
#elif UNITY_IPHONE
            return iOSBannerAdID;
#else
            return _testBannerAdID;
#endif
            }
        }

        /// <summary>
        /// return RewardAdID based on current mobile platform.
        /// </summary>
        /// <returns></returns>
        public string GetRewardAdID()
        {
            if (isTestMode)
            {
                return _testRewardAdID;
            }
            else
            {
#if UNITY_ANDROID
                return androidRewardAdID;
#elif UNITY_IPHONE
            return iOSRewardAdID;
#else
            return _testRewardAdID;
#endif
            }
        }

        /// <summary>
        /// return InterstitialAdID based on current mobile platform.
        /// </summary>
        /// <returns></returns>
        public string GetInterstitialAdID()
        {
            if (isTestMode)
            {
                return _testInterstitialAdID;
            }
            else
            {
#if UNITY_ANDROID
                return androidInterstitialAdID;
#elif UNITY_IPHONE
            return iOSInterstitialAdID;
#else
            return androidInterstitialAdID;
#endif
            }
        }

        public string GetInterstitialSplashAdID()
        {
            if (isTestMode)
            {
                return _testInterstitialSplashAdID;
            }
            else
            {
#if UNITY_ANDROID
                return androidInterstitialSplashAdID;
#elif UNITY_IPHONE
            return iOSInterstitialSplashAdID;
#else
            return _testInterstitialSplashAdID;
#endif
            }
        }

        /// <summary>
        /// return RewardInterstitialAdID based on current mobile platform.
        /// </summary>
        /// <returns></returns>
        public string GetRewardedInterstitialAdID()
        {
            if (isTestMode)
            {
                return _testRewardInterstitialAdID;
            }
            else
            {
#if UNITY_ANDROID
                return androidRewardInterstitialAdID;
#elif UNITY_IPHONE
            return iOSRewardInterstitialAdID;
#else
            return _testRewardInterstitialAdID;
#endif
            }
        }

        public string GetOpenAdID()
        {
            if (isTestMode)
            {
                return _testOpenAdID;
            }
            else
            {
#if UNITY_ANDROID
                return androidOpneAdID;
#elif UNITY_IPHONE
            return iOSOpneAdID;
#else
            return androidOpneAdID;
#endif
            }
        }

        /// <summary>
        /// return BannerAdID based on current mobile platform.
        /// </summary>
        /// <returns></returns>
        public string GetRectBannerAdID()
        {
            if (isTestMode)
            {
                return _testBannerAdID;
            }
            else
            {
#if UNITY_ANDROID
                return androidRectBannerAdID;
#elif UNITY_IPHONE
            return iOSRectBannerAdID;
#else
            return _testBannerAdID;
#endif
            }
        }


        public bool IsFromValidStoreInstallation()
        {
            if (isTestMode)
            {
                return true;
            }
            else
            {
#if UNITY_ANDROID
                return ValidateAndroidStore();
#elif UNITY_IOS
            return true;
#else
            return true;
#endif
                }
        }

        public bool ValidateAndroidStore()
        {
            var storeName = Application.installerName;

            Debug.Log("Application.installerName " + Application.installerName);

            if (validAndroidStoreList.Contains(storeName))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public List<string> GetTestDeviceHashIds()
        {
            if (isTestMode)
            {
                return testDeviceHashedIds;
            }

            return new List<string>();
        }
    }
}
