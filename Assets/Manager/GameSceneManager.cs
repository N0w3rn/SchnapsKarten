using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class GameSceneManager : MonoBehaviour
{
    [Header("Card Loading")]
    public CardLoader cardLoader;

    [Header("Player UI")]
    public TextMeshProUGUI currentPlayerText;

    [Header("Card UI")]
    public Image cardBackground;
    public TextMeshProUGUI cardTitle;
    public TextMeshProUGUI cardText;
    public Image cardImage;

    [Header("Navigation")]
    public Button backToMenuButton;

    [Header("Game Settings")]
    [Range(20, 100)]
    public int minCardsPerRound = 30;
    [Range(20, 100)]
    public int maxCardsPerRound = 50;

    [Header("Card Type Mix (share of round, 0-1)")]
    [Range(0f, 0.5f)]
    public float ruleCardShare = 0.05f;
    [Range(0f, 0.5f)]
    public float gameCardShare = 0.15f;
    [Range(0f, 0.5f)]
    public float specialCardShare = 0.32f;

    [Header("Group Rarity")]
    [Range(1, 8)]
    public int maxWoPGroups = 6;
    [Range(1, 6)]
    public int maxPantomimeGroups = 5;
    [Range(4, 12)]
    public int minGapBetweenGroups = 6;

    [Header("Rule Duration")]
    // Used only as a fallback if a Regel card has no duration set in cards.json.
    [Range(1, 10)]
    public int ruleDurationRounds = 3;

    private List<Player> players;
    private int currentPlayerIndex = 0;
    private List<CardData> shuffledDeck = new List<CardData>();
    private int currentCardIndex = 0;
    private int cardsPlayedThisRound = 0;
    private int targetCardsForRound;
    private bool gameBlocked = false;
    private bool gameEnded = false;
    private bool isLastCardOfRound = false;
    private GameModeEnum currentGameMode;
    private Image titleBackdrop;
    private VerticalAlignmentOptions defaultTitleVerticalAlignment;
    private bool capturedDefaultTitleAlignment = false;

    private class ActiveRule
    {
        public string text;
        public int turnsRemaining;
    }

    // Multiple rules can be active at once - each tracked with its own countdown.
    private List<ActiveRule> activeRules = new List<ActiveRule>();
    private Queue<string> ruleEndAnnouncements = new Queue<string>();

    void Start()
    {
        players = PlayerManager.instance.GetPlayers();

        if (players == null || players.Count == 0)
        {
            BackToMenu();
            return;
        }

        currentGameMode = GameModeManager.instance.getGameMode();

        targetCardsForRound = Random.Range(minCardsPerRound, maxCardsPerRound + 1);

        LoadCards();
        SetupUI();
        UpdateCurrentPlayer();

        cardsPlayedThisRound = 0;
        gameBlocked = false;
        gameEnded = false;

        DrawCard();
    }

    void LoadCards()
    {
        if (cardLoader == null) return;

        cardLoader.LoadCardData();

        var allCards = cardLoader.GetAllCards();
        var specialWoPCards = cardLoader.GetSpezialWoPCards();
        var specialPantomimeCards = cardLoader.GetSpezialPantomimeCards();

        var wopCards = new List<CardData>();
        var pantomimeCards = new List<CardData>();

        foreach (var card in allCards)
        {
            if (card.cardType == CardType.WahrheitOderPflicht)
                wopCards.Add(CreateCardCopy(card));
            else if (card.cardType == CardType.Pantomime)
                pantomimeCards.Add(CreateCardCopy(card));
        }

        ShuffleList(wopCards);
        ShuffleList(pantomimeCards);

        int requiredCards = targetCardsForRound + 4; // small buffer

        // Card type counts scale with the round length instead of being fixed
        // absolute numbers, so the mix stays consistent regardless of round length.
        int ruleCount = Mathf.RoundToInt(requiredCards * ruleCardShare);
        int gameCount = Mathf.RoundToInt(requiredCards * gameCardShare);
        int specialCount = Mathf.RoundToInt(requiredCards * specialCardShare);
        int easyCount = Mathf.Max(0, requiredCards - ruleCount - gameCount - specialCount);

        var nonSpecialCards = new List<CardData>();
        nonSpecialCards.AddRange(BuildCardTypeSet(allCards, CardType.Regel, ruleCount, allowDuplicates: false));
        nonSpecialCards.AddRange(BuildCardTypeSet(allCards, CardType.Spiel, gameCount, allowDuplicates: false));
        // Special cards are rare in the pool - duplicates are allowed so the target count can exceed supply.
        nonSpecialCards.AddRange(BuildCardTypeSet(allCards, CardType.Spezial, specialCount, allowDuplicates: true));
        nonSpecialCards.AddRange(BuildCardTypeSet(allCards, CardType.Einfach, easyCount, allowDuplicates: false));
        ShuffleList(nonSpecialCards);

        shuffledDeck = new List<CardData>(nonSpecialCards);

        // Group placement: WoP and Pantomime cards are inserted as small clusters
        // at randomized, spaced-out positions instead of being mixed in uniformly.
        int usableRange = Mathf.Max(0, Mathf.Min(shuffledDeck.Count - 15, requiredCards));
        int possibleWoPSlots = usableRange / (minGapBetweenGroups + 3); // +3 for group size
        int possiblePantomimeSlots = usableRange / (minGapBetweenGroups + 2); // +2 for group size

        int maxPossibleWoP = Mathf.Min(maxWoPGroups, wopCards.Count / 2, possibleWoPSlots);
        int maxPossiblePantomime = Mathf.Min(maxPantomimeGroups, pantomimeCards.Count, possiblePantomimeSlots);

        // Weighted randomness - leans towards the higher end of the possible range
        int actualWoPGroups = maxPossibleWoP > 0 ? Random.Range(Mathf.Max(1, maxPossibleWoP / 2), maxPossibleWoP + 1) : 0;
        int actualPantomimeGroups = maxPossiblePantomime > 0 ? Random.Range(Mathf.Max(1, maxPossiblePantomime / 2), maxPossiblePantomime + 1) : 0;

        // Both group types draw from the same pool of random positions with retries,
        // so neither type systematically loses out to position collisions with the other.
        List<int> wopPositions = PlaceGroupPositions(actualWoPGroups, usableRange, new List<int>());
        List<int> pantomimePositions = PlaceGroupPositions(actualPantomimeGroups, usableRange, wopPositions);

        // Combine and shuffle insertion order so groups don't always appear WoP-first
        List<(int pos, string type, int index)> allGroups = new List<(int, string, int)>();

        for (int i = 0; i < wopPositions.Count; i++)
            allGroups.Add((wopPositions[i], "wop", i));

        for (int i = 0; i < pantomimePositions.Count; i++)
            allGroups.Add((pantomimePositions[i], "pantomime", i));

        // Sort by position descending so inserting doesn't shift earlier positions
        allGroups.Sort((a, b) => b.pos.CompareTo(a.pos));

        foreach (var group in allGroups)
        {
            if (group.type == "wop")
            {
                int wopCount = Mathf.Min(Random.Range(1, 3), wopCards.Count - group.index * 2);
                for (int j = wopCount - 1; j >= 0; j--)
                {
                    int cardIndex = group.index * 2 + j;
                    if (cardIndex < wopCards.Count)
                        shuffledDeck.Insert(group.pos, wopCards[cardIndex]);
                }

                if (specialWoPCards.Count > 0)
                    shuffledDeck.Insert(group.pos, CreateCardCopy(specialWoPCards[Random.Range(0, specialWoPCards.Count)]));
            }
            else // pantomime
            {
                if (group.index < pantomimeCards.Count)
                    shuffledDeck.Insert(group.pos, pantomimeCards[group.index]);

                if (specialPantomimeCards.Count > 0)
                    shuffledDeck.Insert(group.pos, CreateCardCopy(specialPantomimeCards[Random.Range(0, specialPantomimeCards.Count)]));
            }
        }

        ValidateFinalDeck();
    }

    void ValidateFinalDeck()
    {
        int requiredCards = targetCardsForRound + 4; // small buffer

        PadDeckWithEasyCards(requiredCards);

        // Remove groups from the last few cards so a round doesn't end mid-group
        RemoveGroupsFromEnd();

        // RemoveGroupsFromEnd can drop the deck below target again - pad once more
        PadDeckWithEasyCards(requiredCards);
    }

    void PadDeckWithEasyCards(int requiredCards)
    {
        if (shuffledDeck.Count >= requiredCards) return;

        Debug.LogWarning($"Deck too small! Required: {requiredCards}, available: {shuffledDeck.Count}");

        // Only fill with "Einfach" cards - filling with any type would undo the
        // fixed card type mix chosen in LoadCards().
        var easyCards = new List<CardData>();
        foreach (var card in cardLoader.GetAllCards())
        {
            if (card.cardType == CardType.Einfach)
                easyCards.Add(card);
        }

        while (shuffledDeck.Count < requiredCards && easyCards.Count > 0)
        {
            var randomCard = easyCards[Random.Range(0, easyCards.Count)];
            shuffledDeck.Add(CreateCardCopy(randomCard));
        }
    }

    void RemoveGroupsFromEnd()
    {
        if (shuffledDeck.Count <= 8) return;

        int safeZoneStart = shuffledDeck.Count - 8;

        for (int i = shuffledDeck.Count - 1; i >= safeZoneStart; i--)
        {
            var cardType = shuffledDeck[i].cardType;

            if (cardType == CardType.spezial_WoP ||
                cardType == CardType.spezial_Pantomime ||
                cardType == CardType.WahrheitOderPflicht ||
                cardType == CardType.Pantomime)
            {
                shuffledDeck.RemoveAt(i);
            }
        }
    }

    // Picks targetCount random positions within [15, usableRange) that stay at least
    // minGapBetweenGroups apart from each other and from existingPositions, retrying
    // on collisions instead of silently dropping the slot.
    List<int> PlaceGroupPositions(int targetCount, int usableRange, List<int> existingPositions)
    {
        var positions = new List<int>();
        int minStart = 15;
        if (minStart >= usableRange) return positions;

        int maxAttempts = targetCount * 30;
        int attempts = 0;

        while (positions.Count < targetCount && attempts < maxAttempts)
        {
            attempts++;
            int pos = Random.Range(minStart, usableRange);

            bool tooClose = false;
            foreach (int existing in existingPositions)
            {
                if (Mathf.Abs(pos - existing) < minGapBetweenGroups) { tooClose = true; break; }
            }
            if (!tooClose)
            {
                foreach (int placed in positions)
                {
                    if (Mathf.Abs(pos - placed) < minGapBetweenGroups) { tooClose = true; break; }
                }
            }

            if (!tooClose) positions.Add(pos);
        }

        return positions;
    }

    // Returns up to targetCount fresh copies of the given card type from sourceCards.
    // If allowDuplicates is true and supply is short, existing copies get duplicated to reach targetCount.
    List<CardData> BuildCardTypeSet(List<CardData> sourceCards, CardType type, int targetCount, bool allowDuplicates)
    {
        var matches = new List<CardData>();
        foreach (var card in sourceCards)
        {
            if (card.cardType == type) matches.Add(CreateCardCopy(card));
        }

        ShuffleList(matches);

        if (matches.Count > targetCount)
        {
            matches.RemoveRange(targetCount, matches.Count - targetCount);
        }
        else if (allowDuplicates)
        {
            while (matches.Count < targetCount && matches.Count > 0)
            {
                matches.Add(CreateCardCopy(matches[Random.Range(0, matches.Count)]));
            }
        }

        return matches;
    }

    CardData CreateCardCopy(CardData original)
    {
        var copy = new CardData(original.cardTitle, original.cardText, original.cardType, original.hasImage, original.hasText, original.duration, original.imageName);
        copy.cardImage = original.cardImage;
        return copy;
    }

    void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
        }
    }

    void ShuffleDeck()
    {
        for (int i = shuffledDeck.Count - 1; i > 0; i--)
        {
            int randomIndex = Random.Range(0, i + 1);
            CardData temp = shuffledDeck[i];
            shuffledDeck[i] = shuffledDeck[randomIndex];
            shuffledDeck[randomIndex] = temp;
        }
        currentCardIndex = 0;
    }

    void SetupUI()
    {
        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.AddListener(BackToMenu);
        }
        else
        {
            Debug.LogWarning("BackToMenuButton not assigned!");
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            OnScreenTouch();
        }
    }

    public void OnScreenTouch()
    {
        if (gameEnded)
        {
            OnEndScreenTouch();
            return;
        }

        if (gameBlocked) return;

        // The last card of the round was already shown - only now show the end screen
        if (isLastCardOfRound)
        {
            ShowEndScreen();
            gameBlocked = true;
            gameEnded = true;
            return;
        }

        NextPlayer();

        if (ruleEndAnnouncements.Count > 0)
        {
            DisplayRuleEndAnnouncement(ruleEndAnnouncements.Dequeue());
            return;
        }

        DrawCard();
    }

    void DisplayRuleEndAnnouncement(string ruleText)
    {
        if (cardBackground != null)
        {
            cardBackground.gameObject.SetActive(true);
            cardBackground.color = new Color(0.6f, 0.6f, 0.6f);
        }
        if (cardImage != null) cardImage.gameObject.SetActive(false);
        SetTitleBackdropVisible(false);

        if (cardText != null)
        {
            cardText.gameObject.SetActive(true);
            cardText.color = Color.white;
            cardText.text = $"Diese Regel ist jetzt vorbei:\n\n{ruleText}";
        }
        cardTitle.color = Color.white;
        cardTitle.text = "REGEL VORBEI";
    }

    void UpdateCurrentPlayer()
    {
        if (players.Count == 0) return;

        Player currentPlayer = players[currentPlayerIndex];
        if (currentPlayerText != null)
        {
            currentPlayerText.text = $"{currentPlayer.name}";
        }
    }

    void DrawCard()
    {
        CardData card = shuffledDeck[currentCardIndex];
        currentCardIndex++;
        if (currentCardIndex >= shuffledDeck.Count)
        {
            ShuffleDeck(); // resets currentCardIndex back to 0
        }

        cardsPlayedThisRound++;

        if (card.cardType == CardType.Regel)
        {
            int duration = card.duration > 0 ? card.duration : ruleDurationRounds;
            // +1 so the "rule over" announcement appears as its own slot after the
            // last regular card of the duration, instead of replacing that last card.
            activeRules.Add(new ActiveRule
            {
                text = ReplacePlayerPlaceholders(card.cardText),
                turnsRemaining = duration * players.Count + 1
            });
        }

        if (currentPlayerText != null)
        {
            currentPlayerText.color = Color.white;
            currentPlayerText.fontStyle = TMPro.FontStyles.Normal;
            currentPlayerText.fontSize = 46;
        }
        cardTitle.color = Color.white;
        cardText.color = Color.white;

        // Always show the card - the end screen only follows on the next touch
        DisplayCard(card);
        isLastCardOfRound = cardsPlayedThisRound >= targetCardsForRound;
    }

    void DisplayCard(CardData card)
    {
        string displayText = ReplacePlayerPlaceholders(card.cardText);
        string titleText = card.cardTitle;

        if (cardTitle != null && !capturedDefaultTitleAlignment)
        {
            defaultTitleVerticalAlignment = cardTitle.verticalAlignment;
            capturedDefaultTitleAlignment = true;
        }

        if (cardBackground != null)
        {
            cardBackground.gameObject.SetActive(true);
            cardBackground.color = GetCardColor(card.cardType);
        }

        if (card.cardType == CardType.WahrheitOderPflicht)
        {
            if (currentPlayerText != null) currentPlayerText.color = Color.black;
            cardTitle.color = Color.black;
            cardText.color = Color.black;
        }

        if (card.hasImage && card.cardImage != null)
        {
            if (cardText != null) cardText.gameObject.SetActive(false);
            if (cardImage != null)
            {
                cardImage.gameObject.SetActive(true);
                cardImage.sprite = card.cardImage;

                if (currentPlayerText != null)
                {
                    if (card.imageName == "wahrheit_oder_pflicht")
                    {
                        currentPlayerText.text = "";
                    }
                    else
                    {
                        currentPlayerText.color = Color.black;
                        currentPlayerText.fontStyle = TMPro.FontStyles.Bold;
                        currentPlayerText.fontSize = 50;
                    }
                }
            }

            // cardText is hidden behind the image. If the card's raw text references
            // the current player, show just their name (not the full card text) over
            // the image via the title, with a backdrop so it stays readable on any photo.
            bool showsCurrentPlayer = card.cardText.Contains("{CURRENT_PLAYER}");
            if (cardTitle != null)
            {
                if (showsCurrentPlayer)
                {
                    cardTitle.text = players[currentPlayerIndex].name;
                    cardTitle.color = Color.black;
                    cardTitle.verticalAlignment = VerticalAlignmentOptions.Top;
                    SetTitleBackdropVisible(true);
                }
                else
                {
                    cardTitle.text = "";
                    cardTitle.verticalAlignment = defaultTitleVerticalAlignment;
                    SetTitleBackdropVisible(false);
                }
            }
        }
        else
        {
            if (cardText != null)
            {
                cardText.gameObject.SetActive(true);
                cardText.text = displayText;
                cardTitle.text = titleText;
                cardTitle.verticalAlignment = defaultTitleVerticalAlignment;
            }
            if (cardImage != null) cardImage.gameObject.SetActive(false);
            SetTitleBackdropVisible(false);
        }
    }

    private Sprite roundedBackdropSprite;

    Sprite GetRoundedBackdropSprite()
    {
        if (roundedBackdropSprite != null) return roundedBackdropSprite;

        const int size = 64;
        const int radius = 20;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = -1f, cy = -1f;
                if (x < radius && y < radius) { cx = radius; cy = radius; }
                else if (x >= size - radius && y < radius) { cx = size - radius - 1; cy = radius; }
                else if (x < radius && y >= size - radius) { cx = radius; cy = size - radius - 1; }
                else if (x >= size - radius && y >= size - radius) { cx = size - radius - 1; cy = size - radius - 1; }

                float alpha = 1f;
                if (cx >= 0f)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    alpha = Mathf.Clamp01(radius - dist + 0.5f);
                }

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        roundedBackdropSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return roundedBackdropSprite;
    }

    void EnsureTitleBackdrop()
    {
        if (titleBackdrop != null || cardTitle == null) return;

        GameObject backdropObj = new GameObject("TitleBackdrop", typeof(RectTransform));
        backdropObj.transform.SetParent(cardTitle.transform.parent, false);

        titleBackdrop = backdropObj.AddComponent<Image>();
        titleBackdrop.sprite = GetRoundedBackdropSprite();
        titleBackdrop.type = Image.Type.Sliced;
        titleBackdrop.color = new Color(1f, 1f, 1f, 0.8f);

        RectTransform backdropRect = backdropObj.GetComponent<RectTransform>();
        RectTransform titleRect = cardTitle.rectTransform;
        backdropRect.anchorMin = titleRect.anchorMin;
        backdropRect.anchorMax = titleRect.anchorMax;
        backdropRect.pivot = titleRect.pivot;
        backdropRect.anchoredPosition = titleRect.anchoredPosition;

        backdropRect.SetSiblingIndex(cardTitle.transform.GetSiblingIndex());
    }

    void UpdateTitleBackdropLayout()
    {
        if (titleBackdrop == null || cardTitle == null) return;

        // The title field is sized for long titles, but the rendered text (especially
        // a short name) can sit anywhere within it depending on alignment - so measure
        // the actual rendered glyph bounds rather than assuming it's centered in the field.
        cardTitle.ForceMeshUpdate();
        Bounds b = cardTitle.textBounds;

        RectTransform backdropRect = titleBackdrop.rectTransform;
        backdropRect.sizeDelta = new Vector2(b.size.x, b.size.y) + new Vector2(28f, 12f);
        backdropRect.anchoredPosition = cardTitle.rectTransform.anchoredPosition + new Vector2(b.center.x, b.center.y);
    }

    void SetTitleBackdropVisible(bool visible)
    {
        if (visible)
        {
            EnsureTitleBackdrop();
            UpdateTitleBackdropLayout();
        }
        if (titleBackdrop != null) titleBackdrop.gameObject.SetActive(visible);
    }

    void HideCard()
    {
        if (cardBackground != null) cardBackground.gameObject.SetActive(false);
        if (cardText != null) cardText.gameObject.SetActive(false);
        if (cardImage != null) cardImage.gameObject.SetActive(false);
        SetTitleBackdropVisible(false);
    }

    string ReplacePlayerPlaceholders(string text)
    {
        text = text.Replace("{CURRENT_PLAYER}", players[currentPlayerIndex].name);
        text = text.Replace("{NEXT_PLAYER}", GetNextPlayer().name);
        text = text.Replace("{OTHER_PLAYER}", GetRandomOtherPlayer().name);
        text = text.Replace("{PLAYER}", GetRandomPlayer().name);
        return text;
    }

    Player GetRandomPlayer() => players[Random.Range(0, players.Count)];

    Player GetNextPlayer()
    {
        int nextIndex = (currentPlayerIndex + 1) % players.Count;
        return players[nextIndex];
    }

    Player GetRandomOtherPlayer()
    {
        if (players.Count <= 1) return players[0];
        int randomIndex;
        do { randomIndex = Random.Range(0, players.Count); }
        while (randomIndex == currentPlayerIndex);
        return players[randomIndex];
    }

    Color GetCardColor(CardType type)
    {
        switch (type)
        {
            case CardType.Einfach: return new Color(0.51f, 0.75f, 0.8f);
            case CardType.Spiel: return new Color(0.28f, 0.62f, 0.71f);
            case CardType.Regel: return new Color(1f, 0.65f, 0.17f);
            case CardType.Spezial: return new Color(0.8f, 0.3f, 0.8f);
            case CardType.spezial_WoP: return new Color(0.8f, 0.3f, 0.8f);
            case CardType.spezial_Pantomime: return new Color(0.28f, 0.62f, 0.71f);
            case CardType.WahrheitOderPflicht: return new Color(0.929f, 0.906f, 0.890f);
            case CardType.Pantomime: return new Color(0.28f, 0.62f, 0.71f);
            default: return Color.white;
        }
    }

    void NextPlayer()
    {
        currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        UpdateCurrentPlayer();

        // Each tap is one player's turn, regardless of which player started the rule.
        for (int i = activeRules.Count - 1; i >= 0; i--)
        {
            activeRules[i].turnsRemaining--;
            if (activeRules[i].turnsRemaining <= 0)
            {
                ruleEndAnnouncements.Enqueue(activeRules[i].text);
                activeRules.RemoveAt(i);
            }
        }
    }

    void ShowEndScreen()
    {
        if (currentPlayerText != null) currentPlayerText.gameObject.SetActive(false);
        HideCard();

        if (cardImage != null)
        {
            cardImage.gameObject.SetActive(true);
            cardImage.sprite = Resources.Load<Sprite>("CardImages/end_screen");
        }
    }

    public void OnEndScreenTouch()
    {
        BackToMenu();
    }

    public void BackToMenu()
    {
        NavigationSceneManager.instance.LoadScene("MainMenu");
    }
}
