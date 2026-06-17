using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AspectRatio : MonoBehaviour
{
    [Range(0, 1)]
    public float matchWidthOrHeight = 0.5f;
    public Vector2 refrenceResolution = new Vector2(1080, 1920); 
    public static float ScaleFactor = 1;

    private void Start()
    {
        CalculateScaleFactor();
    }

    private void CalculateScaleFactor()
    {
        float widthComponent = ((1.0f - matchWidthOrHeight) * (Screen.width / refrenceResolution.x)); 
        float heightComponent = (matchWidthOrHeight * (Screen.height / refrenceResolution.y));
        ScaleFactor = heightComponent + widthComponent;
    }

   
}
