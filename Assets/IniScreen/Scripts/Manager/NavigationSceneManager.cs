using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections;

public class NavigationSceneManager : MonoBehaviour
{
    public static NavigationSceneManager instance;
    public event Action<string> onSceneChange;

    private string previousScene;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            previousScene = SceneManager.GetActiveScene().name;
            SceneManager.sceneUnloaded += setPreviousScene;
            SceneManager.sceneLoaded += triggerSceneChangedAction;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneUnloaded -= setPreviousScene;
        SceneManager.sceneLoaded -= triggerSceneChangedAction;
    }

    private void setPreviousScene(Scene scene)
    {
        previousScene = scene.name;
    }

    public string GetPreviousScene()
    {
        return previousScene;
    }

    public string GetCurrentScene()
    {
        return SceneManager.GetActiveScene().name;
    }

    public void LoadScene(string sceneName)
    {
        previousScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(sceneName);
    }

    private void triggerSceneChangedAction(Scene scene, LoadSceneMode mode)
    {
        onSceneChange?.Invoke(scene.name);
        
        // Orientierung nach Scene-Load setzen (verhindert Flackern)
        StartCoroutine(SetOrientationAfterFrame(scene.name));
    }
    
    private IEnumerator SetOrientationAfterFrame(string sceneName)
    {
        // Warte einen Frame, damit die Scene komplett geladen ist
        yield return null;
        
        // Setze Orientierung basierend auf Scene
        if (sceneName.ToLower().Contains("game"))
        {
            Screen.orientation = ScreenOrientation.LandscapeLeft;
        }
        else
        {
            Screen.orientation = ScreenOrientation.Portrait;
        }
    }
}