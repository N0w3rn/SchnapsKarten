// CardData.cs - Erweitert
using UnityEngine;

[System.Serializable]
public class CardData
{
    public string cardTitle;
    public string cardText;
    public string imageName;
    public Sprite cardImage;
    public CardType cardType;
    public bool hasImage;
    public bool hasText;
    public int duration;      // duration for rule cards (in minutes)
    
    public CardData(string title, string text, CardType type, bool hasImg = false, bool hasTxt = true, int dur = 0, string imgName = "")
    {
        cardTitle = title;
        cardText = text;
        cardType = type;
        hasImage = hasImg;
        hasText = hasTxt;
        duration = dur;
        cardImage = null;
        imageName = imgName;
    }
}

[System.Serializable]
public class Player
{
    public string name;
    public Vector2 position;
    public bool isActive;
    
    public Player(string playerName)
    {
        name = playerName;
        isActive = false;
        position = Vector2.zero;
    }
}