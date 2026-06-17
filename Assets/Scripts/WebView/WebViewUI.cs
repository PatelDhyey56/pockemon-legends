using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utils;

public class WebViewUI : MonoBehaviour
{
    private void Start()
    {
        PreferenceHelper.SetIsWebViewOpen(true);
    }

    private void Update()
    {
        OnBackKeyPress();
    }

    public void OnBackKeyPress()
    {
        if (Input.GetKey(KeyCode.Escape))
        {
            OnBackButtonClick();
        }
    }
    public void OnBackButtonClick()
    {
        SceneManager.LoadScene(Constants.SCENE_MENU);
    }
    
}
