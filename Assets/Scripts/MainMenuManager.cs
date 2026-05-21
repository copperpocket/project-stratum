using UnityEngine;
using UnityEngine.SceneManagement; // <-- REQUIRED FOR SCENE MANAGEMENT

public class MainMenuManager : MonoBehaviour
{
    public void StartDuel()
    {
        Debug.Log("Loading Battlefield...");
        
        // This grabs the scene named "DuelStage" out of your Build Settings and loads it
        SceneManager.LoadScene("DuelStage");
    }

    public void QuitGame()
    {
        Debug.Log("Exiting Game Application...");
        Application.Quit();
    }
}