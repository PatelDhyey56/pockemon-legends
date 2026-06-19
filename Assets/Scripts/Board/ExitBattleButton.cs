using UnityEngine;
using UnityEngine.SceneManagement;

public class ExitBattleButton : MonoBehaviour
{
    public void GoToHomeScene()
    {
        // Return to the home scene (MenuScene)
        SceneManager.LoadScene(Constants.SCENE_MENU);
    }
}
