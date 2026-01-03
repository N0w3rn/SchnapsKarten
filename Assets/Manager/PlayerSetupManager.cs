// PlayerSetupManager.cs - Mit Prefab-Liste, Namen-Vorschlägen und persistenten Spielern
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

[System.Serializable]
public class PlayerNamesJSON
{
    public List<string> names = new List<string>();
}

public class PlayerSetupManager : MonoBehaviour
{
    [Header("UI")]
    public Transform playerContainer;
    public List<GameObject> playerRowPrefabs = new List<GameObject>(); // Mehrere Prefabs
    public Button startGameButton;
    public Button backButton;
    public TextMeshProUGUI gameModeText;
    
    [Header("Namen-Vorschläge")]
    public TextAsset playerNamesFile; // JSON-Datei mit Namen
    
    private List<string> playerNames = new List<string>();
    private List<string> availableNames = new List<string>();
    private int currentPrefabIndex = 0;
    
    private const string PLAYER_NAMES_KEY = "SavedPlayerNames";
    
    void Start()
    {
        LoadPlayerNames();
        LoadSavedPlayers(); // Gespeicherte Spieler laden
        UpdateGameModeDisplay();
        RefreshUI();
        
        startGameButton.onClick.AddListener(StartGame);
        backButton.onClick.AddListener(BackToMenu);
    }
    
    void LoadSavedPlayers()
    {
        if (PlayerPrefs.HasKey(PLAYER_NAMES_KEY))
        {
            string savedNamesJson = PlayerPrefs.GetString(PLAYER_NAMES_KEY);
            try
            {
                PlayerNamesJSON savedData = JsonUtility.FromJson<PlayerNamesJSON>(savedNamesJson);
                if (savedData != null && savedData.names != null)
                {
                    playerNames = new List<string>(savedData.names);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Fehler beim Laden gespeicherter Spieler: {e.Message}");
                playerNames.Clear();
            }
        }
    }
    
    void SavePlayers()
    {
        try
        {
            PlayerNamesJSON saveData = new PlayerNamesJSON();
            saveData.names = new List<string>(playerNames);
            string jsonString = JsonUtility.ToJson(saveData);
            PlayerPrefs.SetString(PLAYER_NAMES_KEY, jsonString);
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogError($"Fehler beim Speichern der Spieler: {e.Message}");
        }
    }
    
    // Methode zum Löschen aller gespeicherten Spieler (optional für Debug/Reset)
    public void ClearSavedPlayers()
    {
        PlayerPrefs.DeleteKey(PLAYER_NAMES_KEY);
        PlayerPrefs.Save();
        playerNames.Clear();
        RefreshUI();
    }
    
    void LoadPlayerNames()
    {
        if (playerNamesFile == null)
        {
            Debug.LogWarning("Keine Namen-JSON-Datei zugewiesen! Verwende Standard-Namen.");
            availableNames = new List<string> { "Alex", "Sam", "Chris", "Jordan", "Casey", "Taylor", "Robin", "Jamie" };
            return;
        }
        
        try
        {
            string jsonString = playerNamesFile.text;
            PlayerNamesJSON namesData = JsonUtility.FromJson<PlayerNamesJSON>(jsonString);
            availableNames = new List<string>(namesData.names);
            
            if (availableNames.Count == 0)
            {
                Debug.LogWarning("Namen-Liste ist leer! Verwende Standard-Namen.");
                availableNames = new List<string> { "Alex", "Sam", "Chris", "Jordan" };
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Fehler beim Laden der Namen-JSON: {e.Message}");
            availableNames = new List<string> { "Alex", "Sam", "Chris", "Jordan" };
        }
    }
    
    string GetRandomName()
    {
        if (availableNames.Count == 0) return "Spieler";
        
        // Verwende noch nicht verwendete Namen
        List<string> unusedNames = new List<string>();
        foreach (string name in availableNames)
        {
            if (!playerNames.Contains(name))
            {
                unusedNames.Add(name);
            }
        }
        
        // Wenn alle Namen verwendet, nimm einen zufälligen
        if (unusedNames.Count == 0)
        {
            return availableNames[UnityEngine.Random.Range(0, availableNames.Count)] + " " + (playerNames.Count + 1);
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
        
        // Zyklisch durch die Prefabs gehen
        GameObject prefab = playerRowPrefabs[currentPrefabIndex];
        currentPrefabIndex = (currentPrefabIndex + 1) % playerRowPrefabs.Count;
        return prefab;
    }
    
    void RefreshUI()
    {
        // Alle vorhandenen Rows löschen
        foreach (Transform child in playerContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Prefab-Index zurücksetzen für konsistentes Aussehen
        currentPrefabIndex = 0;
        
        // Rows für alle Spieler erstellen
        for (int i = 0; i < playerNames.Count; i++)
        {
            CreatePlayerRow(i, playerNames[i], false); // Ausgefüllte Rows
        }
        
        // Eine leere Row am Ende hinzufügen mit Namensvorschlag
        string suggestedName = GetRandomName();
        CreatePlayerRow(playerNames.Count, suggestedName, true); // Leere Row mit Vorschlag
        
        UpdateStartButton();
    }
    
    void CreatePlayerRow(int index, string playerName, bool isEmpty)
    {
        GameObject prefab = GetNextPlayerRowPrefab();
        if (prefab == null) return;
        
        GameObject newRow = Instantiate(prefab, playerContainer);
        
        // Sichere Komponenten-Suche
        TMP_InputField input = newRow.GetComponentInChildren<TMP_InputField>();
        if (input == null)
        {
            Debug.LogError($"TMP_InputField nicht gefunden in Prefab: {prefab.name}");
            return;
        }
        
        // Flexible Button-Suche
        Button plusButton = null;
        Button minusButton = null;
        
        // Suche nach Buttons mit verschiedenen Namen
        Transform plusTransform = newRow.transform.Find("PlusButton");
        if (plusTransform == null) plusTransform = newRow.transform.Find("AddButton");
        if (plusTransform == null) plusTransform = newRow.transform.Find("Plus");
        if (plusTransform == null) plusTransform = newRow.transform.Find("+");
        
        Transform minusTransform = newRow.transform.Find("MinusButton");
        if (minusTransform == null) minusTransform = newRow.transform.Find("RemoveButton");
        if (minusTransform == null) minusTransform = newRow.transform.Find("Minus");
        if (minusTransform == null) minusTransform = newRow.transform.Find("-");
        
        // Fallback: Alle Buttons finden und nach Position sortieren
        if (plusTransform == null || minusTransform == null)
        {
            Button[] allButtons = newRow.GetComponentsInChildren<Button>();
            if (allButtons.Length >= 2)
            {
                // Nimm die ersten zwei Buttons
                plusButton = allButtons[0];
                minusButton = allButtons[1];
                Debug.LogWarning($"Button-Namen nicht standardisiert in {prefab.name}. Verwende erste zwei Buttons.");
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
        
        // Input setzen
        input.text = playerName;
        input.interactable = isEmpty; // Nur leere Rows sind editierbar
        
        if (isEmpty)
        {
            // Leere Row: Nur Plus-Button
            plusButton.gameObject.SetActive(true);
            minusButton.gameObject.SetActive(false);
            
            // Placeholder-Stil für Vorschlag (sicher prüfen)
            if (input.placeholder != null && input.placeholder.GetComponent<TextMeshProUGUI>() != null)
            {
                input.placeholder.GetComponent<TextMeshProUGUI>().text = "Name eingeben...";
            }
            
            plusButton.onClick.AddListener(() => {
                string name = input.text.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    playerNames.Add(name);
                    SavePlayers(); // Speichern nach dem Hinzufügen
                    RefreshUI();
                }
            });
            
            // Enter-Taste Support
            input.onEndEdit.AddListener((value) => {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    string name = value.Trim();
                    if (!string.IsNullOrEmpty(name))
                    {
                        playerNames.Add(name);
                        SavePlayers(); // Speichern nach dem Hinzufügen
                        RefreshUI();
                    }
                }
            });
        }
        else
        {
            // Ausgefüllte Row: Nur Minus-Button
            plusButton.gameObject.SetActive(false);
            minusButton.gameObject.SetActive(true);
            
            minusButton.onClick.AddListener(() => {
                playerNames.RemoveAt(index);
                SavePlayers(); // Speichern nach dem Entfernen
                RefreshUI();
            });
        }
    }
    
    void UpdateStartButton()
    {
        startGameButton.interactable = playerNames.Count >= 2;
        startGameButton.GetComponentInChildren<TextMeshProUGUI>().text = 
            playerNames.Count >= 2 ? $"Spiel starten ({playerNames.Count} Spieler)" : "Mindestens 2 Spieler";
    }
    
    void UpdateGameModeDisplay()
    {
        GameModeEnum mode = GameModeManager.instance.getGameMode();
        gameModeText.text = "Spielmodus: " + (mode == GameModeEnum.Classic ? "Classic" : "Flaschendrehen");
    }
    
    public void StartGame()
    {
        if (playerNames.Count < 2) return;
        
        List<Player> players = new List<Player>();
        foreach (string name in playerNames)
        {
            players.Add(new Player(name));
        }
        
        PlayerManager.instance.SetPlayers(players);
        NavigationSceneManager.instance.LoadScene("GameScene");
    }
    
    public void BackToMenu()
    {
        NavigationSceneManager.instance.LoadScene("MainMenu");
    }
    
    // Optional: Methode um einen Reset-Button im UI zu implementieren
    public void OnResetPlayersButtonClicked()
    {
        ClearSavedPlayers();
    }
}