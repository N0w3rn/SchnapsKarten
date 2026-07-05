using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

// Team setup for the quiz mode. Works exactly like PlayerSetupManager, but the
// rows hold team names instead of player names, with team-name suggestions from
// team_names.json (assigned to the namesFile field in the QuizSetup scene).
public class QuizSetupManager : MonoBehaviour
{
    [Header("UI")]
    public Transform playerContainer;
    public List<GameObject> playerRowPrefabs = new List<GameObject>();
    public Button startGameButton;
    public Button backButton;
    public TextMeshProUGUI gameModeText;

    [Header("Name Suggestions")]
    public TextAsset teamNamesFile;

    private List<string> teamNames = new List<string>();
    private List<string> availableNames = new List<string>();
    private int currentPrefabIndex = 0;

    private const string TEAM_NAMES_KEY = "SavedTeamNames";

    void Start()
    {
        LoadSuggestionNames();
        LoadSavedTeams();

        if (gameModeText != null) gameModeText.text = "Spielmodus: Quiz";
        RefreshUI();

        startGameButton.onClick.AddListener(StartGame);
        backButton.onClick.AddListener(BackToMenu);
    }

    void LoadSavedTeams()
    {
        if (PlayerPrefs.HasKey(TEAM_NAMES_KEY))
        {
            try
            {
                PlayerNamesJSON savedData = JsonUtility.FromJson<PlayerNamesJSON>(PlayerPrefs.GetString(TEAM_NAMES_KEY));
                if (savedData != null && savedData.names != null)
                {
                    teamNames = new List<string>(savedData.names);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Fehler beim Laden gespeicherter Teams: {e.Message}");
                teamNames.Clear();
            }
        }
    }

    void SaveTeams()
    {
        try
        {
            PlayerNamesJSON saveData = new PlayerNamesJSON();
            saveData.names = new List<string>(teamNames);
            PlayerPrefs.SetString(TEAM_NAMES_KEY, JsonUtility.ToJson(saveData));
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"Fehler beim Speichern der Teams: {e.Message}");
        }
    }

    void LoadSuggestionNames()
    {
        if (teamNamesFile == null)
        {
            Debug.LogWarning("Keine Teamnamen-JSON-Datei zugewiesen! Verwende Standard-Namen.");
            availableNames = new List<string> { "Team Prost", "Die Durstlöscher", "Quizottel", "Schluckspechte" };
            return;
        }

        try
        {
            PlayerNamesJSON namesData = JsonUtility.FromJson<PlayerNamesJSON>(teamNamesFile.text);
            availableNames = new List<string>(namesData.names);

            if (availableNames.Count == 0)
            {
                Debug.LogWarning("Teamnamen-Liste ist leer! Verwende Standard-Namen.");
                availableNames = new List<string> { "Team Prost", "Die Durstlöscher", "Quizottel", "Schluckspechte" };
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Fehler beim Laden der Teamnamen-JSON: {e.Message}");
            availableNames = new List<string> { "Team Prost", "Die Durstlöscher", "Quizottel", "Schluckspechte" };
        }
    }

    string GetRandomName()
    {
        if (availableNames.Count == 0) return "Team";

        List<string> unusedNames = new List<string>();
        foreach (string name in availableNames)
        {
            if (!teamNames.Contains(name))
            {
                unusedNames.Add(name);
            }
        }

        if (unusedNames.Count == 0)
        {
            return availableNames[UnityEngine.Random.Range(0, availableNames.Count)] + " " + (teamNames.Count + 1);
        }

        return unusedNames[UnityEngine.Random.Range(0, unusedNames.Count)];
    }

    GameObject GetNextPlayerRowPrefab()
    {
        if (playerRowPrefabs.Count == 0)
        {
            Debug.LogError("Keine PlayerRow-Prefabs zugewiesen!");
            return null;
        }

        GameObject prefab = playerRowPrefabs[currentPrefabIndex];
        currentPrefabIndex = (currentPrefabIndex + 1) % playerRowPrefabs.Count;
        return prefab;
    }

    void RefreshUI()
    {
        foreach (Transform child in playerContainer)
        {
            Destroy(child.gameObject);
        }

        currentPrefabIndex = 0;

        for (int i = 0; i < teamNames.Count; i++)
        {
            CreateTeamRow(i, teamNames[i], false);
        }

        CreateTeamRow(teamNames.Count, GetRandomName(), true);

        UpdateStartButton();
    }

    void CreateTeamRow(int index, string teamName, bool isEmpty)
    {
        GameObject prefab = GetNextPlayerRowPrefab();
        if (prefab == null) return;

        GameObject newRow = Instantiate(prefab, playerContainer);

        TMP_InputField input = newRow.GetComponentInChildren<TMP_InputField>();
        if (input == null)
        {
            Debug.LogError($"TMP_InputField nicht gefunden in Prefab: {prefab.name}");
            return;
        }

        Button plusButton = null;
        Button minusButton = null;

        Transform plusTransform = newRow.transform.Find("PlusButton");
        if (plusTransform == null) plusTransform = newRow.transform.Find("AddButton");
        if (plusTransform == null) plusTransform = newRow.transform.Find("Plus");
        if (plusTransform == null) plusTransform = newRow.transform.Find("+");

        Transform minusTransform = newRow.transform.Find("MinusButton");
        if (minusTransform == null) minusTransform = newRow.transform.Find("RemoveButton");
        if (minusTransform == null) minusTransform = newRow.transform.Find("Minus");
        if (minusTransform == null) minusTransform = newRow.transform.Find("-");

        if (plusTransform == null || minusTransform == null)
        {
            Button[] allButtons = newRow.GetComponentsInChildren<Button>();
            if (allButtons.Length >= 2)
            {
                plusButton = allButtons[0];
                minusButton = allButtons[1];
            }
            else
            {
                Debug.LogError($"Nicht genügend Buttons gefunden in Prefab: {prefab.name}");
                return;
            }
        }
        else
        {
            plusButton = plusTransform.GetComponent<Button>();
            minusButton = minusTransform.GetComponent<Button>();
        }

        if (plusButton == null || minusButton == null)
        {
            Debug.LogError($"Button-Komponenten nicht gefunden in Prefab: {prefab.name}");
            return;
        }

        input.text = teamName;
        input.interactable = isEmpty;

        if (isEmpty)
        {
            plusButton.gameObject.SetActive(true);
            minusButton.gameObject.SetActive(false);

            if (input.placeholder != null && input.placeholder.GetComponent<TextMeshProUGUI>() != null)
            {
                input.placeholder.GetComponent<TextMeshProUGUI>().text = "Teamname eingeben...";
            }

            plusButton.onClick.AddListener(() =>
            {
                string name = input.text.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    teamNames.Add(name);
                    SaveTeams();
                    RefreshUI();
                }
            });

            input.onEndEdit.AddListener((value) =>
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    string name = value.Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        teamNames.Add(name);
                        SaveTeams();
                        RefreshUI();
                    }
                }
            });
        }
        else
        {
            plusButton.gameObject.SetActive(false);
            minusButton.gameObject.SetActive(true);

            minusButton.onClick.AddListener(() =>
            {
                teamNames.RemoveAt(index);
                SaveTeams();
                RefreshUI();
            });
        }
    }

    void UpdateStartButton()
    {
        startGameButton.interactable = teamNames.Count >= 2;
        startGameButton.GetComponentInChildren<TextMeshProUGUI>().text =
            teamNames.Count >= 2 ? $"Quiz starten ({teamNames.Count} Teams)" : "Mindestens 2 Teams";
    }

    public void StartGame()
    {
        if (teamNames.Count < 2) return;

        List<Team> teams = new List<Team>();
        foreach (string name in teamNames)
        {
            teams.Add(new Team(name));
        }

        QuizSession.teams = teams;
        NavigationSceneManager.instance.LoadScene("QuizScene");
    }

    public void BackToMenu()
    {
        NavigationSceneManager.instance.LoadScene("MainMenu");
    }
}
