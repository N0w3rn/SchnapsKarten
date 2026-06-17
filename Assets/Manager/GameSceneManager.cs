// GameSceneManager.cs - Mit Pantomime-Gruppen
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

    [Header("Group Rarity")]
    [Range(1, 8)]
    public int maxWoPGroups = 5;
    [Range(1, 6)] 
    public int maxPantomimeGroups = 4;
    [Range(4, 12)]
    public int minGapBetweenGroups = 6;
    [Range(2, 40)]
    public int maxRuleCards = 5;
    [Range(6, 60)]
    public int maxGameCards = 12;
    
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

        var normalCards = cardLoader.GetAllCards();
        var spezialWoPCards = cardLoader.GetSpezialWoPCards();
        var spezialPantomimeCards = cardLoader.GetSpezialPantomimeCards();

        var wopCards = new List<CardData>();
        var pantomimeCards = new List<CardData>();
        var nonSpecialCards = new List<CardData>();

        // Erstelle Kopien und trenne verschiedene Kartentypen
        foreach (var card in normalCards)
        {
            var copy = CreateCardCopy(card);

            if (card.cardType == CardType.WahrheitOderPflicht)
                wopCards.Add(copy);
            else if (card.cardType == CardType.Pantomime)
                pantomimeCards.Add(copy);
            else
                nonSpecialCards.Add(copy);
        }
        
        // Mische alle Listen
        ShuffleList(wopCards);
        ShuffleList(pantomimeCards);
        ShuffleList(nonSpecialCards);

        // RULE CARDS LIMITATION
        var ruleCards = new List<CardData>();
        foreach (var card in nonSpecialCards)
        {
            if (card.cardType == CardType.Regel)
                ruleCards.Add(card);
        }

        int targetRuleCount = Random.Range(maxRuleCards / 2, maxRuleCards + 1);
        if (ruleCards.Count > targetRuleCount)
        {
            ShuffleList(ruleCards);
            // Remove excess rule cards
            for (int i = ruleCards.Count - 1; i >= targetRuleCount; i--)
            {
                nonSpecialCards.Remove(ruleCards[i]);
            }
        }

        // GAME CARDS LIMITATION
        var gameCards = new List<CardData>();
        foreach (var card in nonSpecialCards)
        {
            if (card.cardType == CardType.Spiel)
                gameCards.Add(card);
        }

        int targetGameCount = Random.Range(maxGameCards / 2, maxGameCards + 1);
        if (gameCards.Count > targetGameCount)
        {
            ShuffleList(gameCards);
            // Remove excess game cards
            for (int i = gameCards.Count - 1; i >= targetGameCount; i--)
            {
                nonSpecialCards.Remove(gameCards[i]);
            }
        }
        
        int requiredCards = targetCardsForRound + 4; // +4 Puffer
        
        // EINFACHER ANSATZ: Erstelle das Deck step by step
        shuffledDeck = new List<CardData>();
        
        // 1. Füge ALLE normalen Karten hinzu
        shuffledDeck.AddRange(nonSpecialCards);
        
        // 2. VIEL EINFACHERE GRUPPEN-PLATZIERUNG
        List<int> wopPositions = new List<int>();
        List<int> pantomimePositions = new List<int>();
        
        // Berechne wie oft eine Gruppe in den ersten X Karten passen würde
        int usableRange = Mathf.Max(0, Mathf.Min(shuffledDeck.Count - 15, requiredCards));
        int possibleWoPSlots = usableRange / (minGapBetweenGroups + 3); // +3 für Gruppengröße
        int possiblePantomimeSlots = usableRange / (minGapBetweenGroups + 2); // +2 für Gruppengröße
        
        // ZUFÄLLIGE Anzahl von Gruppen (0 bis maximal möglich)
        int maxPossibleWoP = Mathf.Min(maxWoPGroups, wopCards.Count / 2, possibleWoPSlots);
        int maxPossiblePantomime = Mathf.Min(maxPantomimeGroups, pantomimeCards.Count, possiblePantomimeSlots);

        // Gewichtete Zufälligkeit - bevorzugt höhere Zahlen
        int actualWoPGroups = Random.Range(maxPossibleWoP / 2, maxPossibleWoP + 1);
        int actualPantomimeGroups = Random.Range(maxPossiblePantomime / 2, maxPossiblePantomime + 1);

        // Erstelle WoP-Positionen
        for (int i = 0; i < actualWoPGroups; i++)
        {
            int minPos = 15 + i * (minGapBetweenGroups + 5);
            int maxPos = minPos + minGapBetweenGroups;
            maxPos = Mathf.Min(maxPos, usableRange);
            
            if (minPos < maxPos)
            {
                int pos = Random.Range(minPos, maxPos);
                wopPositions.Add(pos);
            }
        }
        
        // Erstelle Pantomime-Positionen (versetzt zu WoP)
        for (int i = 0; i < actualPantomimeGroups; i++)
        {
            int minPos = 20 + i * (minGapBetweenGroups + 5);
            int maxPos = minPos + minGapBetweenGroups;
            maxPos = Mathf.Min(maxPos, usableRange);
            
            if (minPos < maxPos)
            {
                int pos = Random.Range(minPos, maxPos);
                
                // Prüfe Abstand zu WoP-Gruppen
                bool tooClose = false;
                foreach (int wopPos in wopPositions)
                {
                    if (Mathf.Abs(pos - wopPos) < minGapBetweenGroups)
                    {
                        tooClose = true;
                        break;
                    }
                }
                
                if (!tooClose)
                    pantomimePositions.Add(pos);
            }
        }
        
        // MISCHE ALLE POSITIONEN ZUSAMMEN FÜR ECHTE ZUFÄLLIGKEIT!
        List<(int pos, string type, int index)> allGroups = new List<(int, string, int)>();
        
        for (int i = 0; i < wopPositions.Count; i++)
            allGroups.Add((wopPositions[i], "wop", i));
        
        for (int i = 0; i < pantomimePositions.Count; i++)
            allGroups.Add((pantomimePositions[i], "pantomime", i));
        
        // Sortiere nach Position rückwärts für korrektes Einfügen
        allGroups.Sort((a, b) => b.pos.CompareTo(a.pos));
        
        // Füge alle Gruppen in zufälliger Reihenfolge ein
        foreach (var group in allGroups)
        {
            if (group.type == "wop")
            {
                // Füge WoP-Gruppe ein
                int wopCount = Mathf.Min(Random.Range(1, 3), wopCards.Count - group.index * 2);
                for (int j = wopCount - 1; j >= 0; j--)
                {
                    int cardIndex = group.index * 2 + j;
                    if (cardIndex < wopCards.Count)
                        shuffledDeck.Insert(group.pos, wopCards[cardIndex]);
                }
                
                // Spezial-WoP DAVOR (ZUFÄLLIGE Auswahl!)
                if (spezialWoPCards.Count > 0)
                    shuffledDeck.Insert(group.pos, CreateCardCopy(spezialWoPCards[Random.Range(0, spezialWoPCards.Count)]));
            }
            else // pantomime
            {
                // Füge Pantomime-Gruppe ein
                if (group.index < pantomimeCards.Count)
                    shuffledDeck.Insert(group.pos, pantomimeCards[group.index]);
                
                // Spezial-Pantomime DAVOR (ZUFÄLLIGE Auswahl!)
                if (spezialPantomimeCards.Count > 0)
                    shuffledDeck.Insert(group.pos, CreateCardCopy(spezialPantomimeCards[Random.Range(0, spezialPantomimeCards.Count)]));
            }
        }
        
        // 5. Prüfe finale Deck-Größe und fülle auf falls nötig
        ValidateFinalDeck();
    }

    void ValidateFinalDeck()
    {
        int requiredCards = targetCardsForRound + 4; // +4 Puffer

        PadDeckWithNormalCards(requiredCards);

        // Entferne Gruppen aus dem Endbereich (letzten 8 Karten)
        RemoveGroupsFromEnd();

        // RemoveGroupsFromEnd kann das Deck wieder unter die Zielgröße bringen - erneut auffüllen
        PadDeckWithNormalCards(requiredCards);
    }

    void PadDeckWithNormalCards(int requiredCards)
    {
        if (shuffledDeck.Count >= requiredCards) return;

        Debug.LogWarning($"Deck zu klein! Benötigt: {requiredCards}, Vorhanden: {shuffledDeck.Count}");

        // Füge normale Karten hinzu falls verfügbar (OHNE LINQ)
        var allCards = cardLoader.GetAllCards();
        var normalCards = new List<CardData>();

        foreach (var card in allCards)
        {
            if (card.cardType != CardType.WahrheitOderPflicht &&
                card.cardType != CardType.Pantomime &&
                card.cardType != CardType.spezial_WoP &&
                card.cardType != CardType.spezial_Pantomime)
            {
                normalCards.Add(card);
            }
        }

        while (shuffledDeck.Count < requiredCards && normalCards.Count > 0)
        {
            var randomCard = normalCards[Random.Range(0, normalCards.Count)];
            shuffledDeck.Add(CreateCardCopy(randomCard));
        }
    }

    void RemoveGroupsFromEnd()
    {
        if (shuffledDeck.Count <= 8) return;
        
        int safeZoneStart = shuffledDeck.Count - 8;
        
        // Rückwärts durchgehen und Gruppen-Karten entfernen
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
            Debug.LogWarning("BackToMenuButton nicht zugewiesen!");
        }
    }
    
    void Update()
    {
        // Touch/Click Detection
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            OnScreenTouch();
        }
    }
    
    public void OnScreenTouch()
    {
        // Check: Sind wir im End-Screen?
        if (gameEnded)
        {
            OnEndScreenTouch();
            return;
        }

        // Wenn blockiert, nichts machen
        if (gameBlocked) return;

        // Letzte Karte wurde bereits gezeigt - jetzt erst den End-Screen anzeigen
        if (isLastCardOfRound)
        {
            ShowEndScreen();
            gameBlocked = true;
            gameEnded = true;
            return;
        }

        // Normale Karte: Nächster Spieler + neue Karte
        NextPlayer();
        DrawCard();
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
            ShuffleDeck(); // setzt currentCardIndex zurück auf 0
        }

        cardsPlayedThisRound++;

        currentPlayerText.color = Color.white;
        cardTitle.color = Color.white;
        cardText.color = Color.white;
        currentPlayerText.fontStyle = TMPro.FontStyles.Normal;
        currentPlayerText.fontSize = 46;

        // Karte immer anzeigen - der End-Screen folgt erst beim nächsten Touch
        DisplayCard(card);
        isLastCardOfRound = cardsPlayedThisRound >= targetCardsForRound;
    }
    
    void DisplayCard(CardData card)
    {
        string displayText = ReplacePlayerPlaceholders(card.cardText);
        string titleText = card.cardTitle;
        
        if (cardBackground != null)
        {
            cardBackground.gameObject.SetActive(true);
            cardBackground.color = GetCardColor(card.cardType);
        }

        if (card.cardType == CardType.WahrheitOderPflicht)
        {
            currentPlayerText.color = Color.black;
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

                if(card.imageName == "wahrheit_oder_pflicht")
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
        else
        {
            if (cardText != null)
            {
                cardText.gameObject.SetActive(true);
                cardText.text = displayText;
                cardTitle.text = titleText;
            }
            if (cardImage != null) cardImage.gameObject.SetActive(false);
        }
    }
    
    void HideCard()
    {
        if (cardBackground != null) cardBackground.gameObject.SetActive(false);
        if (cardText != null) cardText.gameObject.SetActive(false);
        if (cardImage != null) cardImage.gameObject.SetActive(false);
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
    }
    
    void ShowEndScreen()
    {
        currentPlayerText.gameObject.SetActive(false);
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