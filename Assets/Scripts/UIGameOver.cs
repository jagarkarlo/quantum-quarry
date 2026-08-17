using UnityEngine;
using TMPro;

public class UIGameOver : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI scoreText;   // Drag a TMP Text here in the GameOver scene
    [SerializeField] TextMeshProUGUI bestText;    // Optional: best score display

    const string LastScoreKey = "LastScore";
    const string BestScoreKey = "BestScore";

    void Start()
    {
        int final = PlayerPrefs.GetInt(LastScoreKey, 0);
        if (scoreText) scoreText.text = final.ToString();

        int best = Mathf.Max(final, PlayerPrefs.GetInt(BestScoreKey, 0));
        PlayerPrefs.SetInt(BestScoreKey, best);
        PlayerPrefs.Save();
        if (bestText) bestText.text = best.ToString();
    }
}
