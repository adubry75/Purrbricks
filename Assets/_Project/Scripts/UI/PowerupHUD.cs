using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>Compact effect strip and three persistent inventory favorites.</summary>
public class PowerupHUD : MonoBehaviour
{
    public static PowerupHUD Instance { get; private set; }
    public static readonly Color[] TypeColors =
    {
        new Color(0.30f, 0.60f, 1.00f),  // 0  WidePaddle
        new Color(1.00f, 0.40f, 0.00f),  // 1  MultiBall
        new Color(0.60f, 0.00f, 1.00f),  // 2  StickyBall
        new Color(1.00f, 0.85f, 0.00f),  // 3  SpeedBall
        new Color(0.10f, 1.00f, 0.30f),  // 4  ExtraLife
        new Color(1.00f, 0.10f, 0.30f),  // 5  Laser
        new Color(1.00f, 0.45f, 0.00f),  // 6  Fireball
        new Color(0.90f, 0.20f, 0.90f),  // 7  BombBrick
        new Color(0.00f, 0.90f, 1.00f),  // 8  ShieldWall
        new Color(0.60f, 0.85f, 1.00f),  // 9  BigBall
        new Color(1.00f, 0.85f, 0.00f),  // 10 ScoreFrenzy
        new Color(0.90f, 0.20f, 0.20f),  // 11 ShrinkPaddle
        new Color(0.40f, 0.90f, 0.10f),  // 12 ZipBall
        new Color(0.65f, 0.10f, 0.80f),  // 13 FlipControls
        new Color(0.20f, 0.75f, 0.35f),  // 14 CursedBall
        new Color(1.00f, 0.25f, 0.50f),  // 15 TinyBall
        new Color(0.55f, 0.55f, 0.60f),  // 16 InvisiBall
        new Color(1.00f, 0.55f, 0.00f),  // 17 DrunkenPaddle
        new Color(0.60f, 0.00f, 1.00f),  // 18 PermanentStickyBall
        new Color(0.20f, 0.25f, 0.65f),  // 19 DrunkVision
        new Color(0.12f, 0.60f, 0.65f),  // 20 GremlinBounces
        new Color(0.80f, 0.10f, 0.10f),  // 21 FlipScreen
    };

    public static readonly string[] TypeLabels =
    {
        "WIDE PADDLE",   // 0
        "MULTI-BALL",    // 1
        "STICKY BALL",   // 2
        "SPEED BALL",    // 3
        "+ LIFE",        // 4
        "LASER",         // 5
        "FIREBALL",      // 6
        "BOMB BRICK",    // 7
        "SHIELD",        // 8
        "BIG BALL",      // 9
        "SCORE FRENZY",  // 10
        "⚠ SHRINK",      // 11
        "⚠ ZIP BALL",    // 12
        "⚠ FLIP CTRL",   // 13
        "⚠ CURSED",      // 14
        "⚠ TINY BALL",   // 15
        "⚠ INVISIBALL",  // 16
        "⚠ DRUNK PAD",   // 17
        "STICKY ∞",      // 18
        "⚠ DRUNK VIS",   // 19
        "⚠ GREMLIN",     // 20
        "⚠ FLIP SCR",    // 21
    };


    GameObject root;
    readonly FavoriteSlots favorites = new FavoriteSlots();
    readonly TMP_Text[] favoriteLabels = new TMP_Text[3];
    readonly TMP_Text[] timers = new TMP_Text[22];
    readonly RectTransform[] fills = new RectTransform[22];
    readonly GameObject[] chips = new GameObject[22];
    readonly int[] lastSeconds = new int[22];
    float refreshAt;
    bool editing;
    TMP_Text editLabel;
    TMP_Text feedback;
    CanvasGroup controlsGroup;
    GameObject controlsRoot;
    TMP_Text controlsNotice;
    public bool IsControlMode { get; private set; }
    Vector2 playPointer;
    CursorLockMode playLock;
    TMP_Text skillStatus;
    float feedbackUntil;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        for(int i=0;i<3;i++) favorites.Assign(i, PlayerPrefs.GetInt("favorite_powerup_"+i,-1));
        Build();
    }
    public void SetVisible(bool visible) { root.SetActive(visible); if (!visible) CloseControls(); }
    public void ToggleControls()
    {
        if (IsControlMode) { InventoryRadialMenu.Instance?.CancelImmediately(); CloseControls(); return; }
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsPlayingOrReady() || gm.IsGameplaySuspended) return;
        IsControlMode = true;
        playLock = Cursor.lockState;
        playPointer = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        controlsGroup.interactable = controlsGroup.blocksRaycasts = true;
        controlsNotice.transform.parent.gameObject.SetActive(true);
        gm.RestoreGameplayTimeScale();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = InputManager.CurrentScheme != InputScheme.Gamepad;
        if (InputManager.CurrentScheme == InputScheme.Gamepad) UINavController.SetDefault(favoriteLabels[0].transform.parent.gameObject);
        else Mouse.current?.WarpCursorPosition(new Vector2(Screen.width * .5f, Screen.height * .09f));
    }
    public void CloseControls()
    {
        bool wasOpen = IsControlMode;
        IsControlMode = false; editing = false;
        if (controlsGroup != null) controlsGroup.interactable = controlsGroup.blocksRaycasts = false;
        if (controlsNotice != null) controlsNotice.transform.parent.gameObject.SetActive(false);
        if (wasOpen && GameManager.Instance?.IsPlayingOrReady() == true && InputManager.CurrentScheme == InputScheme.MouseKeyboard)
            Mouse.current?.WarpCursorPosition(playPointer);
        GameManager.Instance?.RestoreGameplayTimeScale();
        if (wasOpen && GameManager.Instance?.IsPlayingOrReady() == true && !GameManager.Instance.IsGameplaySuspended)
            Cursor.lockState = playLock;
        RefreshPointerVisibility();
    }
    void OnDestroy() { if(Instance==this) Instance=null; }
    public void Pin(int slot, PowerupType type)
    {
        if(!favorites.Assign(slot,(int)type)) return;
        for(int i=0;i<3;i++) PlayerPrefs.SetInt("favorite_powerup_"+i,favorites.Get(i));
        PlayerPrefs.Save(); editing=false; refreshAt=0;
    }
    public void ShowFeedback(string message)
    { feedback.text=message; feedbackUntil=Time.unscaledTime+3; }
    void Use(int slot)
    {
        var gm=GameManager.Instance;
        if(gm==null || gm.IsInventoryUseBlocked) return;
        if(editing || favorites.Get(slot)<0)
        { InventoryRadialMenu.Instance?.OpenForFavorite(slot); return; }
        var type=(PowerupType)favorites.Get(slot);
        if(PowerupManager.Instance==null) return;
        if(!PowerupManager.Instance.CanApplyFromInventory(type,out var reason))
        { ShowFeedback(reason); return; }
        if(PurrBucksManager.Instance?.TryUseFromInventory(type)!=true) ShowFeedback("No stock remaining for this favorite.");
        refreshAt=0;
    }
    void Update()
    {
        if(root==null || !root.activeSelf) return;
        var gm=GameManager.Instance;
        var keyboard=Keyboard.current;
        if (IsControlMode && (gm == null || !gm.IsPlayingOrReady())) CloseControls();
        if (keyboard?.enterKey.wasPressedThisFrame == true || keyboard?.numpadEnterKey.wasPressedThisFrame == true)
            ToggleControls();
        else if (IsControlMode && keyboard?.escapeKey.wasPressedThisFrame == true && !UINavController.RadialMenuOpen)
            CloseControls();
        if (IsControlMode) RefreshPointerVisibility();
        if(gm!=null && !gm.IsGameplaySuspended && (gm.State==GameState.Playing || gm.State==GameState.Ready))
        {
            var k=Keyboard.current; var g=Gamepad.current;
            if(k?.digit1Key.wasPressedThisFrame==true || g?.dpad.left.wasPressedThisFrame==true) Use(0);
            if(k?.digit2Key.wasPressedThisFrame==true || g?.dpad.up.wasPressedThisFrame==true) Use(1);
            if(k?.digit3Key.wasPressedThisFrame==true || g?.dpad.right.wasPressedThisFrame==true) Use(2);
            RefreshPointerVisibility();
        }
        if(Time.unscaledTime<refreshAt) return;
        refreshAt=Time.unscaledTime+.1f;
        skillStatus.text = NineLivesService.Instance?.ActiveStatus ?? "";
        var pm=PowerupManager.Instance;
        int visible=0;
        for(int i=0;i<22;i++)
        {
            float remaining=pm!=null ? pm.GetRemaining((PowerupType)i) : 0;
            bool active=remaining>0;
            chips[i].SetActive(active);
            if(!active) continue;
            var rt=(RectTransform)chips[i].transform;
            rt.anchoredPosition=new Vector2(-825+(visible%11)*165,-83-(visible/11)*39);
            visible++;
            int seconds=float.IsInfinity(remaining) ? -1 : Mathf.CeilToInt(remaining);
            if(lastSeconds[i]!=seconds) { timers[i].text=seconds<0 ? "∞" : seconds.ToString(); lastSeconds[i]=seconds; }
            fills[i].anchorMax=new Vector2(float.IsInfinity(remaining)?1:Mathf.Clamp01(remaining/PowerupManager.POWERUP_DURATION),1);
        }
        for(int i=0;i<3;i++)
        {
            int type=favorites.Get(i);
            int qty=type<0 ? 0 : PurrBucksManager.Instance?.GetInventoryCount((PowerupType)type) ?? 0;
            string key=InputManager.CurrentScheme==InputScheme.Gamepad ? (i==0 ? "←" : i==1 ? "↑" : "→") : (i+1).ToString();
            favoriteLabels[i].text=type<0 ? key+"  PIN POWER-UP" : key+"  "+TypeLabels[type]+" ×"+qty;
            favoriteLabels[i].color=qty>0?Color.white:new Color(.6f,.65f,.72f);
        }
        editLabel.text=editing?"CHOOSE A SLOT":"EDIT FAVORITES";
        if(Time.unscaledTime>feedbackUntil) feedback.text="";
    }
    static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform)); go.transform.SetParent(parent,false);
        var rt=(RectTransform)go.transform; rt.anchorMin=rt.anchorMax=anchor; rt.pivot=new Vector2(.5f,1); rt.anchoredPosition=position; rt.sizeDelta=size; return rt;
    }
    static TMP_Text Label(Transform parent,string text,int size)
    {
        var rt=Rect(parent,"Label",new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
        rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=new Vector2(5,0);rt.offsetMax=new Vector2(-5,0);
        var t=rt.gameObject.AddComponent<TextMeshProUGUI>();t.text=text;t.fontSize=size;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;
        t.enableAutoSizing=true;t.fontSizeMin=12;t.fontSizeMax=size;return t;
    }
    TMP_Text Button(string name,Vector2 position,Vector2 size,UnityEngine.Events.UnityAction action)
    {
        var rt=Rect(controlsRoot.transform,name,new Vector2(.5f,0),position,size);
        var image=rt.gameObject.AddComponent<Image>();image.color=new Color(.06f,.10f,.19f,.96f);
        var b=rt.gameObject.AddComponent<Button>();b.onClick.AddListener(action);
        return Label(rt,name,18);
    }
    static void RefreshPointerVisibility()
    {
        if (InputManager.CurrentScheme == InputScheme.Gamepad) return;
        // A shortcut may have opened the radial earlier in this same Update.
        if (UINavController.RadialMenuOpen) { Cursor.visible = true; return; }
        var gm = GameManager.Instance;
        if (gm == null || !gm.IsPlayingOrReady()) return;
        Cursor.visible = (Instance != null && Instance.IsControlMode) || gm.IsGameplaySuspended;
    }

    void Build()
    {
        var c=gameObject.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=50;
        var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();
        root=new GameObject("CompactHUD",typeof(RectTransform));root.transform.SetParent(transform,false);
        var r=(RectTransform)root.transform;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.sizeDelta=Vector2.zero;
        for(int i=0;i<22;i++)
        {
            var rt=Rect(root.transform,"Effect_"+i,new Vector2(.5f,1),Vector2.zero,new Vector2(158,34));
            chips[i]=rt.gameObject;var bg=rt.gameObject.AddComponent<Image>();bg.color=new Color(.04f,.07f,.13f,.94f);bg.raycastTarget=false;
            var t=Label(rt,TypeLabels[i],14);t.color=TypeColors[i];t.rectTransform.offsetMax=new Vector2(-28,-3);
            timers[i]=Label(rt,"",14);timers[i].alignment=TextAlignmentOptions.Right;lastSeconds[i]=int.MinValue;
            var bar=Rect(rt,"Timer",Vector2.zero,Vector2.zero,Vector2.zero);bar.anchorMin=Vector2.zero;bar.anchorMax=new Vector2(1,0);bar.offsetMin=Vector2.zero;bar.offsetMax=new Vector2(0,3);
            fills[i]=Rect(bar,"Fill",Vector2.zero,Vector2.zero,Vector2.zero);fills[i].anchorMin=Vector2.zero;fills[i].anchorMax=Vector2.one;fills[i].offsetMin=fills[i].offsetMax=Vector2.zero;
            var fill=fills[i].gameObject.AddComponent<Image>();fill.color=TypeColors[i];fill.raycastTarget=false;
            chips[i].SetActive(false);
        }
        controlsRoot=new GameObject("Bottom controls",typeof(RectTransform),typeof(CanvasGroup));
        controlsRoot.transform.SetParent(root.transform,false);
        var controlsRect=(RectTransform)controlsRoot.transform; controlsRect.anchorMin=Vector2.zero; controlsRect.anchorMax=Vector2.one; controlsRect.sizeDelta=Vector2.zero;
        controlsGroup=controlsRoot.GetComponent<CanvasGroup>(); controlsGroup.interactable=controlsGroup.blocksRaycasts=false;
        for(int i=0;i<3;i++){int slot=i;favoriteLabels[i]=Button("Favorite "+(i+1),new Vector2(-675+i*235,113),new Vector2(225,44),()=>Use(slot));}
        Button("INVENTORY [ENTER]",new Vector2(500,113),new Vector2(200,44),()=>InventoryRadialMenu.Instance?.OpenFromButton());
        editLabel=Button("EDIT FAVORITES",new Vector2(735,113),new Vector2(220,44),()=>{editing=!editing;ShowFeedback("Choose a favorite slot, then select an owned power-up.");});
        skillStatus=Label(Rect(root.transform,"Upgrade status",new Vector2(.5f,0),new Vector2(150,113),new Vector2(375,44)),"",17);
        skillStatus.color=new Color(1f,.83f,.35f);
        var noticeRect=Rect(root.transform,"Controls pause notice",new Vector2(.5f,.5f),new Vector2(0,60),new Vector2(900,100));
        var noticeBg=noticeRect.gameObject.AddComponent<Image>(); noticeBg.color=new Color(.025f,.04f,.09f,.96f); noticeBg.raycastTarget=false;
        controlsNotice=Label(noticeRect,"PAUSED — INVENTORY & FAVORITES\nPress ENTER or ESC to return to play",26);
        noticeRect.gameObject.SetActive(false);
        feedback=Label(Rect(root.transform,"Feedback",new Vector2(.5f,1),new Vector2(0,-166),new Vector2(1300,30)),"",20);
        root.SetActive(false);
    }
}
