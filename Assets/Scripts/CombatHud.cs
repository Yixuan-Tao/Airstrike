using UnityEngine;
using UnityEngine.UI;

namespace Airstrike
{
    public sealed class CombatHud : MonoBehaviour
    {
        private AirstrikeGame game;
        private Font font;
        private Text wave, score, lives, status, briefing, overlayTitle, overlayBody;
        private RectTransform healthFill, crosshair;
        private GameObject overlay, banner;
        private readonly Color ink = new Color(0.055f,0.09f,0.12f,0.94f);
        private readonly Color cream = new Color(0.91f,0.91f,0.82f);
        private readonly Color amber = new Color(1,0.69f,0.29f);

        public void Initialize(AirstrikeGame owner)
        {
            game=owner;
            font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasGo=new GameObject("HUD canvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
            canvasGo.transform.SetParent(transform,false);
            var canvas=canvasGo.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=game.MainCamera;
            canvas.planeDistance=1; canvas.sortingOrder=20;
            var scaler=canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.matchWidthOrHeight=0.5f;
            var root=canvasGo.GetComponent<RectTransform>();
            var top=Panel("Command bar",root,new Vector2(0.5f,1),new Vector2(0,-54),new Vector2(1920,108),ink);
            Panel("Accent",top,new Vector2(0,0.5f),new Vector2(15,0),new Vector2(5,60),amber);
            Label(top,new Vector2(0,0.5f),new Vector2(220,10),new Vector2(360,45),"S K Y B R E A C H",31,cream);
            Label(top,new Vector2(0,0.5f),new Vector2(220,-24),new Vector2(360,24),"FIELD TEST 01   /   HOLD THE LINE",15,new Color(0.55f,0.67f,0.7f));
            wave=Label(top,new Vector2(0.5f,0.5f),new Vector2(-100,5),new Vector2(220,45),"WAVE 01",29,cream);
            briefing=Label(top,new Vector2(0.5f,0.5f),new Vector2(100,2),new Vector2(220,44),"03 INBOUND",18,amber);
            score=Label(top,new Vector2(1,0.5f),new Vector2(-135,10),new Vector2(220,45),"000000",31,cream);
            lives=Label(top,new Vector2(1,0.5f),new Vector2(-135,-24),new Vector2(220,24),"LIVES  3",16,amber);
            var bottom=Panel("Controls",root,new Vector2(0.5f,0),new Vector2(0,36),new Vector2(1920,72),ink);
            status=Label(bottom,new Vector2(0,0.5f),new Vector2(240,0),new Vector2(430,40),"ROCKET READY",21,amber);
            Label(bottom,new Vector2(1,0.5f),new Vector2(-530,0),new Vector2(1000,44),"A D  MOVE     SPACE  DOUBLE JUMP     LMB  FIRE     RMB  DIG     ESC  PAUSE",18,cream);
            var health=Panel("Health backing",root,new Vector2(0,0),new Vector2(134,105),new Vector2(210,14),ink);
            healthFill=Panel("Health",health,new Vector2(0,0.5f),new Vector2(105,0),new Vector2(206,10),amber);
            Label(root,new Vector2(0,0),new Vector2(117,133),new Vector2(175,24),"SUIT INTEGRITY",14,cream);
            Label(root,new Vector2(1,0),new Vector2(-170,111),new Vector2(280,40),"2.5D  /  PROTOTYPE",16,new Color(0.7f,0.76f,0.72f));
            banner=Panel("Wave banner",root,new Vector2(0.5f,0.5f),new Vector2(0,120),new Vector2(530,105),ink).gameObject;
            Label(banner.transform,new Vector2(0.5f,0.5f),new Vector2(0,15),new Vector2(490,45),"HOLD THE LINE",34,amber,TextAnchor.MiddleCenter);
            Label(banner.transform,new Vector2(0.5f,0.5f),new Vector2(0,-25),new Vector2(490,30),"ONE ROCKET IN FLIGHT. MAKE IT COUNT.",16,cream,TextAnchor.MiddleCenter);
            crosshair=Panel("Crosshair",root,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(28,28),Color.clear);
            Panel("Cross left",crosshair,new Vector2(0.5f,0.5f),new Vector2(-12,0),new Vector2(7,2),amber);
            Panel("Cross right",crosshair,new Vector2(0.5f,0.5f),new Vector2(12,0),new Vector2(7,2),amber);
            Panel("Cross top",crosshair,new Vector2(0.5f,0.5f),new Vector2(0,12),new Vector2(2,7),amber);
            Panel("Cross bottom",crosshair,new Vector2(0.5f,0.5f),new Vector2(0,-12),new Vector2(2,7),amber);
            Panel("Cross dot",crosshair,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(3,3),cream);
            overlay=Panel("Pause and results",root,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(750,330),ink).gameObject;
            overlayTitle=Label(overlay.transform,new Vector2(0.5f,0.5f),new Vector2(0,76),new Vector2(700,70),"PAUSED",48,amber,TextAnchor.MiddleCenter);
            overlayBody=Label(overlay.transform,new Vector2(0.5f,0.5f),new Vector2(0,-20),new Vector2(680,130),"",23,cream,TextAnchor.MiddleCenter);
            overlay.SetActive(false);
        }

        private void LateUpdate()
        {
            if(game==null || game.Player==null) return;
            wave.text="WAVE "+game.Wave.ToString("00");
            var threats=game.EnemiesPending+game.Aircraft.Count;
            briefing.text=threats>0?threats.ToString("00")+" INBOUND":"SECTOR CLEAR";
            score.text=game.Score.ToString("000000"); lives.text="LIVES  "+game.Lives;
            status.text=(game.RocketsAlive==0?"ROCKET READY":"ROCKET IN FLIGHT")+"    /    JUMPS "+game.Player.JumpsLeft;
            healthFill.sizeDelta=new Vector2(206*game.Player.Health/100f,10);
            healthFill.anchoredPosition=new Vector2(2+healthFill.sizeDelta.x*0.5f,0);
            banner.SetActive(game.Playing && Time.time<game.WaveBannerUntil);
            overlay.SetActive(game.Paused || game.GameOver);
            if(game.GameOver)
            {
                overlayTitle.text="SIGNAL LOST";
                overlayBody.text="WAVES HELD  "+(game.Wave-1)+"     /     KILLS  "+game.Kills+"\nSCORE  "+game.Score+"     /     BEST WAVE  "+game.BestWave+"\n\nR  REDEPLOY       Q  QUIT";
            }
            else if(game.Paused)
            {
                overlayTitle.text="STAND BY";
                overlayBody.text="ESC  RESUME\nR  REDEPLOY       Q  QUIT";
            }
            var p=game.MainCamera.WorldToViewportPoint(new Vector3(game.Controls.aim.x,game.Controls.aim.y,0));
            crosshair.anchorMin=crosshair.anchorMax=new Vector2(Mathf.Clamp(p.x,0.02f,0.98f),Mathf.Clamp(p.y,0.09f,0.88f));
            crosshair.anchoredPosition=Vector2.zero;
            crosshair.gameObject.SetActive(game.Playing);
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
