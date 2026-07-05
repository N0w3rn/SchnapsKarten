using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

// Quiz mode: teams answer questions from JSON-driven categories, points per team,
// drink score-cards in between, scoreboard modal, winner screen at the end.
// The whole UI is built at runtime - the scene only needs a camera and this component.
public class QuizSceneManager : MonoBehaviour
{
    [Header("Game Settings")]
    [Range(1, 50)]
    public int minQuestionsPerTeam = 20;
    [Range(1, 50)]
    public int maxQuestionsPerTeam = 30;
    // A drink score-card appears every N answered questions (random in this range).
    [Range(1, 20)]
    public int minQuestionsBetweenScoreCards = 4;
    [Range(1, 20)]
    public int maxQuestionsBetweenScoreCards = 7;

    private enum Phase { TeamSetup, CategoryAnnounce, Question, Answer, ScoreCard, End }

    private class CategoryState
    {
        public QuizCategoryJSON def;
        public List<QuizQuestionJSON> remaining = new List<QuizQuestionJSON>();
    }

    private Phase phase = Phase.TeamSetup;
    private readonly List<Team> teams = new List<Team>();
    private List<CategoryState> categories = new List<CategoryState>();
    private List<int> categoryOrder = new List<int>();
    private int categoryOrderCursor = 0;
    private List<string> scoreCards = new List<string>();

    private int questionsPerTeam;
    private int roundsDone;
    private int teamCursor;
    private int scoreCardCountdown;
    private CategoryState currentCategory;
    private QuizQuestionJSON currentQuestion;
    private Team lastAnsweredTeam;
    private bool modalOpen;
    // Frame in which a UI button was clicked - screen taps in the same frame are
    // ignored so one press can't both trigger a button and advance the phase.
    private int lastButtonFrame = -1;

    // Colors
    private static readonly Color BackgroundColor = new Color(0.10f, 0.14f, 0.22f);
    private static readonly Color PanelColor = new Color(1f, 1f, 1f, 0.12f);
    private static readonly Color AccentColor = new Color(0.95f, 0.55f, 0.15f);
    private static readonly Color GreenColor = new Color(0.22f, 0.65f, 0.32f);
    private static readonly Color RedColor = new Color(0.78f, 0.25f, 0.25f);

    // Setup UI
    private GameObject setupRoot;
    private RectTransform setupListContent;
    private Button startButton;
    private TextMeshProUGUI startButtonLabel;

    // Game UI
    private GameObject gameRoot;
    private TextMeshProUGUI headerText;
    private TextMeshProUGUI bodyText;
    private Image categoryImage;
    private Button answerButtonA;
    private Button answerButtonB;
    private TextMeshProUGUI tapHintText;
    private Button scoreButton;
    private RectTransform endListContainer;

    // Scoreboard modal
    private GameObject modalRoot;
    private RectTransform modalListContent;

    void Start()
    {
        categories = BuildCategoryStates(QuizLoader.LoadCategories());
        scoreCards = QuizLoader.LoadScoreCards();

        if (categories.Count == 0)
        {
            Debug.LogError("QuizSceneManager: no quiz categories loaded - returning to menu.");
            NavigationSceneManager.instance.LoadScene("MainMenu");
            return;
        }

        Canvas canvas = UiFactory.CreateCanvas("QuizCanvas");
        RectTransform background = UiFactory.CreatePanel("Background", canvas.transform, BackgroundColor);
        UiFactory.Stretch(background);
        background.GetComponent<Image>().raycastTarget = false;

        BuildSetupUI(canvas.transform);
        BuildGameUI(canvas.transform);
        BuildScoreboardModal(canvas.transform);

        teams.Add(new Team("Team 1"));
        teams.Add(new Team("Team 2"));
        RebuildSetupList();

        setupRoot.SetActive(true);
        gameRoot.SetActive(false);
        modalRoot.SetActive(false);
    }

    void Update()
    {
        if (!(Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)))
            return;
        if (modalOpen || Time.frameCount == lastButtonFrame || IsPointerOverUI()) return;

        switch (phase)
        {
            case Phase.CategoryAnnounce:
                ShowQuestion();
                break;
            case Phase.Question:
                ShowAnswer();
                break;
            case Phase.ScoreCard:
                AdvanceAfterQuestion();
                break;
            case Phase.End:
                BackToMenu();
                break;
        }
    }

    bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }

    // ---------- Team setup ----------

    void BuildSetupUI(Transform canvas)
    {
        setupRoot = new GameObject("SetupRoot", typeof(RectTransform)).gameObject;
        setupRoot.transform.SetParent(canvas, false);
        UiFactory.Stretch((RectTransform)setupRoot.transform);

        TextMeshProUGUI title = UiFactory.CreateText("Title", setupRoot.transform, "Teams erstellen", 56, Color.white);
        title.raycastTarget = false;
        title.fontStyle = FontStyles.Bold;
        UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(1200f, 70f));

        ScrollRect scroll;
        setupListContent = UiFactory.CreateScrollView("TeamList", setupRoot.transform, out scroll);
        RectTransform scrollRect = (RectTransform)scroll.transform;
        scrollRect.anchorMin = new Vector2(0.5f, 0f);
        scrollRect.anchorMax = new Vector2(0.5f, 1f);
        scrollRect.pivot = new Vector2(0.5f, 0.5f);
        scrollRect.sizeDelta = new Vector2(1100f, -230f);
        scrollRect.anchoredPosition = new Vector2(0f, 15f);

        Button backButton = UiFactory.CreateButton("BackButton", setupRoot.transform, "Zurück", 36,
            new Color(1f, 1f, 1f, 0.15f), Color.white);
        UiFactory.Place((RectTransform)backButton.transform, new Vector2(0f, 0f), new Vector2(30f, 25f), new Vector2(240f, 90f));
        backButton.onClick.AddListener(BackToMenu);

        Button addTeamButton = UiFactory.CreateButton("AddTeamButton", setupRoot.transform, "+ Team", 36,
            AccentColor, Color.white);
        UiFactory.Place((RectTransform)addTeamButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, 25f), new Vector2(280f, 90f));
        addTeamButton.onClick.AddListener(() =>
        {
            teams.Add(new Team($"Team {teams.Count + 1}"));
            RebuildSetupList();
        });

        startButton = UiFactory.CreateButton("StartButton", setupRoot.transform, "Start", 36, GreenColor, Color.white);
        UiFactory.Place((RectTransform)startButton.transform, new Vector2(1f, 0f), new Vector2(-30f, 25f), new Vector2(340f, 90f));
        startButtonLabel = startButton.GetComponentInChildren<TextMeshProUGUI>();
        startButton.onClick.AddListener(StartGame);
    }

    void RebuildSetupList()
    {
        foreach (Transform child in setupListContent)
        {
            Destroy(child.gameObject);
        }

        for (int t = 0; t < teams.Count; t++)
        {
            int teamIndex = t;
            Team team = teams[t];

            RectTransform block = UiFactory.CreatePanel($"Team{t}", setupListContent, PanelColor, rounded: true);
            VerticalLayoutGroup blockLayout = block.gameObject.AddComponent<VerticalLayoutGroup>();
            blockLayout.childControlWidth = true;
            blockLayout.childControlHeight = true;
            blockLayout.childForceExpandWidth = true;
            blockLayout.childForceExpandHeight = false;
            blockLayout.spacing = 8f;
            blockLayout.padding = new RectOffset(16, 16, 12, 12);
            block.GetComponent<Image>().raycastTarget = false;

            // Team name + remove team
            RectTransform nameRow = CreateRow(block);
            TMP_InputField nameInput = UiFactory.CreateInputField("TeamName", nameRow, "Teamname", 34);
            AddFlexible(nameInput.gameObject);
            nameInput.text = team.name;
            nameInput.onEndEdit.AddListener(value =>
            {
                team.name = string.IsNullOrWhiteSpace(value) ? $"Team {teamIndex + 1}" : value.Trim();
            });
            Button removeTeam = UiFactory.CreateButton("RemoveTeam", nameRow, "X", 34, RedColor, Color.white);
            AddFixed(removeTeam.gameObject, 80f);
            removeTeam.onClick.AddListener(() =>
            {
                teams.RemoveAt(teamIndex);
                RebuildSetupList();
            });

            // Existing players
            for (int p = 0; p < team.players.Count; p++)
            {
                int playerIndex = p;
                RectTransform playerRow = CreateRow(block);
                TextMeshProUGUI playerLabel = UiFactory.CreateText("Player", playerRow,
                    team.players[p], 32, Color.white, TextAlignmentOptions.Left);
                playerLabel.raycastTarget = false;
                playerLabel.margin = new Vector4(14f, 0f, 0f, 0f);
                AddFlexible(playerLabel.gameObject);
                Button removePlayer = UiFactory.CreateButton("RemovePlayer", playerRow, "-", 34,
                    new Color(1f, 1f, 1f, 0.2f), Color.white);
                AddFixed(removePlayer.gameObject, 80f);
                removePlayer.onClick.AddListener(() =>
                {
                    team.players.RemoveAt(playerIndex);
                    RebuildSetupList();
                });
            }

            // New player input
            RectTransform addRow = CreateRow(block);
            TMP_InputField playerInput = UiFactory.CreateInputField("NewPlayer", addRow, "Spieler hinzufügen...", 32);
            AddFlexible(playerInput.gameObject);
            Button addPlayer = UiFactory.CreateButton("AddPlayer", addRow, "+", 36, GreenColor, Color.white);
            AddFixed(addPlayer.gameObject, 80f);

            void CommitPlayer()
            {
                string name = playerInput.text.Trim();
                if (string.IsNullOrEmpty(name)) return;
                team.players.Add(name);
                RebuildSetupList();
            }
            addPlayer.onClick.AddListener(CommitPlayer);
            playerInput.onSubmit.AddListener(_ => CommitPlayer());
        }

        UpdateStartButton();
    }

    RectTransform CreateRow(Transform parent)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.spacing = 10f;
        LayoutElement element = row.AddComponent<LayoutElement>();
        element.preferredHeight = 72f;
        return (RectTransform)row.transform;
    }

    void AddFlexible(GameObject obj)
    {
        LayoutElement element = obj.AddComponent<LayoutElement>();
        element.flexibleWidth = 1f;
    }

    void AddFixed(GameObject obj, float width)
    {
        LayoutElement element = obj.AddComponent<LayoutElement>();
        element.preferredWidth = width;
        element.flexibleWidth = 0f;
    }

    void UpdateStartButton()
    {
        bool enoughTeams = teams.Count >= 2;
        bool allTeamsHavePlayers = true;
        foreach (Team team in teams)
        {
            if (team.players.Count == 0) allTeamsHavePlayers = false;
        }

        startButton.interactable = enoughTeams && allTeamsHavePlayers;
        if (!enoughTeams) startButtonLabel.text = "Min. 2 Teams";
        else if (!allTeamsHavePlayers) startButtonLabel.text = "Spieler fehlen";
        else startButtonLabel.text = "Start";
    }

    // ---------- Game UI ----------

    void BuildGameUI(Transform canvas)
    {
        gameRoot = new GameObject("GameRoot", typeof(RectTransform)).gameObject;
        gameRoot.transform.SetParent(canvas, false);
        UiFactory.Stretch((RectTransform)gameRoot.transform);

        headerText = UiFactory.CreateText("Header", gameRoot.transform, "", 48, AccentColor);
        headerText.raycastTarget = false;
        headerText.fontStyle = FontStyles.Bold;
        UiFactory.Place(headerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(1300f, 70f));

        GameObject imageObj = new GameObject("CategoryImage", typeof(RectTransform));
        imageObj.transform.SetParent(gameRoot.transform, false);
        categoryImage = imageObj.AddComponent<Image>();
        categoryImage.preserveAspect = true;
        categoryImage.raycastTarget = false;
        UiFactory.Place((RectTransform)imageObj.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(900f, 620f));

        bodyText = UiFactory.CreateText("Body", gameRoot.transform, "", 52, Color.white);
        bodyText.raycastTarget = false;
        UiFactory.Place(bodyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1500f, 700f));

        answerButtonA = UiFactory.CreateButton("AnswerA", gameRoot.transform, "Richtig", 40, GreenColor, Color.white);
        UiFactory.Place((RectTransform)answerButtonA.transform, new Vector2(0.5f, 0f), new Vector2(-240f, 40f), new Vector2(420f, 110f));
        answerButtonA.onClick.AddListener(() => OnAnswered(true));

        answerButtonB = UiFactory.CreateButton("AnswerB", gameRoot.transform, "Falsch", 40, RedColor, Color.white);
        UiFactory.Place((RectTransform)answerButtonB.transform, new Vector2(0.5f, 0f), new Vector2(240f, 40f), new Vector2(420f, 110f));
        answerButtonB.onClick.AddListener(() => OnAnswered(false));

        tapHintText = UiFactory.CreateText("TapHint", gameRoot.transform, "Tippen zum Fortfahren", 28,
            new Color(1f, 1f, 1f, 0.45f));
        tapHintText.raycastTarget = false;
        UiFactory.Place(tapHintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(800f, 45f));

        scoreButton = UiFactory.CreateButton("ScoreButton", gameRoot.transform, "Punkte", 32,
            new Color(1f, 1f, 1f, 0.18f), Color.white);
        UiFactory.Place((RectTransform)scoreButton.transform, new Vector2(1f, 1f), new Vector2(-25f, -25f), new Vector2(230f, 80f));
        scoreButton.onClick.AddListener(OpenScoreboard);

        Button menuButton = UiFactory.CreateButton("MenuButton", gameRoot.transform, "X", 32,
            new Color(1f, 1f, 1f, 0.18f), Color.white);
        UiFactory.Place((RectTransform)menuButton.transform, new Vector2(0f, 1f), new Vector2(25f, -25f), new Vector2(80f, 80f));
        menuButton.onClick.AddListener(BackToMenu);

        ScrollRect endScroll;
        endListContainer = UiFactory.CreateScrollView("EndScoreboard", gameRoot.transform, out endScroll);
        RectTransform endRect = (RectTransform)endScroll.transform;
        UiFactory.Place(endRect, new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(900f, 480f));
    }

    void BuildScoreboardModal(Transform canvas)
    {
        modalRoot = new GameObject("ScoreboardModal", typeof(RectTransform)).gameObject;
        modalRoot.transform.SetParent(canvas, false);
        UiFactory.Stretch((RectTransform)modalRoot.transform);

        // Fullscreen overlay swallows all taps behind the modal.
        RectTransform overlay = UiFactory.CreatePanel("Overlay", modalRoot.transform, new Color(0f, 0f, 0f, 0.7f));
        UiFactory.Stretch(overlay);
        Button overlayButton = overlay.gameObject.AddComponent<Button>();
        overlayButton.transition = Selectable.Transition.None;
        overlayButton.onClick.AddListener(CloseScoreboard);

        RectTransform panel = UiFactory.CreatePanel("Panel", modalRoot.transform, new Color(0.14f, 0.18f, 0.27f), rounded: true);
        UiFactory.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 800f));

        TextMeshProUGUI title = UiFactory.CreateText("Title", panel, "Punktestand", 46, Color.white);
        title.raycastTarget = false;
        title.fontStyle = FontStyles.Bold;
        UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(800f, 60f));

        Button closeButton = UiFactory.CreateButton("Close", panel, "X", 34, RedColor, Color.white);
        UiFactory.Place((RectTransform)closeButton.transform, new Vector2(1f, 1f), new Vector2(-15f, -15f), new Vector2(70f, 70f));
        closeButton.onClick.AddListener(CloseScoreboard);

        ScrollRect scroll;
        modalListContent = UiFactory.CreateScrollView("ScoreList", panel, out scroll);
        RectTransform scrollRect = (RectTransform)scroll.transform;
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.pivot = new Vector2(0.5f, 0.5f);
        scrollRect.offsetMin = new Vector2(40f, 30f);
        scrollRect.offsetMax = new Vector2(-40f, -110f);
    }

    void OpenScoreboard()
    {
        PopulateScoreList(modalListContent);
        modalRoot.SetActive(true);
        modalOpen = true;
    }

    void CloseScoreboard()
    {
        lastButtonFrame = Time.frameCount;
        modalRoot.SetActive(false);
        modalOpen = false;
    }

    void PopulateScoreList(RectTransform container)
    {
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }

        List<Team> ordered = GetTeamsByScore();
        for (int i = 0; i < ordered.Count; i++)
        {
            RectTransform row = UiFactory.CreatePanel($"Score{i}", container, PanelColor, rounded: true);
            row.GetComponent<Image>().raycastTarget = false;
            LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 80f;

            TextMeshProUGUI text = UiFactory.CreateText("Text", row,
                $"{i + 1}. {ordered[i].name}   -   {ordered[i].score} Punkte", 36, Color.white, TextAlignmentOptions.Left);
            text.raycastTarget = false;
            text.margin = new Vector4(24f, 0f, 24f, 0f);
            UiFactory.Stretch(text.rectTransform);
        }
    }

    List<Team> GetTeamsByScore()
    {
        List<Team> ordered = new List<Team>(teams);
        ordered.Sort((a, b) => b.score.CompareTo(a.score));
        return ordered;
    }

    // ---------- Game flow ----------

    void StartGame()
    {
        lastButtonFrame = Time.frameCount;
        questionsPerTeam = Random.Range(minQuestionsPerTeam, maxQuestionsPerTeam + 1);
        roundsDone = 0;
        scoreCardCountdown = Random.Range(minQuestionsBetweenScoreCards, maxQuestionsBetweenScoreCards + 1);
        ShuffleCategoryOrder();

        setupRoot.SetActive(false);
        gameRoot.SetActive(true);
        StartCategoryRound();
    }

    List<CategoryState> BuildCategoryStates(List<QuizCategoryJSON> defs)
    {
        List<CategoryState> states = new List<CategoryState>();
        foreach (QuizCategoryJSON def in defs)
        {
            if (def.questions == null || def.questions.Count == 0) continue;
            CategoryState state = new CategoryState { def = def };
            RefillCategory(state);
            states.Add(state);
        }
        return states;
    }

    void RefillCategory(CategoryState state)
    {
        state.remaining = new List<QuizQuestionJSON>(state.def.questions);
        for (int i = state.remaining.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (state.remaining[i], state.remaining[j]) = (state.remaining[j], state.remaining[i]);
        }
    }

    void ShuffleCategoryOrder()
    {
        categoryOrder.Clear();
        for (int i = 0; i < categories.Count; i++) categoryOrder.Add(i);
        for (int i = categoryOrder.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (categoryOrder[i], categoryOrder[j]) = (categoryOrder[j], categoryOrder[i]);
        }
        categoryOrderCursor = 0;
    }

    void StartCategoryRound()
    {
        if (roundsDone >= questionsPerTeam)
        {
            ShowEnd();
            return;
        }

        if (categoryOrderCursor >= categoryOrder.Count) ShuffleCategoryOrder();
        currentCategory = categories[categoryOrder[categoryOrderCursor]];
        categoryOrderCursor++;
        teamCursor = 0;

        phase = Phase.CategoryAnnounce;
        Sprite image = QuizLoader.LoadCategoryImage(currentCategory.def.imageName);
        headerText.text = "Kategorie";
        if (image != null)
        {
            categoryImage.sprite = image;
            SetGameElements(showImage: true, body: "", showAnswerButtons: false, showTapHint: true, showEndList: false);
        }
        else
        {
            SetGameElements(showImage: false, body: currentCategory.def.name, showAnswerButtons: false,
                showTapHint: true, showEndList: false);
            bodyText.fontSize = 90;
            bodyText.fontStyle = FontStyles.Bold;
        }
    }

    void ShowQuestion()
    {
        // Categories can hold fewer questions than there are teams - refill instead of
        // blocking, so a question may repeat within a round in that edge case.
        if (currentCategory.remaining.Count == 0) RefillCategory(currentCategory);
        currentQuestion = currentCategory.remaining[currentCategory.remaining.Count - 1];
        currentCategory.remaining.RemoveAt(currentCategory.remaining.Count - 1);

        phase = Phase.Question;
        headerText.text = $"{teams[teamCursor].name}  -  {currentCategory.def.name}";
        SetGameElements(showImage: false, body: currentQuestion.question, showAnswerButtons: false,
            showTapHint: true, showEndList: false);
    }

    void ShowAnswer()
    {
        phase = Phase.Answer;
        TextMeshProUGUI labelA = answerButtonA.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI labelB = answerButtonB.GetComponentInChildren<TextMeshProUGUI>();

        string body;
        if (currentCategory.def.isAction)
        {
            body = "Geschafft?";
            labelA.text = "Ja";
            labelB.text = "Nein";
        }
        else
        {
            body = $"Antwort:\n\n{currentQuestion.answer}";
            labelA.text = "Richtig";
            labelB.text = "Falsch";
        }

        SetGameElements(showImage: false, body: body, showAnswerButtons: true, showTapHint: false, showEndList: false);
    }

    void OnAnswered(bool correct)
    {
        if (phase != Phase.Answer) return;
        lastButtonFrame = Time.frameCount;

        Team team = teams[teamCursor];
        if (correct) team.score++;
        lastAnsweredTeam = team;
        teamCursor++;

        scoreCardCountdown--;
        if (scoreCardCountdown <= 0 && scoreCards.Count > 0)
        {
            scoreCardCountdown = Random.Range(minQuestionsBetweenScoreCards, maxQuestionsBetweenScoreCards + 1);
            ShowScoreCard();
            return;
        }

        AdvanceAfterQuestion();
    }

    void ShowScoreCard()
    {
        phase = Phase.ScoreCard;
        string text = ResolveTeamPlaceholders(scoreCards[Random.Range(0, scoreCards.Count)]);
        headerText.text = "Trink-Zeit!";
        SetGameElements(showImage: false, body: text, showAnswerButtons: false, showTapHint: true, showEndList: false);
    }

    void AdvanceAfterQuestion()
    {
        if (teamCursor >= teams.Count)
        {
            roundsDone++;
            StartCategoryRound();
        }
        else
        {
            ShowQuestion();
        }
    }

    void ShowEnd()
    {
        phase = Phase.End;
        List<Team> ordered = GetTeamsByScore();
        bool tie = ordered.Count > 1 && ordered[0].score == ordered[1].score;
        headerText.text = tie ? "Unentschieden!" : $"{ordered[0].name} gewinnt!";

        SetGameElements(showImage: false, body: "", showAnswerButtons: false, showTapHint: true, showEndList: true);
        PopulateScoreList(endListContainer);
    }

    void SetGameElements(bool showImage, string body, bool showAnswerButtons, bool showTapHint, bool showEndList)
    {
        categoryImage.gameObject.SetActive(showImage);
        bodyText.text = body;
        bodyText.fontSize = 52;
        bodyText.fontStyle = FontStyles.Normal;
        bodyText.gameObject.SetActive(!string.IsNullOrEmpty(body));
        answerButtonA.gameObject.SetActive(showAnswerButtons);
        answerButtonB.gameObject.SetActive(showAnswerButtons);
        tapHintText.gameObject.SetActive(showTapHint);
        endListContainer.parent.gameObject.SetActive(showEndList);
    }

    // ---------- Placeholders ----------

    string ResolveTeamPlaceholders(string text)
    {
        Team current = lastAnsweredTeam != null ? lastAnsweredTeam : teams[0];
        List<Team> ordered = GetTeamsByScore();
        Team leading = ordered[0];
        Team last = ordered[ordered.Count - 1];

        // Pick one random other team so repeated {OTHER_TEAM} tokens refer to the same team.
        List<Team> others = new List<Team>();
        foreach (Team team in teams)
        {
            if (team != current) others.Add(team);
        }
        Team other = others.Count > 0 ? others[Random.Range(0, others.Count)] : current;

        string player = current.players.Count > 0
            ? current.players[Random.Range(0, current.players.Count)]
            : current.name;

        return text
            .Replace("{CURRENT_TEAM}", current.name)
            .Replace("{CURRENT_TEAM_SCORE}", current.score.ToString())
            .Replace("{OTHER_TEAM}", other.name)
            .Replace("{OTHER_TEAM_SCORE}", other.score.ToString())
            .Replace("{LEADING_TEAM}", leading.name)
            .Replace("{LEADING_TEAM_SCORE}", leading.score.ToString())
            .Replace("{LAST_TEAM}", last.name)
            .Replace("{LAST_TEAM_SCORE}", last.score.ToString())
            .Replace("{TEAM_PLAYER}", player);
    }

    void BackToMenu()
    {
        NavigationSceneManager.instance.LoadScene("MainMenu");
    }
}
