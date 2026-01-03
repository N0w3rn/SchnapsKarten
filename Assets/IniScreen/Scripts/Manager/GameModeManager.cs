using UnityEngine;
using UnityEngine.SceneManagement;
using System;

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager instance;

    public GameModeEnum gameMode;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void setGameMode(GameModeEnum setGameMode){
        gameMode = setGameMode;
    }

    public GameModeEnum getGameMode(){
        return gameMode;
    }
}
