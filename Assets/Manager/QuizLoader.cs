using UnityEngine;
using System.Collections.Generic;

// Loads quiz categories/questions and score cards from Resources/QuizData.
// Unlike CardLoader these are loaded via Resources.Load so the quiz scene
// needs no inspector wiring at all.
public static class QuizLoader
{
    public static List<QuizCategoryJSON> LoadCategories()
    {
        TextAsset file = Resources.Load<TextAsset>("QuizData/quiz_questions");
        if (file == null)
        {
            Debug.LogError("QuizLoader: Resources/QuizData/quiz_questions.json not found!");
            return new List<QuizCategoryJSON>();
        }

        QuizDataJSON data = JsonUtility.FromJson<QuizDataJSON>(file.text);
        if (data == null || data.categories == null || data.categories.Count == 0)
        {
            Debug.LogError("QuizLoader: quiz_questions.json contains no categories!");
            return new List<QuizCategoryJSON>();
        }

        return data.categories;
    }

    public static List<string> LoadScoreCards()
    {
        List<string> texts = new List<string>();

        TextAsset file = Resources.Load<TextAsset>("QuizData/quiz_scorecards");
        if (file == null)
        {
            Debug.LogWarning("QuizLoader: Resources/QuizData/quiz_scorecards.json not found - no score cards will appear.");
            return texts;
        }

        ScoreCardsJSON data = JsonUtility.FromJson<ScoreCardsJSON>(file.text);
        if (data != null && data.scoreCards != null)
        {
            foreach (ScoreCardJSON card in data.scoreCards)
            {
                if (!string.IsNullOrWhiteSpace(card.text)) texts.Add(card.text);
            }
        }

        return texts;
    }

    public static Sprite LoadCategoryImage(string imageName)
    {
        if (string.IsNullOrEmpty(imageName)) return null;

        Sprite sprite = Resources.Load<Sprite>($"QuizImages/{imageName}");
        if (sprite == null) sprite = Resources.Load<Sprite>($"CardImages/{imageName}");
        if (sprite == null) Debug.LogWarning($"QuizLoader: category image '{imageName}' not found in Resources/QuizImages or Resources/CardImages.");
        return sprite;
    }
}
