using UnityEngine;
using System.Collections.Generic;
using System;

[System.Serializable]
public class CardDataJSON
{
    public string title = "";
    public string text;
    public string type = "einfach";
    public bool hasImage = false;
    public string imageName = "";
    public int duration = 0;
}

[System.Serializable]
public class GameDataJSON
{
    public List<CardDataJSON> cards = new List<CardDataJSON>();
}

public class CardLoader : MonoBehaviour
{
    [Header("JSON File")]
    public TextAsset cardDataFile;
    
    private GameDataJSON gameData;
    
    public void LoadCardData()
    {
        if (cardDataFile == null)
        {
            Debug.LogError("Keine JSON-Datei zugewiesen!");
            return;
        }
        
        try
        {
            string jsonString = cardDataFile.text;
            gameData = JsonUtility.FromJson<GameDataJSON>(jsonString);
            
        }
        catch (Exception e)
        {
            Debug.LogError($"Fehler beim Laden der JSON: {e.Message}");
        }
    }
    
    public List<CardData> GetAllCards()
    {
        List<CardData> cards = new List<CardData>();
        
        if (gameData?.cards == null) return cards;
        
        foreach (CardDataJSON cardJSON in gameData.cards)
        {
            CardType type = ConvertToCardType(cardJSON.type);

            if (type == CardType.spezial_WoP || type == CardType.spezial_Pantomime)
                continue;

            CardData card = new CardData(cardJSON.title, cardJSON.text, type, cardJSON.hasImage, true, cardJSON.duration, cardJSON.imageName);

            if (cardJSON.hasImage && !string.IsNullOrEmpty(cardJSON.imageName))
            {
                card.cardImage = LoadSpriteFromResources(cardJSON.imageName);
            }
            
            cards.Add(card);
        }
        
        return cards;
    }
    
    public List<CardData> GetSpezialWoPCards()
    {
        List<CardData> spezialWoPCards = new List<CardData>();
        
        if (gameData?.cards == null) return spezialWoPCards;
        
        foreach (CardDataJSON cardJSON in gameData.cards)
        {
            CardType type = ConvertToCardType(cardJSON.type);
            
            if (type == CardType.spezial_WoP)
            {
                CardData card = new CardData(cardJSON.title, cardJSON.text, type, cardJSON.hasImage, true, cardJSON.duration, cardJSON.imageName);

                if (cardJSON.hasImage && !string.IsNullOrEmpty(cardJSON.imageName))
                {
                    card.cardImage = LoadSpriteFromResources(cardJSON.imageName);
                }
                
                spezialWoPCards.Add(card);
            }
        }
        
        return spezialWoPCards;
    }
    
    public List<CardData> GetSpezialPantomimeCards()
    {
        List<CardData> spezialPantomimeCards = new List<CardData>();
        
        if (gameData?.cards == null) return spezialPantomimeCards;
        
        foreach (CardDataJSON cardJSON in gameData.cards)
        {
            CardType type = ConvertToCardType(cardJSON.type);
            
            if (type == CardType.spezial_Pantomime)
            {
                CardData card = new CardData(cardJSON.title, cardJSON.text, type, cardJSON.hasImage, true, cardJSON.duration, cardJSON.imageName);

                if (cardJSON.hasImage && !string.IsNullOrEmpty(cardJSON.imageName))
                {
                    card.cardImage = LoadSpriteFromResources(cardJSON.imageName);
                }
                
                spezialPantomimeCards.Add(card);
            }
        }
        
        return spezialPantomimeCards;
    }
    
    CardType ConvertToCardType(string typeString)
    {
        switch ((typeString ?? "einfach").ToLower())
        {
            case "einfach": return CardType.Einfach;
            case "spiel": return CardType.Spiel;
            case "regel": return CardType.Regel;
            case "spezial": return CardType.Spezial;
            case "wahrheit_oder_pflicht": return CardType.WahrheitOderPflicht;
            case "spezial_wop": return CardType.spezial_WoP;
            case "spezial_pantomime": return CardType.spezial_Pantomime;
            case "pantomime": return CardType.Pantomime;
            default: 
                Debug.LogWarning($"Unbekannter Kartentyp: '{typeString}' - verwende Einfach als Standard");
                return CardType.Einfach;
        }
    }
    
    Sprite LoadSpriteFromResources(string spriteName)
    {
        Sprite sprite = Resources.Load<Sprite>($"CardImages/{spriteName}");
        
        if (sprite == null)
        {
            Debug.LogWarning($"Sprite '{spriteName}' nicht in Resources/CardImages/ gefunden");
        }
        
        return sprite;
    }
}