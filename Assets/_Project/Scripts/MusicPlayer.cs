using System.Collections;
using UnityEngine;

/// <summary>
/// Handles all background music: menu loop, gameplay track cycling,
/// level-finish and game-over stingers, and Fury Strike pitch-shift.
/// Two AudioSources are used for smooth crossfading (A/B pattern).
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    public static MusicPlayer Instance { get; private set; }

    [Header("Tracks")]
    [SerializeField] private AudioClip   _menuTrack;
    [SerializeField] private AudioClip[] _gameplayTracks;   // cycles 1→2→3→1…
    [SerializeField] private AudioClip   _gameOverTrack;
    [SerializeField] private AudioClip   _levelFinishTrack;
    [SerializeField] private AudioClip   _gameFinishedTrack; // plays during end credits

    [Header("Settings")]
    [SerializeField] [Range(0f, 1f)] private float _musicVolume      = 0.50f;
    [SerializeField]                 private float _crossfadeDuration = 1.5f;
    [SerializeField]                 private float _furyPitchMax      = 1.28f;

    private AudioSource _srcA;
    private AudioSource _srcB;
    private AudioSource _active;
    private AudioSource _inactive;

    private bool _inGameplay;
    private bool _randomTracks;
    private int  _trackIndex;
    private Coroutine _musicRoutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _srcA = MakeSource();
        _srcB = MakeSource();
        _active   = _srcA;
        _inactive = _srcB;
    }

    private AudioSource MakeSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop        = false;
        s.volume      = 0f;
        return s;
    }

    private void Update()
    {
        if (!_inGameplay || _active == null || !_active.isPlaying) return;

        // Pitch-shift gameplay music based on Fury Strike charge level
        float ramp = GameManager.Instance?.GetPrimaryBall()?.RampFraction ?? 0f;
        _active.pitch = Mathf.Lerp(1f, _furyPitchMax, ramp);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void PlayMenu()
    {
        _inGameplay = false;
        // Don't restart if menu track is already playing (Menu ↔ HighScores continuity)
        if (_active != null && _active.clip == _menuTrack && _active.isPlaying)
            return;
        Run(MenuLoop());
    }

    public void PlayGameplay(int levelIndex = 0)
    {
        _inGameplay   = true;
        _randomTracks = levelIndex != 0;

        if (_gameplayTracks == null || _gameplayTracks.Length == 0)
            _trackIndex = 0;
        else if (levelIndex == 0)  // We always play this song for level 1, because it's good and will hook new players.
            _trackIndex = 0;
        else
        {
            int randomTrack = Random.Range(0, _gameplayTracks.Length);
            Debug.Log($"Playing random track # {randomTrack}: {_gameplayTracks[randomTrack].name}");
            _trackIndex = randomTrack ; // Otherwise for any other level, randomly pick a song.
        }

        Run(GameplayLoop());
    }

    public void PlayGameOver()
    {
        _inGameplay = false;
        Run(Stinger(_gameOverTrack));
    }

    public void PlayLevelFinish()
    {
        _inGameplay = false;
        Run(Stinger(_levelFinishTrack));
    }

    public void PlayGameFinished()
    {
        _inGameplay = false;
        Run(GameFinishedLoop());
    }

    /// <summary>Applies a new master volume to the active source immediately (0–1).</summary>
    public void SetVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        if (_active != null && _active.isPlaying)
            _active.volume = _musicVolume;
    }

    public float GetVolume() => _musicVolume;

    // ── Coroutines ────────────────────────────────────────────────────────────

    private void Run(IEnumerator routine)
    {
        if (_musicRoutine != null) StopCoroutine(_musicRoutine);
        _musicRoutine = StartCoroutine(routine);
    }

    private IEnumerator MenuLoop()
    {
        yield return Crossfade(_menuTrack, loop: true);
    }

    private IEnumerator GameplayLoop()
    {
        if (_gameplayTracks == null || _gameplayTracks.Length == 0) yield break;

        while (_inGameplay)
        {
            var clip = _gameplayTracks[_trackIndex % _gameplayTracks.Length];
            yield return Crossfade(clip, loop: false);

            // Wait for this track to finish playing naturally.
            // Guard: require the clip to be near its end before advancing — prevents a false
            // trigger when the audio device briefly resets on alt-tab (ExclusiveFullScreen).
            yield return new WaitUntil(() =>
                !_inGameplay || _active == null || _active.clip == null ||
                (!_active.isPlaying && _active.time >= _active.clip.length - 1f));

            if (!_inGameplay) yield break;

            _trackIndex = _randomTracks
                ? Random.Range(0, _gameplayTracks.Length)
                : (_trackIndex + 1) % _gameplayTracks.Length;
        }
    }

    private IEnumerator Stinger(AudioClip clip)
    {
        yield return Crossfade(clip, loop: false);
    }

    private IEnumerator GameFinishedLoop()
    {
        yield return Crossfade(_gameFinishedTrack, loop: true);
    }

    /// <summary>Crossfades from the current active source to a new clip.</summary>
    private IEnumerator Crossfade(AudioClip clip, bool loop)
    {
        if (clip == null) yield break;

        var fadeIn  = _inactive;
        var fadeOut = _active;

        fadeIn.clip   = clip;
        fadeIn.loop   = loop;
        fadeIn.pitch  = 1f;
        fadeIn.volume = 0f;
        fadeIn.Play();

        float elapsed  = 0f;
        float startVol = fadeOut.volume;

        while (elapsed < _crossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;   // works while paused
            float t  = Mathf.Clamp01(elapsed / _crossfadeDuration);
            fadeIn.volume  = Mathf.Lerp(0f, _musicVolume, t);
            fadeOut.volume = Mathf.Lerp(startVol, 0f, t);
            yield return null;
        }

        fadeIn.volume  = _musicVolume;
        fadeOut.Stop();
        fadeOut.clip   = null;
        fadeOut.volume = 0f;

        _active   = fadeIn;
        _inactive = fadeOut;
    }
}
