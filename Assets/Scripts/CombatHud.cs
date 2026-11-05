using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using static Airstrike.UiLanguage;

namespace Airstrike
{
    public sealed class CombatHud : MonoBehaviour
    {
        private AirstrikeGame game;
        private Font font;
        private Text wave, score, lives, status, briefing, overlayTitle, overlayBody, coverHint, damageDirection, upgrades, waveTitle, supplyHint;
        private RectTransform healthFill, crosshair;
        private GameObject overlay, banner, hitMarker;
        private RectTransform battleRoot, menuRoot;
        private Text edition, controls, healthLabel, bannerHint, menuTitle, menuHint, pcTitle, classicTitle, languageTitle, menuError;
        private string balanceError;
        internal Button PcEntry { get; private set; }
        internal Button ClassicEntry { get; private set; }
        internal Button LanguageButton { get; private set; }
        internal string VisibleMenuTitle => menuTitle.text;
        internal string VisibleWaveLabel => wave.text;
        internal Font CurrentFont => font;
        private Image[] reticleBars, hurtEdges;
        private readonly Color ink = new Color(0.055f,0.09f,0.12f,0.94f);
        private readonly Color cream = new Color(0.91f,0.91f,0.82f);
        private readonly Color amber = new Color(1,0.69f,0.29f);

        public void Initialize(AirstrikeGame owner)
        {
            game=owner;
            font=FontForLanguage();
            var canvasGo=new GameObject("HUD canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform,false);
            var canvas=canvasGo.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=game.MainCamera;
            canvas.planeDistance=1; canvas.sortingOrder=20;
            var scaler=canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=0.5f;
            var canvasRoot=canvasGo.GetComponent<RectTransform>();
            CreateMenu(canvasRoot);
            var root=Panel("Combat HUD",canvasRoot,new Vector2(0.5f,0.5f),Vector2.zero,Vector2.zero,Color.clear);
            root.anchorMin=Vector2.zero; root.anchorMax=Vector2.one;
            battleRoot=root;
            var top=Panel("Command bar",root,new Vector2(0.5f,1),new Vector2(0,-54),new Vector2(1920,108),ink);
            Panel("Accent",top,new Vector2(0,0.5f),new Vector2(15,0),new Vector2(5,60),amber);
            Label(top,new Vector2(0,0.5f),new Vector2(220,10),new Vector2(360,45),"S K Y B R E A C H",31,cream);
            edition=Label(top,new Vector2(0,0.5f),new Vector2(220,-24),new Vector2(360,24),"",15,new Color(0.55f,0.67f,0.7f));
            wave=Label(top,new Vector2(0.5f,0.5f),new Vector2(-100,5),new Vector2(220,45),"WAVE 01",29,cream);
            briefing=Label(top,new Vector2(0.5f,0.5f),new Vector2(100,2),new Vector2(220,44),"03 INBOUND",18,amber);
            score=Label(top,new Vector2(1,0.5f),new Vector2(-135,10),new Vector2(220,45),"000000",31,cream);
            lives=Label(top,new Vector2(1,0.5f),new Vector2(-135,-24),new Vector2(220,24),"LIVES  3",16,amber);
            var bottom=Panel("Controls",root,new Vector2(0.5f,0),new Vector2(0,36),new Vector2(1920,72),ink);
            status=Label(bottom,new Vector2(0,0.5f),new Vector2(240,0),new Vector2(430,40),"ROCKET READY",21,amber);
            controls=Label(bottom,new Vector2(1,0.5f),new Vector2(-530,0),new Vector2(1000,44),"",18,cream);
            var health=Panel("Health backing",root,new Vector2(0,0),new Vector2(134,105),new Vector2(210,14),ink);
            healthFill=Panel("Health",health,new Vector2(0,0.5f),new Vector2(105,0),new Vector2(206,10),amber);
            healthLabel=Label(root,new Vector2(0,0),new Vector2(117,133),new Vector2(175,24),"",14,cream);
            coverHint=Label(root,new Vector2(0,0),new Vector2(260,165),new Vector2(460,28),"",19,new Color(0.55f,0.88f,0.76f));
            upgrades=Label(root,new Vector2(1,0),new Vector2(-270,111),new Vector2(460,40),"ROCKET SLOTS 1 / STANDARD",16,new Color(0.7f,0.76f,0.72f));
            supplyHint=Label(root,new Vector2(0.5f,0),new Vector2(0,112),new Vector2(600,40),"",22,amber,TextAnchor.MiddleCenter);
            banner=Panel("Wave banner",root,new Vector2(0.5f,0.5f),new Vector2(0,120),new Vector2(530,105),ink).gameObject;
            waveTitle=Label(banner.transform,new Vector2(0.5f,0.5f),new Vector2(0,15),new Vector2(490,45),"FIRST CONTACT",34,amber,TextAnchor.MiddleCenter);
            bannerHint=Label(banner.transform,new Vector2(0.5f,0.5f),new Vector2(0,-25),new Vector2(490,30),"",16,cream,TextAnchor.MiddleCenter);
            crosshair=Panel("Crosshair",root,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(28,28),Color.clear);
            Panel("Cross left",crosshair,new Vector2(0.5f,0.5f),new Vector2(-12,0),new Vector2(7,2),amber);
            Panel("Cross right",crosshair,new Vector2(0.5f,0.5f),new Vector2(12,0),new Vector2(7,2),amber);
            Panel("Cross top",crosshair,new Vector2(0.5f,0.5f),new Vector2(0,12),new Vector2(2,7),amber);
            Panel("Cross bottom",crosshair,new Vector2(0.5f,0.5f),new Vector2(0,-12),new Vector2(2,7),amber);
            Panel("Cross dot",crosshair,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(3,3),cream);
            reticleBars=crosshair.GetComponentsInChildren<Image>();
            hitMarker=Panel("Confirmed hit",crosshair,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(18,18),Color.clear).gameObject;
            var slash=Panel("Hit slash",hitMarker.transform,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(18,3),cream);
            slash.localRotation=Quaternion.Euler(0,0,45);
            slash=Panel("Hit backslash",hitMarker.transform,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(18,3),cream);
            slash.localRotation=Quaternion.Euler(0,0,-45);
            damageDirection=Label(root,new Vector2(0.5f,0.5f),new Vector2(0,-90),new Vector2(500,40),"",22,new Color(1,0.42f,0.28f),TextAnchor.MiddleCenter);
            hurtEdges=new[] {
                Panel("Hurt left",root,new Vector2(0,0.5f),new Vector2(5,0),new Vector2(10,1080),Color.clear).GetComponent<Image>(),
                Panel("Hurt right",root,new Vector2(1,0.5f),new Vector2(-5,0),new Vector2(10,1080),Color.clear).GetComponent<Image>() };
            overlay=Panel("Pause and results",root,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(750,330),ink).gameObject;
            overlayTitle=Label(overlay.transform,new Vector2(0.5f,0.5f),new Vector2(0,76),new Vector2(700,70),"PAUSED",48,amber,TextAnchor.MiddleCenter);
            overlayBody=Label(overlay.transform,new Vector2(0.5f,0.5f),new Vector2(0,-20),new Vector2(680,130),"",23,cream,TextAnchor.MiddleCenter);
            overlay.SetActive(false);
            battleRoot.gameObject.SetActive(false);
            RefreshLanguage();
        }

        private void CreateMenu(RectTransform root)
        {
            if(EventSystem.current==null)
            {
                var events=new GameObject("Menu input",typeof(EventSystem),typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform,false);
            }
            var mid=new Vector2(0.5f,0.5f);
            menuRoot=Panel("Mode menu",root,mid,Vector2.zero,new Vector2(960,630),ink);
            Panel("Header accent",menuRoot,mid,new Vector2(0,296),new Vector2(902,4),amber);
            Label(menuRoot,mid,new Vector2(0,223),new Vector2(820,78),"S K Y B R E A C H",55,amber,TextAnchor.MiddleCenter);
            menuTitle=Label(menuRoot,mid,new Vector2(0,155),new Vector2(820,45),"",27,cream,TextAnchor.MiddleCenter);
            PcEntry=MenuButton("PC edition",65,new Vector2(760,76),()=>game.StartRun(BalanceMode.PC),out pcTitle);
            ClassicEntry=MenuButton("Classic rules",-27,new Vector2(760,76),()=>game.StartRun(BalanceMode.Classic),out classicTitle);
            menuHint=Label(menuRoot,mid,new Vector2(0,-110),new Vector2(820,54),"",20,cream,TextAnchor.MiddleCenter);
            LanguageButton=MenuButton("Language",-183,new Vector2(360,50),ToggleLanguage,out languageTitle);
            menuError=Label(menuRoot,mid,new Vector2(0,-251),new Vector2(850,90),"",17,amber,TextAnchor.MiddleCenter);
            menuError.horizontalOverflow=HorizontalWrapMode.Wrap;
        }

        private Button MenuButton(string name,float y,Vector2 size,UnityEngine.Events.UnityAction action,out Text title)
        {
            var rect=Panel(name,menuRoot,new Vector2(0.5f,0.5f),new Vector2(0,y),size,new Color(0.22f,0.32f,0.36f));
            var image=rect.GetComponent<Image>();
            image.raycastTarget=true;
            var button=rect.gameObject.AddComponent<Button>();
            button.targetGraphic=image;
            button.onClick.AddListener(action);
            title=Label(rect,new Vector2(0.5f,0.5f),Vector2.zero,size,"",25,cream,TextAnchor.MiddleCenter);
            return button;
        }

        public void EnterBattle()
        {
            menuRoot.gameObject.SetActive(false);
            battleRoot.gameObject.SetActive(true);
            RefreshLanguage();
        }

        public void ShowBalanceError(string message)
        {
            balanceError=message;
            RefreshLanguage();
        }

        public void ToggleLanguage() { UiLanguage.Toggle(); RefreshLanguage(); }

        private void RefreshLanguage()
        {
            font=FontForLanguage();
            foreach(var label in GetComponentsInChildren<Text>(true)) label.font=font;
            menuTitle.text=L("CHOOSE A RULESET","选择玩法规则");
            pcTitle.text="1   /   "+Mode(BalanceMode.PC);
            classicTitle.text="2   /   "+Mode(BalanceMode.Classic);
            menuHint.text=L("PC balance or classic Easy values adapted to this battlefield.","使用当前 PC 数值，或按原版简单模式数值开局。");
            languageTitle.text=L("F1   /   CHINESE","F1   /   ENGLISH");
            menuError.text=balanceError==null?"":L("Fix Balance.csv and select the mode again.\n","请修正 Balance.csv，再次选择入口。\n")+balanceError;
            if(game.RunStarted) edition.text=Mode(game.Rules.Mode);
            controls.text=L("A D  MOVE     SPACE  JUMP     LMB  FIRE     RMB  DIG     ESC  PAUSE     F1  LANGUAGE",
                "A D 移动     空格 跳跃     左键 开火     右键 挖掘     Esc 暂停     F1 语言");
            healthLabel.text=L("SUIT INTEGRITY","生命值");
            bannerHint.text=game.RunStarted && game.Rules.CoverDepth>0
                ?L("DIG FOR COVER. SOIL ABSORBS BLASTS.","挖掘掩体。土层能挡住爆炸。")
                :L("KEEP MOVING TO AVOID THE BOMBS.","注意走位，躲开炸弹。");
        }

        private static string L(string english,string chinese) => UiLanguage.Text(english,chinese);

        private void LateUpdate()
        {
            if(game==null || game.Player==null) return;
            wave.text=L("WAVE ","波次 ")+game.Wave.ToString("00");
            var threats=game.EnemiesPending+game.HostileAircraftCount;
            briefing.text=threats>0?threats.ToString("00")+L(" INBOUND"," 架敌机"):
                game.HostileProjectilesAlive>0?L("ORDNANCE INBOUND","弹药来袭"):L("SECTOR CLEAR","战区已清空");
            score.text=game.Rules.WaveScoreOnly?L("CLEARED ","已通关 ")+game.CompletedWaves:game.Score.ToString("000000");
            lives.text=L("LIVES  ","生命  ")+game.Lives;
            var jumps=game.MultiJumpEnabled?L("FREE","不限"):game.Player.JumpsLeft.ToString();
            status.text=(game.RocketsAlive<game.RocketCapacity?L("ROCKET READY","火箭就绪"):L("SLOTS FULL","火箭位已满"))+L("    /    JUMPS ","    /    跳跃 ")+jumps;
            if(game.RocketsAlive==0 && game.Player.BarrelBlocked) status.text=L("MUZZLE BLOCKED    /    JUMPS ","枪口受阻    /    跳跃 ")+jumps;
            upgrades.text=L("ROCKET SLOTS ","火箭位 ")+game.RocketsAlive+"/"+game.RocketCapacity+" / "
                +(game.PiercingEnabled?L("PIERCING","穿甲"):L("STANDARD","普通"))+(game.MultiJumpEnabled?L(" / BOOTS"," / 跳跳靴"):"");
            waveTitle.text=UiLanguage.Wave(game.WaveName);
            var warning=false;
            foreach(var aircraft in game.Aircraft) if(aircraft!=null && aircraft.IsTelegraphing) warning=true;
            supplyHint.text=warning?L("ROCKET LOCK  /  MOVE FROM THE LINE","火箭锁定  /  离开瞄准线"):Time.time<game.SupplyFeedbackUntil?game.SupplyMessage:
                PickupHint();
            coverHint.text=game.Playing && Time.time<game.CoverFeedbackUntil
                ? L("COVER ABSORBED ","掩体吸收 ")+Mathf.RoundToInt(game.LastCoverAbsorption*100)+"%"
                : game.Player.Protected && game.Player.Health>0 ? L("PROTECTED","保护中") : "";
            var hurt=Mathf.Clamp01((game.HurtFeedbackUntil-Time.time)/0.4f);
            foreach(var edge in hurtEdges) edge.color=new Color(1,0.24f,0.12f,hurt*0.85f);
            damageDirection.text=hurt>0 ? (game.LastBlastOrigin.x<game.Player.transform.position.x?L("<  BLAST FROM LEFT","<  左侧爆炸"):L("BLAST FROM RIGHT  >","右侧爆炸  >")) : "";
            healthFill.sizeDelta=new Vector2(206f*game.Player.Health/game.Rules.PlayerHealth,10);
            healthFill.anchoredPosition=new Vector2(2+healthFill.sizeDelta.x*0.5f,0);
            banner.SetActive(game.Playing && Time.time<game.WaveBannerUntil);
            overlay.SetActive(game.Paused || game.GameOver);
            if(game.GameOver)
            {
                overlayTitle.text=L("SIGNAL LOST","战斗结束");
                overlayBody.text=L("WAVES HELD  ","完成波次  ")+game.CompletedWaves+"     /     "+L("KILLS  ","击落  ")+game.Kills+
                    (game.Rules.WaveScoreOnly?"\n":"\n"+L("SCORE  ","得分  ")+game.Score+"     /     ")+L("BEST WAVE  ","最高波次  ")+game.BestWave+
                    L("\n\nR  REDEPLOY    M  MENU    Q  QUIT","\n\nR  重开    M  主菜单    Q  退出");
            }
            else if(game.Paused)
            {
                overlayTitle.text=L("STAND BY","已暂停");
                overlayBody.text=L("ESC  RESUME    F1  LANGUAGE\nR  REDEPLOY    M  MENU    Q  QUIT","Esc  继续    F1  语言\nR  重开    M  主菜单    Q  退出");
            }
            var p=game.MainCamera.WorldToViewportPoint(new Vector3(game.Controls.aim.x,game.Controls.aim.y,0));
            crosshair.anchorMin=crosshair.anchorMax=new Vector2(Mathf.Clamp(p.x,0.02f,0.98f),Mathf.Clamp(p.y,0.09f,0.88f));
            crosshair.anchoredPosition=Vector2.zero;
            crosshair.gameObject.SetActive(game.Playing);
            for(var i=1;i<reticleBars.Length;i++) reticleBars[i].color=game.Player.BarrelBlocked?new Color(1,0.35f,0.22f):amber;
            hitMarker.SetActive(game.Playing && Time.time<game.HitFeedbackUntil);
        }

        private string PickupHint()
        {
            var best=float.PositiveInfinity; var message="";
            foreach(var crate in game.Supplies)
            {
                if(crate==null) continue;
                var distance=Vector2.Distance(crate.transform.position,game.Player.Body.position);
                if(distance>=best) continue;
                best=distance; message=SupplyRules.Name(crate.Kind)+"  /  "+Mathf.CeilToInt(distance)+L("m TO PICKUP"," 米可拾取");
            }
            foreach(var pilot in game.Pilots)
            {
                if(pilot==null) continue;
                var distance=Vector2.Distance(pilot.transform.position,game.Player.Body.position);
                if(distance>=best) continue;
                best=distance; message=L("WOUNDED PILOT  /  ","受伤飞行员  /  ")+Mathf.CeilToInt(distance)+L("m TO RESCUE"," 米可救援");
            }
            if(message!="") return message;
            foreach(var aircraft in game.Aircraft)
                if(aircraft!=null && aircraft.IsSupplyTransport) return L("CARGO: ","补给机：")+SupplyRules.Name(aircraft.Cargo)+L("  /  SHOOT IT DOWN","  /  击落获取");
            return "";
        }

        private RectTransform Panel(string name,Transform parent,Vector2 anchor,Vector2 position,Vector2 size,Color color)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image));
            go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=anchor;
            rect.anchoredPosition=position; rect.sizeDelta=size;
            var image=go.GetComponent<Image>(); image.color=color; image.raycastTarget=false;
            return rect;
        }

        private Text Label(Transform parent,Vector2 anchor,Vector2 position,Vector2 size,string value,int fontSize,Color color,TextAnchor alignment=TextAnchor.MiddleLeft)
        {
            var go=new GameObject(value,typeof(RectTransform),typeof(Text)); go.transform.SetParent(parent,false);
            var rect=go.GetComponent<RectTransform>(); rect.anchorMin=rect.anchorMax=anchor; rect.anchoredPosition=position; rect.sizeDelta=size;
            var text=go.GetComponent<Text>(); text.font=font; text.text=value; text.fontSize=fontSize;
            text.color=color; text.alignment=alignment; text.raycastTarget=false;
            text.horizontalOverflow=HorizontalWrapMode.Overflow; text.verticalOverflow=VerticalWrapMode.Overflow;
            return text;
        }
    }
}
