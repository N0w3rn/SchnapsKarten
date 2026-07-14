using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections;
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

    private enum Phase { CategoryAnnounce, Question, Answer, ScoreCard, End }

    private class CategoryState
    {
        public QuizCategoryJSON def;
        public List<QuizQuestionJSON> remaining = new List<QuizQuestionJSON>();
    }

    private Phase phase = Phase.CategoryAnnounce;
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

    // App palette - matches GetCardColor in GameSceneManager and the menu/setup artwork
    // (beige background, teal shapes, orange accents, white pills with teal outline).
    private static readonly Color BeigeColor = new Color(0.929f, 0.906f, 0.890f);
    private static readonly Color LightTealColor = new Color(0.51f, 0.75f, 0.8f);
    private static readonly Color TealColor = new Color(0.28f, 0.62f, 0.71f);
    private static readonly Color DarkTealColor = new Color(0.07f, 0.42f, 0.49f);
    private static readonly Color OrangeColor = new Color(1f, 0.65f, 0.17f);
    private static readonly Color PurpleColor = new Color(0.8f, 0.3f, 0.8f);
    private static readonly Color TextDarkColor = new Color(0.196f, 0.196f, 0.196f);
    private static readonly Color PanelWhiteColor = new Color(1f, 1f, 1f, 0.6f);
    private static readonly Color GreenColor = new Color(0.22f, 0.65f, 0.32f);
    private static readonly Color RedColor = new Color(0.78f, 0.25f, 0.25f);

    // Game UI
    private Image backgroundImage;
    private GameObject gameRoot;
    private TextMeshProUGUI headerText;
    private RectTransform headerScrim;
    private TextMeshProUGUI bodyText;
    private Image categoryImage;
    private RectTransform categoryImageContainerRect;
    private RectTransform tapHintScrim;
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
        // Teams are built in the QuizSetup scene and handed over via QuizSession.
        if (QuizSession.teams == null || QuizSession.teams.Count < 2)
        {
            Debug.LogError("QuizSceneManager: no teams set up - returning to menu.");
            NavigationSceneManager.instance.LoadScene("MainMenu");
            return;
        }
        teams.AddRange(QuizSession.teams);

        categories = BuildCategoryStates(QuizLoader.LoadCategories());
        scoreCards = QuizLoader.LoadScoreCards();

        if (categories.Count == 0)
        {
            Debug.LogError("QuizSceneManager: no quiz categories loaded - returning to menu.");
            NavigationSceneManager.instance.LoadScene("MainMenu");
            return;
        }

        Canvas canvas = UiFactory.CreateCanvas("QuizCanvas");
        RectTransform background = UiFactory.CreatePanel("Background", canvas.transform, BeigeColor);
        UiFactory.Stretch(background);
        backgroundImage = background.GetComponent<Image>();
        backgroundImage.raycastTarget = false;

        BuildGameUI(canvas.transform);
        BuildScoreboardModal(canvas.transform);

        gameRoot.SetActive(true);
        modalRoot.SetActive(false);

        StartGame();
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

    // ---------- Game UI ----------

    void BuildGameUI(Transform canvas)
    {
        gameRoot = new GameObject("GameRoot", typeof(RectTransform)).gameObject;
        gameRoot.transform.SetParent(canvas, false);
        UiFactory.Stretch((RectTransform)gameRoot.transform);

        // Full-bleed category image: a masked container clips an oversized, aspect-correct
        // image so it covers the whole screen without letterboxing or distortion.
        GameObject imageContainerObj = new GameObject("CategoryImageContainer", typeof(RectTransform));
        imageContainerObj.transform.SetParent(gameRoot.transform, false);
        categoryImageContainerRect = (RectTransform)imageContainerObj.transform;
        UiFactory.Stretch(categoryImageContainerRect);
        imageContainerObj.AddComponent<RectMask2D>();

        GameObject imageObj = new GameObject("CategoryImage", typeof(RectTransform));
        imageObj.transform.SetParent(imageContainerObj.transform, false);
        categoryImage = imageObj.AddComponent<Image>();
        categoryImage.preserveAspect = false;
        categoryImage.raycastTarget = false;
        RectTransform imageRect = (RectTransform)imageObj.transform;
        imageRect.anchorMin = imageRect.anchorMax = new Vector2(0.5f, 0.5f);
        imageRect.pivot = new Vector2(0.5f, 0.5f);

        headerText = UiFactory.CreateText("Header", gameRoot.transform, "", 48, Color.white);
        headerText.raycastTarget = false;
        headerText.fontStyle = FontStyles.Bold;
        UiFactory.Place(headerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -25f), new Vector2(1300f, 70f));

        // Dark scrim bars keep the header/tap-hint text legible over an arbitrary photo.
        headerScrim = UiFactory.CreatePanel("HeaderScrim", gameRoot.transform, new Color(0f, 0f, 0f, 0.4f));
        headerScrim.GetComponent<Image>().raycastTarget = false;
        headerScrim.anchorMin = new Vector2(0f, 1f);
        headerScrim.anchorMax = new Vector2(1f, 1f);
        headerScrim.pivot = new Vector2(0.5f, 1f);
        headerScrim.anchoredPosition = Vector2.zero;
        headerScrim.sizeDelta = new Vector2(0f, 130f);
        headerScrim.SetSiblingIndex(headerText.transform.GetSiblingIndex());

        bodyText = UiFactory.CreateText("Body", gameRoot.transform, "", 52, Color.white);
        bodyText.raycastTarget = false;
        UiFactory.Place(bodyText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(1500f, 700f));

        answerButtonA = UiFactory.CreateButton("AnswerA", gameRoot.transform, "Richtig", 40, GreenColor, Color.white);
        UiFactory.Place((RectTransform)answerButtonA.transform, new Vector2(0.5f, 0f), new Vector2(-240f, 40f), new Vector2(420f, 110f));
        answerButtonA.onClick.AddListener(() => OnAnswered(true));

        answerButtonB = UiFactory.CreateButton("AnswerB", gameRoot.transform, "Falsch", 40, RedColor, Color.white);
        UiFactory.Place((RectTransform)answerButtonB.transform, new Vector2(0.5f, 0f), new Vector2(240f, 40f), new Vector2(420f, 110f));
        answerButtonB.onClick.AddListener(() => OnAnswered(false));

        tapHintScrim = UiFactory.CreatePanel("TapHintScrim", gameRoot.transform, new Color(0f, 0f, 0f, 0.4f));
        tapHintScrim.GetComponent<Image>().raycastTarget = false;
        tapHintScrim.anchorMin = new Vector2(0f, 0f);
        tapHintScrim.anchorMax = new Vector2(1f, 0f);
        tapHintScrim.pivot = new Vector2(0.5f, 0f);
        tapHintScrim.anchoredPosition = Vector2.zero;
        tapHintScrim.sizeDelta = new Vector2(0f, 90f);

        tapHintText = UiFactory.CreateText("TapHint", gameRoot.transform, "Tippen zum Fortfahren", 28,
            new Color(1f, 1f, 1f, 0.45f));
        tapHintText.raycastTarget = false;
        UiFactory.Place(tapHintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(800f, 45f));
        tapHintScrim.SetSiblingIndex(tapHintText.transform.GetSiblingIndex());

        scoreButton = UiFactory.CreateButton("ScoreButton", gameRoot.transform, "Punkte", 32,
            DarkTealColor, Color.white);
        UiFactory.Place((RectTransform)scoreButton.transform, new Vector2(1f, 1f), new Vector2(-25f, -25f), new Vector2(230f, 80f));
        scoreButton.onClick.AddListener(OpenScoreboard);

        // Back-to-menu arrow, same graphic as the setup scenes (flipped to point left).
        Sprite arrowSprite = Resources.Load<Sprite>("QuizImages/arrow2");
        GameObject menuObj = new GameObject("MenuButton", typeof(RectTransform));
        menuObj.transform.SetParent(gameRoot.transform, false);
        Image menuImage = menuObj.AddComponent<Image>();
        menuImage.sprite = arrowSprite;
        menuImage.preserveAspect = true;
        Button menuButton = menuObj.AddComponent<Button>();
        menuButton.targetGraphic = menuImage;
        RectTransform menuRect = (RectTransform)menuObj.transform;
        UiFactory.Place(menuRect, new Vector2(0f, 1f), new Vector2(65f, -65f), new Vector2(80f, 80f));
        // Center pivot so the horizontal mirror flip keeps the button in place.
        menuRect.pivot = new Vector2(0.5f, 0.5f);
        menuRect.localScale = new Vector3(-1f, 1f, 1f);
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

        RectTransform panel = UiFactory.CreatePanel("Panel", modalRoot.transform, BeigeColor, rounded: true);
        UiFactory.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 800f));

        TextMeshProUGUI title = UiFactory.CreateText("Title", panel, "Punktestand", 46, DarkTealColor);
        title.raycastTarget = false;
        title.fontStyle = FontStyles.Bold;
        UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(800f, 60f));

        Button closeButton = UiFactory.CreateButton("Close", panel, "X", 34, OrangeColor, Color.white);
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
            RectTransform row = UiFactory.CreatePanel($"Score{i}", container, PanelWhiteColor, rounded: true);
            row.GetComponent<Image>().raycastTarget = false;
            LayoutElement element = row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 80f;

            TextMeshProUGUI text = UiFactory.CreateText("Text", row,
                $"{i + 1}. {ordered[i].name}   -   {ordered[i].score} Punkte", 36, TextDarkColor, TextAlignmentOptions.Left);
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
        questionsPerTeam = Random.Range(minQuestionsPerTeam, maxQuestionsPerTeam + 1);
        roundsDone = 0;
        scoreCardCountdown = Random.Range(minQuestionsBetweenScoreCards, maxQuestionsBetweenScoreCards + 1);
        ShuffleCategoryOrder();
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
        ApplyPage(OrangeColor, Color.white);
        Sprite image = QuizLoader.LoadCategoryImage(currentCategory.def.imageName);
        headerText.text = "Kategorie";
        if (image != null)
        {
            categoryImage.sprite = image;
            FitImageToScreen(categoryImageContainerRect, categoryImage.rectTransform, image);
            // On the very first category the canvas hasn't finished its initial layout
            // yet, so the container rect can still be wrong (unscaled screen size).
            // Re-fit one frame later, when the CanvasScaler has definitely run.
            StartCoroutine(RefitImageNextFrame(image));
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
        ApplyPage(TealColor, Color.white);
        headerText.text = $"{teams[teamCursor].name}  -  {currentCategory.def.name}";
        SetGameElements(showImage: false, body: currentQuestion.question, showAnswerButtons: false,
            showTapHint: true, showEndList: false);
    }

    void ShowAnswer()
    {
        phase = Phase.Answer;
        ApplyPage(LightTealColor, TextDarkColor);
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
        ApplyPage(PurpleColor, Color.white);
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
        ApplyPage(BeigeColor, DarkTealColor);
        List<Team> ordered = GetTeamsByScore();
        bool tie = ordered.Count > 1 && ordered[0].score == ordered[1].score;
        headerText.text = tie ? "Unentschieden!" : $"{ordered[0].name} gewinnt!";

        SetGameElements(showImage: false, body: "", showAnswerButtons: false, showTapHint: true, showEndList: true);
        PopulateScoreList(endListContainer);
    }

    // Each page type gets its own background color from the app palette,
    // with the text color chosen for contrast on it.
    void ApplyPage(Color background, Color text)
    {
        backgroundImage.color = background;
        headerText.color = text;
        bodyText.color = text;
        tapHintText.color = new Color(text.r, text.g, text.b, 0.5f);
    }

    // Compromise between "contain" (whole image visible, but can leave large borders)
    // and "cover" (fills the screen, but can crop a lot): scale up beyond contain to
    // fill the screen, but never crop more than ~1/6 of the image on the overflowing
    // axis - if the aspect ratios differ more than that, a small border remains.
    const float MaxImageOverfill = 1.3f;

    void FitImageToScreen(RectTransform container, RectTransform image, Sprite sprite)
    {
        float containerWidth = container.rect.width;
        float containerHeight = container.rect.height;
        if (containerWidth <= 0f || containerHeight <= 0f)
        {
            // The very first category is shown in the same frame the canvas is built,
            // before its first layout pass - the container rect is still 0. Force the
            // layout so the first image gets sized like all later ones.
            Canvas.ForceUpdateCanvases();
            containerWidth = container.rect.width;
            containerHeight = container.rect.height;
            if (containerWidth <= 0f || containerHeight <= 0f) return;
        }

        float spriteWidth = sprite.rect.width;
        float spriteHeight = sprite.rect.height;

        float containScale = Mathf.Min(containerWidth / spriteWidth, containerHeight / spriteHeight);
        float coverScale = Mathf.Max(containerWidth / spriteWidth, containerHeight / spriteHeight);
        float scale = Mathf.Min(coverScale, containScale * MaxImageOverfill);

        image.sizeDelta = new Vector2(spriteWidth * scale, spriteHeight * scale);
    }

    IEnumerator RefitImageNextFrame(Sprite sprite)
    {
        yield return null;
        if (categoryImage.sprite == sprite && categoryImageContainerRect.gameObject.activeInHierarchy)
        {
            FitImageToScreen(categoryImageContainerRect, categoryImage.rectTransform, sprite);
        }
    }

    void SetGameElements(bool showImage, string body, bool showAnswerButtons, bool showTapHint, bool showEndList)
    {
        categoryImageContainerRect.gameObject.SetActive(showImage);
        headerScrim.gameObject.SetActive(showImage);
        bodyText.text = body;
        bodyText.fontSize = 52;
        bodyText.fontStyle = FontStyles.Normal;
        bodyText.gameObject.SetActive(!string.IsNullOrEmpty(body));
        answerButtonA.gameObject.SetActive(showAnswerButtons);
        answerButtonB.gameObject.SetActive(showAnswerButtons);
        tapHintText.gameObject.SetActive(showTapHint);
        tapHintScrim.gameObject.SetActive(showTapHint && showImage);
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

        return text
            .Replace("{CURRENT_TEAM}", current.name)
            .Replace("{CURRENT_TEAM_SCORE}", current.score.ToString())
            .Replace("{OTHER_TEAM}", other.name)
            .Replace("{OTHER_TEAM_SCORE}", other.score.ToString())
            .Replace("{LEADING_TEAM}", leading.name)
            .Replace("{LEADING_TEAM_SCORE}", leading.score.ToString())
            .Replace("{LAST_TEAM}", last.name)
            .Replace("{LAST_TEAM_SCORE}", last.score.ToString());
    }

    void BackToMenu()
    {
        NavigationSceneManager.instance.LoadScene("MainMenu");
    }
}
