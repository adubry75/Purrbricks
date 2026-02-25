using UnityEngine;

public class SfxPlayer : MonoBehaviour
{
    public static SfxPlayer Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip _paddleHit;
    [SerializeField] private AudioClip _wallHit;
    [SerializeField] private AudioClip _brickBreak;
    [SerializeField] private AudioClip _brickBreakHeavy;  // used for tough (>1 HP) bricks
    [SerializeField] private AudioClip _brickHit;
    [SerializeField] private AudioClip _lifeLost;
    [SerializeField] private AudioClip _win;
    [SerializeField] private AudioClip _gameOver;
    [SerializeField] private AudioClip _laser;
    [SerializeField] private AudioClip _powerupPickup;
    [SerializeField] private AudioClip _furyStrike;       // Fury Strike activation fanfare

    [Header("Tuning")]
    [SerializeField] private float _volume = 0.7f;
    [SerializeField] private float _pitchMin = 0.97f;
    [SerializeField] private float _pitchMax = 1.03f;

    private AudioSource _src;
    private bool _isMuted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        _src = GetComponent<AudioSource>();
    }

    public void PlayPaddleHit() => PlayOne(_paddleHit);
    public void PlayWallHit() => PlayOne(_wallHit);
    public void PlayBrickBreak() => PlayOne(_brickBreak);
    /// <summary>Deep, heavy break for tough bricks (maxHp > 1). Falls back to standard break if clip not assigned.</summary>
    public void PlayBrickBreakHeavy() => PlayOne(_brickBreakHeavy != null ? _brickBreakHeavy : _brickBreak);
    public void PlayBrickHit() => PlayOne(_brickHit);
    public void PlayLifeLost() => PlayOne(_lifeLost);
    public void PlayWin() => PlayOne(_win);
    public void PlayGameOver() => PlayOne(_gameOver);
    public void PlayLaser() => PlayOne(_laser);
    public void PlayPowerupPickup() => PlayOne(_powerupPickup);
    public void PlayFuryStrike() => PlayOne(_furyStrike != null ? _furyStrike : _powerupPickup);

    public void MuteAll(bool mute) { _isMuted = mute; }

    /// <summary>Sets the master SFX volume (0–1). Does not affect the mute toggle.</summary>
    public void SetVolume(float volume) { _volume = Mathf.Clamp01(volume); }

    public float GetVolume() => _volume;

    private void PlayOne(AudioClip clip)
    {
        if (clip == null || _src == null || _isMuted) return;

        _src.pitch = UnityEngine.Random.Range(_pitchMin, _pitchMax);
        _src.PlayOneShot(clip, _volume);
    }
}
