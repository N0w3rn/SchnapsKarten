using System.Collections.Generic;

[System.Serializable]
public class QuizQuestionJSON
{
    public string question;
    public string answer = "";
}

[System.Serializable]
public class QuizCategoryJSON
{
    public string name;
    public string imageName = "";
    public bool isAction = false;
    public List<QuizQuestionJSON> questions = new List<QuizQuestionJSON>();
}

[System.Serializable]
public class QuizDataJSON
{
    public List<QuizCategoryJSON> categories = new List<QuizCategoryJSON>();
}

[System.Serializable]
public class ScoreCardJSON
{
    public string text;
}

[System.Serializable]
public class ScoreCardsJSON
{
    public List<ScoreCardJSON> scoreCards = new List<ScoreCardJSON>();
}

public class Team
{
    public string name;
    public List<string> players = new List<string>();
    public int score;

    public Team(string teamName)
    {
        name = teamName;
    }
}

// Hands the teams built in the QuizSetup scene over to the QuizScene.
// Same idea as PlayerManager for the classic mode, but no scene object needed.
public static class QuizSession
{
    public static List<Team> teams;
}
