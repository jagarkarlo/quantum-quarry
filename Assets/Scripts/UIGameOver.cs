using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class UIGameOver : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI scoreText;   // Drag a TMP Text here in the GameOver scene
    [SerializeField] TextMeshProUGUI bestText;    // Optional: best score display
    [SerializeField] TextMeshProUGUI statsText;   // Optional: run damage summary

    const string BestScoreKey = "BestScore";

    void Start()
    {
        bool isVictory = SceneManager.GetActiveScene().name == "Victory";
        int final = isVictory
            ? PlayerPrefs.GetInt(GameSession.FinalCoinsKey, 0)
            : PlayerPrefs.GetInt(GameSession.LastScoreKey, 0);
        if (scoreText) scoreText.text = final.ToString();

        int best = Mathf.Max(final, PlayerPrefs.GetInt(BestScoreKey, 0));
        PlayerPrefs.SetInt(BestScoreKey, best);
        PlayerPrefs.Save();
        if (bestText) bestText.text = best.ToString();

        if (statsText)
        {
            int hitsTaken = PlayerPrefs.GetInt(
                isVictory ? GameSession.FinalHitsTakenKey : GameSession.LastHitsTakenKey, 0);
            int stabilityUnitsLost = PlayerPrefs.GetInt(
                isVictory ? GameSession.FinalStabilityLostUnitsKey : GameSession.LastStabilityLostUnitsKey, 0);
            float stabilityLost = stabilityUnitsLost / (float)QuantumStability.UnitsPerPoint;
            statsText.text = $"Hits Taken {hitsTaken}   Stability Lost {stabilityLost:0.#}";
        }
    }
}
