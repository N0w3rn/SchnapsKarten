using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

// Team setup for the quiz mode. Deliberately mirrors PlayerSetupManager so the
// QuizSetup scene can be a duplicate of the PlayerSetup scene: same PlayerRow
// prefabs, same name suggestions, one start button, arrow back button.
// Teams are shown as sections ("Team 1", "Team 2", ...) with player rows beneath.
public class QuizSetupManager : MonoBehaviour
{
    [Header("UI")]
    public Transform playerContainer;
    public List<GameObject> playerRowPrefabs = new List<GameObject>();
    public Button startGameButton;
    public Button backButton;
    public TextMeshProUGUI gameModeText;

    [Header("Name Suggestions")]
    public TextAsset playerNamesFile;

    private readonly List<Team> teams = new List<Team>();
    private List<string> availableNames = new List<string>();
    private int currentPrefabIndex = 0;

    private static readonly Color HeaderColor = new Color(0.07f, 0.42f, 0.49f);
    private static readonly Color IconGrayColor = new Color(0.55f, 0.55f, 0.55f);

    void Start()
    {
        LoadPlayerNames();

        teams.Add(new Team("Team 1"));
        teams.Add(new Team("Team 2"));

        if (gameModeText != null) gameModeText.text = "Spielmodus: Quiz";
        RefreshUI();

        startGameButton.onClick.AddListener(StartGame);
        backButton.onClick.AddListener(BackToMenu);
    }

    void LoadPlayerNames()
    {
        if (playerNamesFile == null)
        {
            Debug.LogWarning("No names JSON assigned! Using default names.");
            availableNames = new List<string> { "Alex", "Sam", "Chris", "Jordan", "Casey", "Taylor", "Robin", "Jamie" };
            return;
        }

        try
        {
            PlayerNamesJSON namesData = JsonUtility.FromJson<PlayerNamesJSON>(playerNamesFile.text);
            availableNames = new List<string>(namesData.names);

            if (availableNames.Count == 0)
            {
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

        List<string> usedNames = new List<string>();
        foreach (Team team in teams) usedNames.AddRange(team.players);

        List<string> unusedNames = new List<string>();
        foreach (string name in availableNames)
        {
            if (!usedNames.Contains(name)) unusedNames.Add(name);
        }

        if (unusedNames.Count == 0)
        {
            return availableNames[UnityEngine.Random.Range(0, availableNames.Count)] + " " + (usedNames.Count + 1);
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

        for (int t = 0; t < teams.Count; t++)
        {
            CreateTeamHeader(t);

            for (int p = 0; p < teams[t].players.Count; p++)
            {
                CreatePlayerRow(t, p, teams[t].players[p], false);
            }

            CreatePlayerRow(t, teams[t].players.Count, GetRandomName(), true);
        }

        CreateAddTeamRow();
        UpdateStartButton();
    }

    void CreateTeamHeader(int teamIndex)
    {
        GameObject header = new GameObject($"TeamHeader{teamIndex}", typeof(RectTransform));
        header.transform.SetParent(playerContainer, false);
        LayoutElement element = header.AddComponent<LayoutElement>();
        element.preferredHeight = 70f;

        TextMeshProUGUI label = UiFactory.CreateText("Label", header.transform,
            teams[teamIndex].name, 44, HeaderColor, TextAlignmentOptions.Left);
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        UiFactory.Stretch(label.rectTransform, 20f, 0f);

        // Teams beyond the required two can be removed via a small X next to the header.
        if (teams.Count > 2)
        {
            Button remove = UiFactory.CreateButton("RemoveTeam", header.transform, "X", 32,
                Color.white, IconGrayColor);
            RectTransform removeRect = (RectTransform)remove.transform;
            UiFactory.Place(removeRect, new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(60f, 60f));
            int index = teamIndex;
            remove.onClick.AddListener(() =>
            {
                teams.RemoveAt(index);
                RenameTeams();
                RefreshUI();
            });
        }
    }

    void RenameTeams()
    {
        for (int i = 0; i < teams.Count; i++)
        {
            teams[i].name = $"Team {i + 1}";
        }
    }

    void CreateAddTeamRow()
    {
        GameObject row = new GameObject("AddTeamRow", typeof(RectTransform));
        row.transform.SetParent(playerContainer, false);
        LayoutElement element = row.AddComponent<LayoutElement>();
        element.preferredHeight = 90f;

        Button addTeam = UiFactory.CreateButton("AddTeam", row.transform, "+ Team hinzufügen", 34,
            Color.white, HeaderColor);
        RectTransform rect = (RectTransform)addTeam.transform;
        UiFactory.Place(rect, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 80f));
        addTeam.onClick.AddListener(() =>
        {
            teams.Add(new Team($"Team {teams.Count + 1}"));
            RefreshUI();
        });
    }

    void CreatePlayerRow(int teamIndex, int playerIndex, string playerName, bool isEmpty)
    {
        GameObject prefab = GetNextPlayerRowPrefab();
        if (prefab == null) return;

        GameObject newRow = Instantiate(prefab, playerContainer);
        Team team = teams[teamIndex];

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

        input.text = playerName;
        input.interactable = isEmpty;

        if (isEmpty)
        {
            plusButton.gameObject.SetActive(true);
            minusButton.gameObject.SetActive(false);

            if (input.placeholder != null && input.placeholder.GetComponent<TextMeshProUGUI>() != null)
            {
                input.placeholder.GetComponent<TextMeshProUGUI>().text = "Name eingeben...";
            }

            plusButton.onClick.AddListener(() =>
            {
                string name = input.text.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    team.players.Add(name);
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
                        team.players.Add(name);
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
                team.players.RemoveAt(playerIndex);
                RefreshUI();
            });
        }
    }

    void UpdateStartButton()
    {
        bool enoughTeams = teams.Count >= 2;
        bool allTeamsHavePlayers = true;
        foreach (Team team in teams)
        {
            if (team.players.Count == 0) allTeamsHavePlayers = false;
        }

        startGameButton.interactable = enoughTeams && allTeamsHavePlayers;
        TextMeshProUGUI label = startGameButton.GetComponentInChildren<TextMeshProUGUI>();
        if (!enoughTeams) label.text = "Mindestens 2 Teams";
        else if (!allTeamsHavePlayers) label.text = "Jedes Team braucht Spieler";
        else label.text = $"Quiz starten ({teams.Count} Teams)";
    }

    public void StartGame()
    {
        if (!startGameButton.interactable) return;

        QuizSession.teams = new List<Team>(teams);
        NavigationSceneManager.instance.LoadScene("QuizScene");
    }

    public void BackToMenu()
    {
        NavigationSceneManager.instance.LoadScene("MainMenu");
    }
}
