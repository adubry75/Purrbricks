using UnityEngine;

public class SfxPlayer : MonoBehaviour
{
    public static SfxPlayer Instance { get; private set; }

    [Header("Clips")]
    [SerializeField] private AudioClip _paddleHit;
    [SerializeField] private AudioClip _wallHit;
    [SerializeField] private AudioClip _brickBreak;
    [SerializeField] private AudioClip _lifeLost;
    [SerializeField] private AudioClip _win;
    [SerializeField] private AudioClip _gameOver;

    [Header("Tuning")]
    [SerializeField] private float _volume = 0.7f;
    [SerializeField] private float _pitchMin = 0.97f;
    [SerializeField] private float _pitchMax = 1.03f;

    private AudioSource _src;

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
    public void PlayLifeLost() => PlayOne(_lifeLost);
    public void PlayWin() => PlayOne(_win);
    public void PlayGameOver() => PlayOne(_gameOver);

    private void PlayOne(AudioClip clip)
    {
        if (clip == null || _src == null) return;

        _src.pitch = UnityEngine.Random.Range(_pitchMin, _pitchMax);
        _src.PlayOneShot(clip, _volume);
    }
}
