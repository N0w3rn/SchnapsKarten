using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

public class InitialiseGame : MonoBehaviour
{
    private const string MASTERVOLUME_KEY = "masterVolume";
    private const float DEFAULT_VOLUME = 0.5f;

    private bool isInitialisationComplete = false;

    void Start()
    {
        InitialiseSettings();
    }

    private void Update()
    {
        if (isInitialisationComplete)
        {
            Invoke("LoadScene", 1.2f);
        }
    }

    void InitialiseSettings()
    {
        InitialiseVolume();
        PlayerPrefs.Save();
        isInitialisationComplete = true;
    }

    void LoadScene()
    {
        NavigationSceneManager.instance.LoadScene("MainMenu");
    }

    void InitialiseVolume()
    {
        if (!PlayerPrefs.HasKey(MASTERVOLUME_KEY))
        {
            PlayerPrefs.SetFloat(MASTERVOLUME_KEY, DEFAULT_VOLUME);
        }

        AudioListener.volume = PlayerPrefs.GetFloat(MASTERVOLUME_KEY);
    }
}
