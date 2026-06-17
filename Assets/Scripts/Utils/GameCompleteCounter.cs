using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utils;

public class GameCompleteCounter : MonoBehaviour
{
    private static int TOTAL_GAME = 3;
    private int _gameCount = 0;
    private int _prevCount = 0;
    private int _currentCount = 1;
    private int _totalCount = 0;

    //call on gamecomplete event of game
    private void OnGameComplete()
    {
        _gameCount++;

        _totalCount = _prevCount + _currentCount;

        _prevCount = _currentCount;
        _currentCount = _totalCount;

        if(_totalCount >= TOTAL_GAME)
        {
            if (_gameCount == _totalCount && !PreferenceHelper.IsUserRateThisApp())
            {
                RateAppPopUpView.GetInstance().Show();
            }
        }

    }

}
