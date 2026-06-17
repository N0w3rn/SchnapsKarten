using UnityEngine;
using System.Collections.Generic;

public class PlayerManager : MonoBehaviour
{
    public static PlayerManager instance;
    
    private List<Player> players = new List<Player>();

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
    }

    public List<Player> GetPlayers()
    {
        return players;
    }

    public int GetPlayerCount()
    {
        return players.Count;
    }

    public void ClearPlayers()
    {
        players.Clear();
    }
}