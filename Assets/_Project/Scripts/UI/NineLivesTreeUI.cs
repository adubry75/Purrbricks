using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>Read-only tree presentation; NineLivesService owns purchases and safe return flow.</summary>
public class NineLivesTreeUI : MonoBehaviour
{
    private RectTransform _panel;
    private readonly Button[] _nodes = new Button[15];
    private readonly Image[] _nodeBgs = new Image[15];
    private readonly Text[] _nodeStates = new Text[15];
    private readonly Outline[] _nodeOutlines = new Outline[15];
    private Text _points, _progress, _equipped, _notice, _welcome, _detailTitle, _description, _requirements, _status;
    private Text _primaryLabel, _continueLabel;
    private Button _primary, _respec, _continue;
    private GameObject _skip, _starter;
    private readonly Image[] _starterBgs = new Image[3];
    private Text _totalEarned, _packedLunchEffect;
    private readonly GameObject[] _starterSelected = new GameObject[3];
    private RectTransform _xpFill;
    private int _selected = 10;
    private int _openedFrame;
    private string _feedback;
    private static readonly Color Ink = new Color(.035f, .05f, .095f, 1f);
    private static readonly Color Muted = new Color(.65f, .73f, .84f, 1f);
    private static readonly Color[] BranchColors = { new Color(1f,.55f,.24f), new Color(.39f,.87f,.91f), new Color(.70f,.60f,1f) };
    private static readonly string[] ShortEffects = {
        "Fury charges 20% faster", "Laser cooldown -25%", "Edge returns: +3% Fury", "Last bricks: faster Fury", "Fury arms a Fireball return",
        "Good timers +25%", "Choose a starter", "Repeat pickups +3 sec", "New clones get one safety bounce", "Multiball + 10 sec Fireball",
        "Paddle width +12%", "Fresh starts: 4 lives", "Curse timers -20%", "Shield after losing a life", "Rescue the last ball once"
    };
    private static readonly int[] IconTypes = {3,5,9,7,6,10,0,2,1,1,0,4,8,8,4};
    private static string Id(int index) => ((char)('A' + index / 5)).ToString() + (index % 5 + 1);

    private void Awake() { BuildUI(); }
    public void Show(Action onClose)
    {
        BuildUI();
        gameObject.SetActive(true);
        _openedFrame = Time.frameCount;
        _feedback = null;
        if (NineLivesService.Instance != null && NineLivesService.Instance.PendingIntroduction) _selected = 10;
        Refresh();
        UINavController.SetDefault(_nodes[_selected].gameObject);
    }
    public void Hide() { gameObject.SetActive(false); }
    private void OnEnable()
    {
        if (NineLivesService.Instance != null) NineLivesService.Instance.Changed += Refresh;
        if (InputManager.Actions != null) InputManager.Actions.UI.CancelUI.performed += Cancel;
    }
    private void OnDisable()
    {
        if (NineLivesService.Instance != null) NineLivesService.Instance.Changed -= Refresh;
        if (InputManager.Actions != null) InputManager.Actions.UI.CancelUI.performed -= Cancel;
    }
    private void Cancel(InputAction.CallbackContext _) { if (Time.frameCount > _openedFrame) NineLivesService.Instance?.CloseTree(); }
    private void Update()
    {
        if (Time.frameCount <= _openedFrame) return;
        var key = Keyboard.current;
        if (key == null) return;
        int selected = _selected;
        if (key.leftArrowKey.wasPressedThisFrame || key.aKey.wasPressedThisFrame) selected = (_selected + 10) % 15;
        if (key.rightArrowKey.wasPressedThisFrame || key.dKey.wasPressedThisFrame) selected = (_selected + 5) % 15;
        if (key.upArrowKey.wasPressedThisFrame || key.wKey.wasPressedThisFrame) selected = (_selected / 5)*5 + (_selected % 5 + 4)%5;
        if (key.downArrowKey.wasPressedThisFrame || key.sKey.wasPressedThisFrame) selected = (_selected / 5)*5 + (_selected % 5 + 1)%5;
        if (selected != _selected) Select(selected);
        if (key.enterKey.wasPressedThisFrame || key.numpadEnterKey.wasPressedThisFrame) ActivateSelected();
        if (key.rKey.wasPressedThisFrame) Refund();
        if (key.spaceKey.wasPressedThisFrame) NineLivesService.Instance?.CloseTree();
        if (_starter != null && _starter.activeSelf)
        {
            if (key.digit1Key.wasPressedThisFrame) NineLivesService.Instance?.ChooseStarter(0);
            if (key.digit2Key.wasPressedThisFrame) NineLivesService.Instance?.ChooseStarter(2);
            if (key.digit3Key.wasPressedThisFrame) NineLivesService.Instance?.ChooseStarter(5);
        }
    }
    private void Select(int index) { _selected = index; _feedback = null; Refresh(); }
    private void ActivateSelected()
    {
        var service = NineLivesService.Instance;
        if (service == null) return;
        string id = Id(_selected);
        if (service.Model.Has(id))
        {
            if (NineLivesCatalog.Find(id).IsCapstone) service.Equip(id);
            return;
        }
        if (service.Model.CanPurchase(id, out string reason))
        {
            service.Buy(id);
            _feedback = "Purchased: " + NineLivesCatalog.Find(id).Name + ". Applies on your next attempt.";
        }
        else _feedback = reason;
        Refresh();
    }
    private void Refund()
    {
        if (NineLivesService.Instance == null) return;
        NineLivesService.Instance.Respec();
        _feedback = "Points refunded. XP and total skill points earned are unchanged.";
        Refresh();
    }

    private void BuildUI()
    {
        if (_panel != null) return;
        var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 700;
        var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        gameObject.AddComponent<GraphicRaycaster>();
        var backdrop = Box(transform,"Backdrop",Vector2.zero,new Vector2(1920,1080),Ink);
        backdrop.raycastTarget = true;
        backdrop.rectTransform.anchorMin = Vector2.zero; backdrop.rectTransform.anchorMax = Vector2.one; backdrop.rectTransform.sizeDelta = Vector2.zero;
        _panel = Rect(transform,"TreePanel",Vector2.zero,new Vector2(1800,1000));
        Label(_panel,"NINE LIVES",new Vector2(-590,440),new Vector2(610,80),68,Color.white,TextAnchor.MiddleLeft,true);
        _welcome=Label(_panel,"SMALL UPGRADES. BIG COMEBACKS.",new Vector2(-375,377),new Vector2(1040,38),22,Muted,TextAnchor.MiddleLeft);
        FitText(_welcome,18);
        _points = Label(_panel,"",new Vector2(620,452),new Vector2(500,48),30,UIStyle.AccentGold,TextAnchor.MiddleRight,true);
        _totalEarned = Label(_panel,"",new Vector2(620,412),new Vector2(500,32),22,Muted,TextAnchor.MiddleRight);
        _equipped = Label(_panel,"",new Vector2(620,377),new Vector2(500,30),20,Muted,TextAnchor.MiddleRight);
        _progress = Label(_panel,"",new Vector2(-260,333),new Vector2(1250,52),21,Muted,TextAnchor.MiddleLeft);
        FitText(_progress,19);
        var track = Box(_panel,"XPTrack",new Vector2(640,339),new Vector2(430,10),new Color(.12f,.17f,.25f));
        _xpFill = Box(track.transform,"XPFill",Vector2.zero,Vector2.zero,UIStyle.AccentGold).rectTransform;
        _xpFill.anchorMin = Vector2.zero; _xpFill.anchorMax = Vector2.one; _xpFill.offsetMin = _xpFill.offsetMax = Vector2.zero;
        string[] branches = { "POUNCE", "PLAY", "SURVIVE" };
        for (int branch=0; branch<3; branch++)
        {
            float x = -690 + branch*425;
            Color color = BranchColors[branch];
            Label(_panel,branches[branch],new Vector2(x,280),new Vector2(360,36),29,color,TextAnchor.MiddleCenter,true);
            var locations = new[] { new Vector2(x,209), new Vector2(x-100,67), new Vector2(x+100,67), new Vector2(x,-84), new Vector2(x,-244) };
            Link(locations[0], locations[1], color); Link(locations[0], locations[2], color);
            Link(locations[1], locations[3], color); Link(locations[2], locations[3], color); Link(locations[3], locations[4], color);
            for (int n=0;n<5;n++)
            {
                int index=branch*5+n;
                Vector2 size = n==1 || n==2 ? new Vector2(184,106) : new Vector2(360,n==4?104:86);
                var node = NineLivesCatalog.Find(Id(index));
                var image = Box(_panel,"Node_"+Id(index),locations[n],size,new Color(.08f,.11f,.18f));
                image.raycastTarget = true;
                var button=image.gameObject.AddComponent<Button>(); button.targetGraphic=image;
                button.onClick.AddListener(()=>Select(index));
                var outline=image.gameObject.AddComponent<Outline>(); outline.effectDistance=new Vector2(2,-2); outline.effectColor=color;
                _nodes[index]=button; _nodeBgs[index]=image; _nodeOutlines[index]=outline;
                bool split=n==1||n==2;
                var icon=Box(image.transform,"Icon",new Vector2(-size.x/2+19,0),new Vector2(26,26),Color.white);
                icon.sprite=PowerupIconRegistry.Instance?.GetIcon((PowerupType)IconTypes[index]); icon.preserveAspect=true;
                if(icon.sprite==null) { icon.color=Color.clear; Label(icon.transform,(n+1).ToString(),Vector2.zero,new Vector2(26,26),23,color,TextAnchor.MiddleCenter,true); }
                var nameLabel=Label(image.transform,node.Name,new Vector2(15,split?31:19),new Vector2(size.x-48,30),split?20:25,Color.white,TextAnchor.MiddleCenter,true);
                FitText(nameLabel,split?18:21);
                var effectLabel=Label(image.transform,ShortEffects[index],new Vector2(15,split?-7:-10),new Vector2(size.x-46,split?38:24),split?17:19,Muted,TextAnchor.MiddleCenter);
                FitText(effectLabel,split?16:17);
                if(index==6) _packedLunchEffect=effectLabel;
                _nodeStates[index]=Label(image.transform,"",new Vector2(0,split?-39:-size.y/2+10),new Vector2(size.x-18,16),14,color,TextAnchor.MiddleCenter,true);
            }
        }
        var detail=Box(_panel,"DetailPane",new Vector2(642,5),new Vector2(442,586),new Color(.065f,.085f,.14f));
        Label(detail.transform,"SELECTED UPGRADE",new Vector2(0,261),new Vector2(390,30),20,Muted,TextAnchor.MiddleLeft,true);
        _detailTitle=Label(detail.transform,"",new Vector2(0,212),new Vector2(390,72),34,Color.white,TextAnchor.MiddleLeft,true);
        _description=Label(detail.transform,"",new Vector2(0,75),new Vector2(390,190),24,Color.white,TextAnchor.UpperLeft);
        _requirements=Label(detail.transform,"",new Vector2(0,-74),new Vector2(390,104),19,Muted,TextAnchor.UpperLeft);
        _status=Label(detail.transform,"",new Vector2(0,-157),new Vector2(390,62),21,UIStyle.AccentGold,TextAnchor.MiddleLeft);
        FitText(_detailTitle,28);
        FitText(_description,21);
        FitText(_requirements,16);
        FitText(_status,17);
        _primary=UIStyle.CreateButton(detail.transform,"Buy - 1 point",new Vector2(0,-242),new Vector2(382,66),ActivateSelected,UIStyle.AccentGold);
        _primaryLabel=_primary.GetComponentInChildren<Text>();
        _starter=Rect(_panel,"StarterChoice",new Vector2(-260,-357),new Vector2(1230,64)).gameObject;
        Label(_starter.transform,"PACKED LUNCH",new Vector2(-472,0),new Vector2(285,40),23,Muted,TextAnchor.MiddleLeft,true);
        string[] starterNames={"1  Wide Paddle","2  Sticky Ball","3  Laser"}; int[] starterTypes={0,2,5};
        for(int i=0;i<3;i++)
        {
            int type=starterTypes[i];
            var b=UIStyle.CreateButton(_starter.transform,starterNames[i],new Vector2(-160+i*260,0),new Vector2(245,54),()=>NineLivesService.Instance?.ChooseStarter(type),BranchColors[1]);
            _starterBgs[i]=b.GetComponent<Image>();
            _starterSelected[i]=Label(b.transform,"SELECTED",new Vector2(0,36),new Vector2(245,18),15,Color.white,TextAnchor.MiddleCenter,true).gameObject;
        }
        _notice=Label(_panel,"",new Vector2(-5,-351),new Vector2(1770,52),24,UIStyle.AccentGold,TextAnchor.MiddleCenter);
        _respec=UIStyle.CreateButton(_panel,"Free respec [R]",new Vector2(-675,-432),new Vector2(400,64),Refund,UIStyle.AccentBlue);
        _continue=UIStyle.CreateButton(_panel,"Back [Space]",new Vector2(640,-432),new Vector2(450,64),()=>NineLivesService.Instance?.CloseTree(),UIStyle.AccentGreen);
        _continueLabel=_continue.GetComponentInChildren<Text>();
        _skip=UIStyle.CreateButton(_panel,"Skip for now",new Vector2(110,-432),new Vector2(330,64),()=>NineLivesService.Instance?.CloseTree(),UIStyle.AccentBlue).gameObject;
        foreach (var button in GetComponentsInChildren<Button>(true))
            button.navigation = new Navigation { mode = Navigation.Mode.None };
        Label(_panel,"Arrow keys: select  |  Enter: buy / equip  |  Esc: close  |  Changes apply next attempt",new Vector2(0,-486),new Vector2(1760,28),20,Muted,TextAnchor.MiddleCenter);
    }
    private void Link(Vector2 from,Vector2 to,Color color)
    {
        var delta=to-from;
        var line=Box(_panel,"Prerequisite",(from+to)/2,new Vector2(delta.magnitude,3),new Color(color.r,color.g,color.b,.35f));
        line.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
    }
    private void Refresh()
    {
        var service=NineLivesService.Instance;
        if (_panel==null || service==null) return;
        var model=service.Model;
        _points.text=model.UnspentPoints+" SKILL POINT"+(model.UnspentPoints==1?"":"S")+" AVAILABLE";
        _totalEarned.text=$"Total skill points earned: {model.LifetimePoints} / 15";
        _progress.text=model.LifetimePoints>=15 ? "ALL 15 POINTS EARNED - spent points count toward total earned." : model.IntroGranted ? $"NEXT POINT  {model.XpTowardNextPoint:N0} / {model.NextPointCost:N0} XP  -  Play to earn XP and new points.\nTotal earned includes spent points." : "Clear the first campaign level to earn your first point.";
        _xpFill.anchorMax=new Vector2(model.NextPointCost>0?Mathf.Clamp01((float)model.XpTowardNextPoint/model.NextPointCost):1,1);
        var cap=NineLivesCatalog.Find(model.EquippedCapstone);
        _equipped.text="CAPSTONE  "+(cap!=null?cap.Name:"None equipped");
        for(int i=0;i<15;i++)
        {
            string id=Id(i); bool owned=model.Has(id); bool available=model.CanPurchase(id,out _);
            Color color=BranchColors[i/5];
            _nodeBgs[i].color=owned?new Color(color.r*.23f,color.g*.23f,color.b*.23f):new Color(.07f,.10f,.16f);
            _nodeOutlines[i].effectColor=i==_selected?Color.white:new Color(color.r,color.g,color.b,(owned||available)? .85f : .22f);
            _nodeOutlines[i].effectDistance=i==_selected?new Vector2(3,-3):new Vector2(1,-1);
            _nodeStates[i].text=model.EquippedCapstone==id?"EQUIPPED":owned?"PURCHASED":available?"1 POINT":i%5==4?$"CAPSTONE - {Mathf.Min(model.LifetimePoints,8)}/8 POINTS EARNED":"LOCKED";
        }
        string selected=Id(_selected); var node=NineLivesCatalog.Find(selected);
        bool purchased=model.Has(selected); bool canBuy=model.CanPurchase(selected,out string reason);
        _detailTitle.text=node.Name;
        _description.text=node.Description+(selected=="B2"&&purchased?"\n\nSelected starter: "+StarterName(model.StarterPowerup)+".":"");
        _packedLunchEffect.text=model.Has("B2")?"Starter: "+StarterName(model.StarterPowerup):ShortEffects[6];
        _requirements.text=Prerequisites(selected,model.LifetimePoints)+"\n"+(node.IsCapstone?"Only one capstone can be equipped.":"Purchased upgrades are always active.");
        _status.text=_feedback ?? (purchased?(node.IsCapstone?(model.EquippedCapstone==selected?"Equipped for your next attempt":"Purchased. Select Equip to use this capstone."):"Purchased - active next attempt"):canBuy?"Available - costs 1 skill point":reason);
        _primary.interactable=canBuy || (purchased&&node.IsCapstone&&model.EquippedCapstone!=selected);
        _primaryLabel.text=purchased?(node.IsCapstone?(model.EquippedCapstone==selected?"EQUIPPED":"EQUIP CAPSTONE"):"PURCHASED"):"BUY - 1 POINT";
        _respec.interactable=model.PurchasedMask!=0;
        bool intro=service.PendingIntroduction;
        bool continueToLevelTwo=intro&&GameManager.Instance!=null&&GameManager.Instance.State==GameState.Victory;
        _continueLabel.text=continueToLevelTwo?"CONTINUE TO LEVEL 2":"BACK [SPACE]";
        _skip.SetActive(intro&&model.UnspentPoints>0);
        _starter.SetActive(model.Has("B2")&&!intro);
        int[] types={0,2,5};
        for(int i=0;i<3;i++)
        {
            bool chosen=model.StarterPowerup==types[i];
            _starterBgs[i].color=chosen?new Color(.45f,1f,1f):Color.white;
            _starterSelected[i].SetActive(chosen);
        }
        _notice.gameObject.SetActive(!_starter.activeSelf);
        _welcome.text=!string.IsNullOrEmpty(service.WelcomeMessage)?service.WelcomeMessage:"SMALL UPGRADES. BIG COMEBACKS.";
        _welcome.color=!string.IsNullOrEmpty(service.WelcomeMessage)?UIStyle.AccentGold:Muted;
        _notice.text=intro?(model.PurchasedMask==0?"Your first point is free. Helping Paw is a friendly first choice.":continueToLevelTwo?"Your upgrade is ready. Try it on level two!":"Your upgrade is ready for your next campaign attempt."):"Every node costs 1 point. Free respecs keep all earned XP.";
    }
    private static string StarterName(int type) => type==2?"Sticky Ball":type==5?"Laser":"Wide Paddle";
    private static string Prerequisites(string id,int earned)
    {
        char branch=id[0]; int n=id[1]-'0';
        if(n==1) return "ROOT - no prerequisite";
        if(n==2||n==3) return "Requires "+NineLivesCatalog.Find(branch+"1").Name;
        if(n==4) return "Requires "+NineLivesCatalog.Find(branch+"2").Name+" + "+NineLivesCatalog.Find(branch+"3").Name;
        return "Requires "+NineLivesCatalog.Find(branch+"4").Name+$"\nTotal skill points earned: {earned}/8 (spent points count).";
    }
    private static RectTransform Rect(Transform parent,string name,Vector2 pos,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
        var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.sizeDelta=size;rt.anchoredPosition=pos;return rt;
    }
    private static Image Box(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
    { var rt=Rect(parent,name,pos,size);var image=rt.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=false;return image; }
    private static void FitText(Text text,int minSize)
    {
        text.resizeTextForBestFit=true;
        text.resizeTextMinSize=minSize;
        text.resizeTextMaxSize=text.fontSize;
    }
    private static Text Label(Transform parent,string value,Vector2 pos,Vector2 size,int font,Color color,TextAnchor alignment,bool bold=false)
    {
        var rt=Rect(parent,"Label",pos,size);var t=rt.gameObject.AddComponent<Text>();t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.text=value;t.fontSize=font;t.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;t.color=color;t.alignment=alignment;t.raycastTarget=false;
        t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Truncate;return t;
    }

    public static RectTransform CreateProgressBar(Transform parent, Vector2 position, Vector2 size)
    {
        var track=Box(parent,"NineLivesXPTrack",position,size,new Color(.14f,.19f,.27f));
        var fill=Box(track.transform,"NineLivesXPFill",Vector2.zero,Vector2.zero,UIStyle.AccentGold).rectTransform;
        fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
        return fill;
    }
    public static void RefreshProgressBar(RectTransform fill)
    {
        var m=NineLivesService.Instance?.Model;if(fill==null||m==null)return;
        fill.anchorMax=new Vector2(!m.IntroGranted?0:m.NextPointCost>0?Mathf.Clamp01((float)m.XpTowardNextPoint/m.NextPointCost):1,1);
    }
    public static string ProgressSummary(bool includeAttempt)
    {
        var service=NineLivesService.Instance;if(service==null)return "";
        var m=service.Model;
        if(!m.IntroGranted)return "First campaign clear unlocks a free skill point.";
        string progress=m.LifetimePoints>=15?"All skill points earned":$"Next point: {m.XpTowardNextPoint:N0} / {m.NextPointCost:N0} XP";
        return (includeAttempt?$"+{service.LastAttemptXp:N0} XP this attempt\n<size=20>{service.LastAttemptXpBreakdown}</size>\n":"")+progress;
    }
    public static string EntryLabel()
    { int points=NineLivesService.Instance?.Model.UnspentPoints??0;return points>0?$"NINE LIVES  [{points}]":"NINE LIVES"; }
}
