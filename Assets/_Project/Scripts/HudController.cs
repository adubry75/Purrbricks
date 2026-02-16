using TMPro;
using UnityEngine;

public class HudController : MonoBehaviour
{
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _livesText;
    [SerializeField] private TMP_Text _stateText;
    [SerializeField] private TMP_Text _centerMessage;

    public void SetScore(int score)
    {
        if (_scoreText != null) _scoreText.text = "Score: " + score;
    }

    public void SetLives(int lives)
    {
        if (_livesText != null) _livesText.text = "Lives: " + lives;
    }

    public void SetState(string state)
    {
        if (_stateText != null) _stateText.text = state;
    }

    public void ShowCenter(string message)
    {
        if (_centerMessage == null) return;
        _centerMessage.gameObject.SetActive(true);
        _centerMessage.text = message;
    }

    public void HideCenter()
    {
        if (_centerMessage == null) return;
        _centerMessage.gameObject.SetActive(false);
    }
}
