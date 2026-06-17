using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
public class RotateAnimation : MonoBehaviour {

    public LoopType LoopType;
    public float delay = 3f;

	// Use this for initialization
	void Start ()
    {
        gameObject.transform.DOLocalRotate(new Vector3(0, 0, -360), delay, RotateMode.FastBeyond360)
               .SetLoops(-1, LoopType)
               .SetEase(Ease.Linear);
    }

}
