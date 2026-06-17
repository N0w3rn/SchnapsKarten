// OrientationManager.cs - Verwaltet Screen Orientation
using UnityEngine;

public class OrientationManager : MonoBehaviour
{
    public static OrientationManager instance;
    
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
    
    private void Start()
    {
        // Standard: Portrait für Menüs
        SetPortrait();
        
        // Event für Scene-Wechsel abonnieren
        if (NavigationSceneManager.instance != null)
        {
            NavigationSceneManager.instance.onSceneChange += OnSceneChanged;
        }
    }
    
    private void OnDisable()
    {
        if (NavigationSceneManager.instance != null)
        {
            NavigationSceneManager.instance.onSceneChange -= OnSceneChanged;
        }
    }
    
    private void OnSceneChanged(string sceneName)
    {
        // Landscape nur für GameScene
        if (sceneName == "GameScene")
        {
            SetLandscape();
        }
        else
        {
            SetPortrait();
        }
    }
    
    public void SetLandscape()
    {
        Screen.orientation = ScreenOrientation.LandscapeLeft;
    }
    
    public void SetPortrait()
    {
        Screen.orientation = ScreenOrientation.Portrait;
    }
    
    public void SetAutoRotation()
    {
        Screen.orientation = ScreenOrientation.AutoRotation;
    }
}