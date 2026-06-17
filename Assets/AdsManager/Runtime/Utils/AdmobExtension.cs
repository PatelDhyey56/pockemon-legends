using UnityEngine;

internal static class AdmobExtension
{
    public static bool IsNullOrEmpty(this string value)
    {
        return value == null || value == "" || value.Trim() == "";
    }
}
