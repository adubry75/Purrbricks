using TMPro;
using UnityEngine;

public class HudController : MonoBehaviour
{
    [SerializeField] private TMP_Text _scoreText;
    [SerializeField] private TMP_Text _livesText;
    [SerializeField] private TMP_Text _stateText;
    [SerializeField] private TMP_Text _centerMessage;
    [SerializeField] private TMP_Text _comboText;

    // Persisted across SetState calls so the level name is always visible
    private string _levelInfo = "";
    private string _lastState = "";
    private string _levelCode = "";
    private int    _lastLives;

    public void SetScore(int score)
    {
        if (_scoreText != null) _scoreText.text = "Score: " + score;
    }

    public void SetLives(int lives)
    {
        _lastLives = lives;
        RefreshLivesText();
    }

    /// <summary>Sets the level code shown under the Lives label. Pass empty string to hide.</summary>
    public void SetLevelCode(string code)
    {
        _levelCode = code ?? "";
        RefreshLivesText();
    }

    private void RefreshLivesText()
    {
        if (_livesText == null) return;
        _livesText.text = string.IsNullOrEmpty(_levelCode)
            ? $"Lives: {_lastLives}"
            : $"Lives: {_lastLives}\n<size=70%>CODE: {_levelCode}</size>";
    }

    /// <summary>
    /// Stores the current level number and title so every subsequent
    /// SetState call shows it persistently in the state text area.
    /// </summary>
    public void SetLevelInfo(int levelNumber, string levelTitle)
    {
        _levelInfo = string.IsNullOrEmpty(levelTitle)
            ? $"Level {levelNumber}"
            : $"Level {levelNumber}: {levelTitle}";
        RefreshStateText();
    }

    public void SetState(string state)
    {
        _lastState = state;
        RefreshStateText();
    }

    private void RefreshStateText()
    {
        if (_stateText == null) return;

        if (string.IsNullOrEmpty(_levelInfo))
            _stateText.text = _lastState;
        else if (string.IsNullOrEmpty(_lastState))
            _stateText.text = _levelInfo;
        else
            _stateText.text = $"{_levelInfo}";
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

    public void SetCombo(int combo)
    {
        if (_comboText == null) return;
        int multiplier = 1 + combo;
        _comboText.text = "Combo: x" + multiplier;
    }

    public void SetStatus(string status)
    {
        SetState(status);
    }

    public void SetLevel(int levelNumber)
    {
        SetLevelInfo(levelNumber, "");
    }
}
