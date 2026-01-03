// PlayerManager.cs - Singleton für Spieler-Management
using UnityEngine;
using System.Collections.Generic;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager instance;
    
    private List<Player> players = new List<Player>();
    private int currentPlayerIndex = 0;
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public void SetPlayers(List<Player> newPlayers)
    {
        players = new List<Player>(newPlayers);
        currentPlayerIndex = 0;
    }
    
    public List<Player> GetPlayers()
    {
        return players;
    }
    
    public Player GetCurrentPlayer()
    {
        if (players.Count > 0 && currentPlayerIndex < players.Count)
        {
            return players[currentPlayerIndex];
        }
        return null;
    }
    
    public void NextPlayer()
    {
        if (players.Count > 0)
        {
            currentPlayerIndex = (currentPlayerIndex + 1) % players.Count;
        }
    }
    
    public void SetCurrentPlayer(int index)
    {
        if (index >= 0 && index < players.Count)
        {
            currentPlayerIndex = index;
        }
    }
    
    public int GetCurrentPlayerIndex()
    {
        return currentPlayerIndex;
    }
    
    public int GetPlayerCount()
    {
        return players.Count;
    }
    
    public Player GetPlayerByIndex(int index)
    {
        if (index >= 0 && index < players.Count)
        {
            return players[index];
        }
        return null;
    }
    
    public void ClearPlayers()
    {
        players.Clear();
        currentPlayerIndex = 0;
    }
}