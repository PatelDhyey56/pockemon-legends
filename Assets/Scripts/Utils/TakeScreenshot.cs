using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TakeScreenshot : MonoBehaviour
{
    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        if (Input.GetKeyUp(KeyCode.C))
        {
            ScreenCapture.CaptureScreenshot("D:/SS/Screenshot" + DateTime.Now.ToFileTime() + ".png");
        }
    }
}

