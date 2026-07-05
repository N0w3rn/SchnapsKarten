using UnityEngine;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [Header("Menu Buttons")]
    public Button flaschenDrehenButton;
    public Button classicButton;
    public Button quizButton;
    
    void Start()
    {
        SetupButtons();
    }
    
    void SetupButtons()
    {
        classicButton.onClick.AddListener(() => StartGameMode(GameModeEnum.Classic));
        
        if (quizButton != null)
            quizButton.onClick.AddListener(() => StartGameMode(GameModeEnum.Quiz));
        if (flaschenDrehenButton != null)
            flaschenDrehenButton.onClick.AddListener(() => StartGameMode(GameModeEnum.Flaschendrehen));
    }
    
    void StartGameMode(GameModeEnum gameMode)
    {
        GameModeManager.instance.setGameMode(gameMode);

        // Quiz has its own team setup inside its scene; the other modes share PlayerSetup.
        if (gameMode == GameModeEnum.Quiz)
        {
            NavigationSceneManager.instance.LoadScene("QuizScene");
        }
        else
        {
            NavigationSceneManager.instance.LoadScene("PlayerSetup");
        }
    }
}