using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
public class ShakeEffect : MonoBehaviour {

    public float delay;
    public float offset = 0.5f;
	// Use this for initialization
	void Start ()
    {
        gameObject.transform.DOScale(offset,delay).From()
               .SetLoops(-1, LoopType.Yoyo)
               .SetEase(Ease.Linear);
    }
	
	
}
