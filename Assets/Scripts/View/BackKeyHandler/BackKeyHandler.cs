using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BackKeyHandler : MonoBehaviour
{
    private static BackKeyHandler _instance;
    private Stack<View> _viewStack;

    private void Awake()
    {
        _instance = this;
        _viewStack = new Stack<View>();
    }

    public static BackKeyHandler GetInstance()
    {
        return _instance;
    }

    public void PushView(View view)
    {
        _viewStack.Push(view);
    }

    public void PopView()
    {
        if (_viewStack.Count != 0)
        {
            View hideView = _viewStack.Peek();
            hideView.OnViewClosed();
            _viewStack.Pop();
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OnBackKeyPressedEvent();
        }
    }

    private void OnBackKeyPressedEvent()
    {
        View hideView = _viewStack.Peek();
        hideView.OnBackeyPressed();
    }

    public int GetStackCount()
    {
        return _viewStack.Count;
    }
}
