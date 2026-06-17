using System;
using UnityEngine;
/// <summary>
/// Handles the PlayerPrefes related methods....
/// </summary>
public static class GamePlayerPrefs
{

    /// <summary>
    /// Return true if PlayerPrefs contain that key otherwise return false..
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public static bool HasKey(string key)
    {
        return PlayerPrefs.HasKey(key);
    }


    /// <summary>
    /// Delete the PlayerPrefrence based on key...
    /// </summary>
    /// <param name="key"></param>
    public static void DeleteKey(string key)
    {
        PlayerPrefs.DeleteKey(key);
    }

    /// <summary>
    /// Delete all the PlayerPrefrence...use only when reset PlayerPrefs Data...
    /// </summary>
    /// <param name="key"></param>
    public static void DeleteAll()
    {
        PlayerPrefs.DeleteAll();
    }


    /// <summary>
    /// Return the integer PlayerPrfes based on Key...else defaultValue
    /// </summary>
    /// <param name="key"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static int GetInt(string key, int defaultValue = 0)
    {
        return (!GamePlayerPrefs.HasKey(key)) ? defaultValue : PlayerPrefs.GetInt(key);
    }

    /// <summary>
    /// Set the integer PlayerPrfes based on Key...else defaultValue
    /// </summary>
    /// <param name="key"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static void SetInt(string key, int value)
    {
        PlayerPrefs.SetInt(key, value);
    }


    /// <summary>
    /// Return the Float PlayerPrfes based on Key...else defaultValue
    /// </summary>
    /// <param name="key"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static float GetFloat(string key, float defaultValue = 0f)
    {
        return (!GamePlayerPrefs.HasKey(key)) ? defaultValue : PlayerPrefs.GetFloat(key);
    }

    /// <summary>
    /// Set the Float PlayerPrfes based on Key...else defaultValue
    /// </summary>
    /// <param name="key"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static void SetFloat(string key, float value)
    {
        PlayerPrefs.SetFloat(key, value);
    }


    /// <summary>
    /// Return the String PlayerPrfes based on Key...else defaultValue
    /// </summary>
    /// <param name="key"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static string GetString(string key, string defaultValue = null)
    {
        return (!GamePlayerPrefs.HasKey(key)) ? defaultValue : PlayerPrefs.GetString(key);

    }

    /// <summary>
    /// Set the String PlayerPrfes based on Key...else defaultValue
    /// </summary>
    /// <param name="key"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static void SetString(string key, string value)
    {
        PlayerPrefs.SetString(key, value);
    }

    /// <summary>
    /// Return the Bool PlayerPrfes based on Key...else defaultValue
    /// </summary>
    /// <param name="key"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static bool GetBool(string key, bool defaultValue = false)
    {
        return (!GamePlayerPrefs.HasKey(key)) ? defaultValue : (1 == PlayerPrefs.GetInt(key));
    }


    /// <summary>
    /// Set the Bool PlayerPrfes based on Key...else defaultValue
    /// </summary>
    /// <param name="key"></param>
    /// <param name="defaultValue"></param>
    /// <returns></returns>
    public static void SetBool(string key, bool value)
    {
        PlayerPrefs.SetInt(key, (!value) ? 0 : 1);
    }


    /// <summary>
    /// Save the PlayerPrefes during exception arise..
    /// </summary>
    public static void Save()
    {
        PlayerPrefs.Save();
    }
}