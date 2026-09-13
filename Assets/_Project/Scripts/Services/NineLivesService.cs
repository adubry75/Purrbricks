using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>Owns permanent progression, safe menu access, and frozen per-attempt perks.</summary>
public sealed class NineLivesService : MonoBehaviour
{
    public static NineLivesService Instance { get; private set; }
    public const string SaveKey = "NineLives_Save_v1";
    public const string IntroLevelId = "diamond_rain";
    const string BackupKey = "NineLives_Save_v1_backup";
    [Serializable] sealed class SaveData
    {
        public NineLivesModel Model = new NineLivesModel();
        public string IntroLevelId = "";
        public bool IntroductionSeen;
        public bool MigrationWelcomePending;
        public int MigrationWelcomePoints;
    }
    SaveData save = new SaveData();
    string lastValidJson;
    public NineLivesModel Model => save.Model;
    public event Action Changed;
    public bool IsTreeOpen { get; private set; }
    public bool PendingIntroduction { get; private set; }
    public string WelcomeMessage { get; private set; } = "";
    public bool HasPendingWelcome => save.MigrationWelcomePending;
    public int LastAttemptXp { get; private set; }
    public string LastAttemptXpBreakdown => $"Bricks {attemptBrickXp:N0} + Clear {attemptClearXp:N0}\nFirst clear {attemptFirstClearXp:N0} + Stars {attemptStarXp:N0}";
    public bool IsFuryActive => GameManager.Instance != null && GameManager.Instance.IsFuryActive;
    public float AddedFuryCharge { get; private set; }
    public int StartingLivesBonus => Model.Has("C2") ? 1 : 0;

    // Gameplay tests can construct a frozen build without invoking persistence.
    int _activeMask;
    bool _eligibleAttempt;
    string _activeCapstone = "";
    int _activeStarter;
    string attemptLevel = "";
    bool completed, starterUsed, landingArmed, rescueUsed, partyUsed, partyQueued;
    bool meteorArmed, meteorCarry, continueMeteor;
    int partyCatches, attemptStartXp;
    int attemptBrickXp, attemptClearXp, attemptFirstClearXp, attemptStarXp;
    int attemptGeneration;
    float clock, edgeAt, lastBrickWait, saveAt;
    bool huntBoost, dirty;
    readonly NineLivesAttemptCredit damageCredit = new NineLivesAttemptCredit();
    readonly Dictionary<BallController, float> cloneSafety = new Dictionary<BallController, float>();
    readonly HashSet<BallController> lostBalls = new HashSet<BallController>();
    NineLivesTreeUI tree;
    Action closeAction;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install()
    {
        if (Instance == null) new GameObject("Nine Lives Progression").AddComponent<NineLivesService>();
    }
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject); Load();
    }
    void OnDestroy() { if (Instance == this) { Flush(); Instance = null; } }
    void OnApplicationQuit() { Flush(); }
    void OnApplicationFocus(bool focus) { if (!focus) Flush(); }

    void Load()
    {
        lastValidJson = null;
        bool TryRead(string key)
        {
            if (!PlayerPrefs.HasKey(key)) return false;
            try
            {
                string json = PlayerPrefs.GetString(key);
                var payload = JObject.Parse(json);
                // Initializers must not turn a truncated/missing model into a valid empty save.
                if (!(payload["Model"] is JObject)) return false;
                var restored = payload.ToObject<SaveData>();
                if (restored == null || restored.Model == null) return false;
                restored.Model.Normalize(); save = restored; lastValidJson = json; return true;
            }
            catch (JsonException) { return false; }
        }
        if (!TryRead(SaveKey)) TryRead(BackupKey);
    }
    void Save(bool immediate = false)
    {
        dirty = true;
        if (immediate) Flush();
    }
    void Flush()
    {
        if (!dirty || save == null) return;
        string json = JsonConvert.SerializeObject(save);
        // Never rotate a rejected primary over a backup that just recovered the profile.
        if (!string.IsNullOrEmpty(lastValidJson)) PlayerPrefs.SetString(BackupKey, lastValidJson);
        PlayerPrefs.SetString(SaveKey, json);
        PlayerPrefs.Save(); lastValidJson = json; dirty = false; saveAt = Time.unscaledTime + 1f;
    }
    void NotifyChanged(bool immediate = false) { Save(immediate); Changed?.Invoke(); }

    public void InitializeCampaign(string[] levelIds)
    {
        if (levelIds == null || levelIds.Length == 0) return;
        if (string.IsNullOrEmpty(save.IntroLevelId)) save.IntroLevelId = IntroLevelId;
        bool migrationWasComplete = Model.MigrationCompleted;
        int before = Model.LifetimePoints;
        var recorded = new Dictionary<string, int>();
        foreach (string id in levelIds)
        {
            int stars = LevelStarsHelper.GetBestStars(id);
            if (stars > 0) recorded[id] = stars;
        }
        Model.Migrate(save.IntroLevelId, recorded);
        int credit = Model.LifetimePoints - before;
        if (!migrationWasComplete && recorded.Count > 0)
        {
            save.IntroductionSeen = true;
            save.MigrationWelcomePending = true;
            save.MigrationWelcomePoints = Mathf.Max(0, credit);
        }
        WelcomeMessage = BuildWelcomeMessage();
        PendingIntroduction = Model.IntroGranted && !save.IntroductionSeen;
        NotifyChanged(true);
    }

    public bool CanOpenTree
    {
        get
        {
            var gm = GameManager.Instance;
            return gm != null && !gm.IsEditorTestMode && !gm.IsCommunityMode &&
                (gm.State == GameState.MainMenu || gm.State == GameState.Victory || gm.State == GameState.GameOver);
        }
    }
    public void ShowTree(Action onClose = null)
    {
        if (IsTreeOpen || !CanOpenTree) return;
        IsTreeOpen = true; closeAction = onClose;
        InventoryRadialMenu.Instance?.CancelImmediately();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = InputManager.CurrentScheme != InputScheme.Gamepad;
        if (tree == null)
        {
            var go = new GameObject("Nine Lives Skill Tree"); go.transform.SetParent(transform, false);
            tree = go.AddComponent<NineLivesTreeUI>();
        }
        tree.Show(null);
    }
    public void CloseTree()
    {
        if (!IsTreeOpen) return;
        tree?.Hide(); IsTreeOpen = false;
        save.IntroductionSeen |= PendingIntroduction;
        save.MigrationWelcomePending = false;
        PendingIntroduction = false; WelcomeMessage = "";
        var callback = closeAction; closeAction = null;
        NotifyChanged(true);
        GameManager.Instance?.RestoreGameplayTimeScale();
        Cursor.lockState = CursorLockMode.None; Cursor.visible = InputManager.CurrentScheme != InputScheme.Gamepad;
        callback?.Invoke();
    }
    public bool Buy(string id)
    {
        if (!IsTreeOpen || !CanOpenTree || !Model.Purchase(id)) return false;
        NotifyChanged(true); return true;
    }
    public void Respec()
    {
        if (!IsTreeOpen || !CanOpenTree) return;
        Model.Respec(); NotifyChanged(true);
    }
    public void Equip(string id)
    {
        if (!IsTreeOpen || !CanOpenTree) return;
        Model.EquipCapstone(id); NotifyChanged(true);
    }
    public void ChooseStarter(int type)
    {
        if (!IsTreeOpen || !CanOpenTree || !Model.Has("B2") || (type != 0 && type != 2 && type != 5)) return;
        Model.StarterPowerup = type; NotifyChanged(true);
    }
    public bool Has(string id)
    {
        var node = NineLivesCatalog.Find(id);
        var gm = GameManager.Instance;
        return _eligibleAttempt && node != null && (_activeMask & (1 << node.Index)) != 0 &&
            (gm == null || (!gm.IsDemoMode && !gm.IsCommunityMode && !gm.IsEditorTestMode));
    }
    public bool IsCapstone(string id) => Has(id) && _activeCapstone == id;

    public void ContinueToNextLevel() { continueMeteor = meteorCarry; }
    public void BeginAttempt(string levelId, bool eligible)
    {
        Flush();
        attemptGeneration++;
        bool carry = continueMeteor && meteorCarry;
        _eligibleAttempt = eligible; attemptLevel = levelId ?? "";
        _activeMask = eligible ? Model.PurchasedMask : 0;
        _activeCapstone = eligible ? Model.EquippedCapstone : "";
        _activeStarter = Model.StarterPowerup;
        completed = starterUsed = landingArmed = rescueUsed = partyUsed = partyQueued = false;
        meteorArmed = carry && IsCapstone("A5"); meteorCarry = continueMeteor = false;
        partyCatches = 0; clock = lastBrickWait = 0f; edgeAt = -1f; huntBoost = false;
        ResetFuryBonus(); cloneSafety.Clear(); lostBalls.Clear();
        attemptStartXp = Model.TotalXp; LastAttemptXp = 0;
        attemptBrickXp = attemptClearXp = attemptFirstClearXp = attemptStarXp = 0;
        var originals = new Dictionary<object, int>();
        if (eligible && LevelLoader.Instance != null)
            foreach (var brick in LevelLoader.Instance.CurrentBricks)
                if (brick != null && !brick.IsIndestructible) originals[brick] = brick.MaxHitPoints;
        damageCredit.Begin(originals);
        Changed?.Invoke();
    }
    public void EndAttempt()
    {
        Flush(); attemptGeneration++; _eligibleAttempt = false; _activeMask = 0; _activeCapstone = "";
        meteorCarry = meteorArmed = continueMeteor = false;
        cloneSafety.Clear(); lostBalls.Clear(); ResetFuryBonus();
    }
    public void NotifyBrickDamage(Brick brick, int actualDamage)
    {
        if (!_eligibleAttempt || completed || brick == null || brick.IsIndestructible || actualDamage <= 0) return;
        var gm = GameManager.Instance;
        if (gm == null || gm.IsDemoMode || gm.IsCommunityMode || gm.IsEditorTestMode) return;
        lastBrickWait = 0f; huntBoost = false;
        int earned = damageCredit.Damage(brick, actualDamage);
        if (!Model.IntroGranted || earned == 0) return;
        int beforeXp = Model.TotalXp;
        Model.AddXp(earned); LastAttemptXp = Model.TotalXp - attemptStartXp;
        attemptBrickXp += Model.TotalXp - beforeXp;
        NotifyChanged();
    }
    public void CompleteLevel(int stars)
    {
        if (!_eligibleAttempt || completed) return;
        completed = true;
        int beforeXp = Model.TotalXp;
        int oldStars = 0;
        bool cleared = Model.CreditedStars != null && Model.CreditedStars.TryGetValue(attemptLevel, out oldStars);
        bool intro = Model.CompleteLevel(attemptLevel, save.IntroLevelId, stars);
        if (intro) PendingIntroduction = true;
        // Allocate only XP actually credited, including attempts that reach the cap.
        int credited = Math.Max(0, Model.TotalXp - beforeXp);
        attemptClearXp = Math.Min(30, credited); credited -= attemptClearXp;
        attemptFirstClearXp = Math.Min(cleared ? 0 : 60, credited); credited -= attemptFirstClearXp;
        attemptStarXp = Math.Min(5 * Math.Max(0, Math.Min(3, stars) - oldStars), credited);
        LastAttemptXp = Model.TotalXp - attemptStartXp;
        NotifyChanged(true);
    }
    public void NotifyLifeLost()
    {
        Flush(); landingArmed = Has("C4"); meteorArmed = meteorCarry = false;
        partyQueued = false; cloneSafety.Clear(); lostBalls.Clear();
        ResetFuryBonus(); lastBrickWait = 0f; huntBoost = false;
    }
    public void NotifyBallLaunch()
    {
        if (!_eligibleAttempt || completed) return;
        var pm = PowerupManager.Instance;
        if (!starterUsed)
        {
            starterUsed = true;
            if (Has("B2")) pm?.GrantAtLeast((PowerupType)_activeStarter, 6f);
        }
        if (landingArmed)
        {
            landingArmed = false;
            if (Has("C4")) pm?.GrantAtLeast(PowerupType.ShieldWall, 3f);
        }
        // Defer until the current launch/clone operation returns; avoid recursive cloning.
        if (partyQueued) StartCoroutine(DeliverPartyNextFrame());
    }
    IEnumerator DeliverPartyNextFrame()
    {
        int generation = attemptGeneration;
        yield return null;
        if (generation == attemptGeneration) TryDeliverParty();
    }
    public void NotifyWorldPickup(PowerupType type)
    {
        if (!IsCapstone("B5") || partyUsed || completed || ((int)type > 10 && type != PowerupType.PermanentStickyBall)) return;
        partyCatches = Mathf.Min(3, partyCatches + 1);
        if (partyCatches >= 3) { partyUsed = true; partyQueued = true; TryDeliverParty(); }
        Changed?.Invoke();
    }
    void TryDeliverParty()
    {
        var gm = GameManager.Instance;
        if (!partyQueued || completed || gm == null || gm.IsGameplaySuspended) return;
        bool launched = false;
        foreach (var ball in BallController.ActiveBalls)
            if (ball != null && ball.IsLaunched() && !ball.IsStickyHeld) { launched = true; break; }
        var powerups = PowerupManager.Instance;
        if (!launched || powerups == null) return;
        partyQueued = false;
        powerups.GrantAtLeast(PowerupType.Fireball, 10f);
        // Clones inherit Fireball immediately, before their first physics step.
        powerups.GrantMultiBall();
        PowerupNotification.Instance?.Show("CATNIP PARTY!", new Color(.6f, 1f, .35f), true);
    }
    public void NotifyPaddleReturn(BallController ball, bool edge)
    {
        if (!_eligibleAttempt || completed || IsFuryActive || GameManager.Instance?.IsGameplaySuspended == true) return;
        var gm = GameManager.Instance;
        if (ball != null && edge && Has("A3") && clock >= edgeAt && gm != null && gm.GetFuryChargeFraction() < 1f)
        {
            AddedFuryCharge = Mathf.Min(1f, AddedFuryCharge + .03f); edgeAt = clock + .5f;
            gm.InvalidateFuryCharge();
            ScorePopup.SpawnMessage(ball.transform.position,
                gm.GetFuryChargeFraction() >= 1f ? "FURY READY!" : "+3% FURY", new Color(1f, .83f, .2f));
            SfxPlayer.Instance?.PlayEdgeHunter();
        }
        if (meteorArmed && IsCapstone("A5"))
        {
            meteorArmed = false; PowerupManager.Instance?.GrantAtLeast(PowerupType.Fireball, 4f);
            PowerupNotification.Instance?.Show("METEOR RETURN!", new Color(1f, .5f, .15f), true);
        }
    }
    public void NotifyFuryCompleted()
    {
        if (IsCapstone("A5")) { meteorCarry = true; Changed?.Invoke(); }
    }
    public float FuryRateMultiplier => (Has("A1") ? 1.2f : 1f) * (huntBoost && Has("A4") ? 2f : 1f);
    public float ConsumeAddedFuryCharge() { float charge = AddedFuryCharge; AddedFuryCharge = 0f; return charge; }
    public void ResetFuryBonus() { AddedFuryCharge = 0f; }
    public void RegisterCloneSafety(BallController ball)
    {
        if (ball != null && Has("B4")) cloneSafety[ball] = clock + 3f;
    }
    public bool TryPreventBallLoss(BallController ball)
    {
        if (!_eligibleAttempt || completed || ball == null || GameManager.Instance?.State != GameState.Playing) return false;
        if (cloneSafety.TryGetValue(ball, out float expires) && clock <= expires && Has("B4"))
        {
            cloneSafety.Remove(ball); ball.RescueBounce(); return true;
        }
        int alive = 0;
        foreach (var current in BallController.ActiveBalls)
            if (current != null && current.gameObject.activeInHierarchy && current.IsLaunched() && !lostBalls.Contains(current)) alive++;
        if (alive == 1 && !rescueUsed && IsCapstone("C5"))
        {
            rescueUsed = true; ball.RescueBounce(); GameManager.Instance?.ResetCombo();
            PowerupNotification.Instance?.Show("NINTH LIFE — SAVED!", new Color(.3f, 1f, .6f), true);
            Changed?.Invoke(); return true;
        }
        lostBalls.Add(ball); return false;
    }
    public string ActiveStatus
    {
        get
        {
            if (!_eligibleAttempt) return "";
            if (IsCapstone("B5")) return "CATNIP PARTY " + (partyQueued ? "READY" : partyUsed ? "USED" : partyCatches + "/3");
            if (IsCapstone("C5")) return "NINTH LIFE " + (rescueUsed ? "USED" : "READY");
            if (IsCapstone("A5") && (meteorCarry || meteorArmed)) return meteorCarry ? "METEOR RETURN: NEXT LEVEL" : "METEOR RETURN READY";
            return huntBoost ? "LAST BRICKS: FURY BOOST" : "";
        }
    }
    void Update()
    {
        if (dirty && Time.unscaledTime >= saveAt) Flush();
        var gm = GameManager.Instance;
        if (!_eligibleAttempt || completed || gm == null || gm.State != GameState.Playing || gm.IsGameplaySuspended) return;
        clock += Time.deltaTime;
        bool anyFlying = false;
        foreach (var ball in BallController.ActiveBalls)
            if (ball != null && ball.IsLaunched() && !ball.IsStickyHeld) { anyFlying = true; break; }
        bool huntCondition = Has("A4") && !IsFuryActive && LevelManager.Instance != null &&
            LevelManager.Instance.BricksRemaining > 0 && LevelManager.Instance.BricksRemaining <= 3;
        if (huntCondition && anyFlying)
        {
            lastBrickWait += Time.deltaTime;
            if (!huntBoost && lastBrickWait >= 15f)
            {
                huntBoost = true;
                PowerupNotification.Instance?.Show("LAST BRICKS: FURY BOOST", new Color(1f,.8f,.2f), true);
            }
        }
        else if (!huntCondition) { lastBrickWait = 0f; huntBoost = false; }
        if (partyQueued) TryDeliverParty();
    }

    string BuildWelcomeMessage()
    {
        if (!save.MigrationWelcomePending) return "";
        int points = Mathf.Max(0, save.MigrationWelcomePoints);
        return "WELCOME BACK! Your saved campaign progress earned " + points + " skill point" +
            (points == 1 ? "." : "s.");
    }
}
