using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Constants
{
    //add alll scenes here
    #region SCENES

    public const string SCENE_MENU = "MenuScene";
    public const string SCENE_WEB = "webscene";

    #endregion

    //add string values here
    #region STRINGS

    public const string PRIVACY_URL = "http://sharpenminds.in:8081/consent/policy/GamePad";
    public const string PRIVACY_TITTLE = "Privacy Policy";
    public const string LICENCE_TITTLE = "Licence & Credits";
    public const string ATT_DESCRIPTION = "This identifier will be used to deliver personalized ads to you.";
    public const string RESTORE_PURCHASE_WARNING = "Restore Failed.. Seems like you have not previously purchased the plan.";
    public const string RESTORE_PURCHASE_SUCCESS = "Restore completed";
    public const string GAME_SHARE_TEXT = "Download UnityBasedProject game!\n ";

    #endregion

    //add analytics events name here
    #region ANALYTICS

    public const string EVENT_GAME_LAUNCH ="game_launch";

    #endregion

    //add integer constant here

    //warning messages
    public const string WARN_SOMETHING_WRONG = "Something went wrong,Please try again later.";
    public const string WARN_NO_INTERNET = "Internet connection required,Please try again.";

}
