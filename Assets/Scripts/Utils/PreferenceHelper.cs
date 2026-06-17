using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
namespace Utils
{
    public class PreferenceHelper
    {
        private static bool _isWebViewOpen = false;
        //add all preference key here with PREF keyword for exaple PREF_MY_CUSTOM KEY
        private const string PREF_AD_REMOVED = "wpk1u25xwt";
        private const string PREF_WEB_TITTLE = "wt";
        private static bool _isPremiumUser = false;
        private static bool _isUserRateThisApp = false;

        public static bool IsAdRemoved()
        {
            return GamePlayerPrefs.GetBool(PREF_AD_REMOVED, false);
        }

        public static void RemoveAd()
        {
            GamePlayerPrefs.SetBool(PREF_AD_REMOVED, true);
        }

        public static void SetWebViewTittle(string data)
        {
            GamePlayerPrefs.SetString(PREF_WEB_TITTLE,data);
        }

        public static string GetWebViewTittle()
        {
            return GamePlayerPrefs.GetString(PREF_WEB_TITTLE);
        }

        public static void SetIsWebViewOpen(bool value)
        {
            _isWebViewOpen = value;
        }

        public static bool GetIsWebViewOpen()
        {
            return _isWebViewOpen;
        }

        public static void SetRateThisAppState(bool state)
        {
            _isUserRateThisApp = state;
        }

        public static bool IsUserRateThisApp()
        {
            return _isUserRateThisApp;
        }

        // use this method for in app purchase subscription
        public static bool IsPremiumUser()
        {
            return _isPremiumUser;
        }

        public static void SetPremiumUser(bool isPremium)
        {
            _isPremiumUser = isPremium;
        }
    }
}