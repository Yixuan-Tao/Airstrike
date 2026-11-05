using UnityEngine;

namespace Airstrike
{
    public sealed class EnemyAircraft : MonoBehaviour
    {
        public float Speed = 5;
        public bool AttacksEnabled = true;
        public AircraftRole Role { get; private set; }
        public int SpawnWave { get; private set; }
        public int Health { get; private set; }
        public bool Armored { get; private set; }
        public int AttackRank { get; private set; }
        public bool IsSupplyTransport => Role==AircraftRole.SupplyTransport;
        public SupplyKind Cargo { get; private set; }
        public int NormalBombsReleased { get; private set; }
        public float LastNormalBombAt { get; private set; }
        public Vector2 LastNormalBombPosition { get; private set; }
        internal float BombTimeRemaining => bombTimeLeft;
        public bool IsTelegraphing => warningUntil > 0;
        public Vector2 LockedAim { get; private set; }
        public int ScoreValue => Role==AircraftRole.Helicopter?175:Role==AircraftRole.FastBomber?125:Armored?150:100;
        private AirstrikeGame game;
        private int direction, burstLeft;
        private float altitude, weaponAt, phase, warningUntil, nextBurst, age;
        private bool dead, tintActive, falling;
        private float bombTimeLeft, crashAge;
        private Vector2 crashVelocity;
        private Transform[] crashSmoke;
        private Renderer[] modelRenderers;
        private MaterialPropertyBlock hitProperties;
        private float hitUntil;
        private Transform model, rotor;
        private LineRenderer warningLine;

        public void Initialize(AirstrikeGame owner,int travelDirection,int wave,AircraftRole role,bool armored,int attackRank)
        {
            game=owner; direction=travelDirection; SpawnWave=wave; Role=role; Armored=armored; AttackRank=attackRank;
            altitude=transform.position.y; phase=Random.Range(0,6.28f);
            Health=armored?180:role==AircraftRole.Helicopter?130:role==AircraftRole.FastBomber?80:100;
            Speed=IsSupplyTransport?5.8f:role==AircraftRole.FastBomber?8.2f+Mathf.Min(wave,10)*0.35f:
                role==AircraftRole.Helicopter?3.4f+Mathf.Min(wave,8)*0.1f:4.2f+Mathf.Min(wave,10)*0.45f;
            if(role==AircraftRole.Bomber) bombTimeLeft=NormalBombInterval();
            weaponAt=Time.time+(role==AircraftRole.FastBomber?0.35f:0.7f);
            gameObject.layer=GameArt.EnemyLayer;
            var box=gameObject.AddComponent<BoxCollider>();
            box.size=role==AircraftRole.Helicopter?new Vector3(3.4f,1.2f,2):new Vector3(3.3f,1.2f,2.4f);
            model=new GameObject("Aircraft model").transform; model.SetParent(transform,false);
            if(direction<0) model.localRotation=Quaternion.Euler(0,180,0);
            if(role==AircraftRole.Helicopter) rotor=GameArt.Helicopter(model,game.Palette[4],game.Palette[3],game.Palette[7]);
            else if(role==AircraftRole.FastBomber) GameArt.FastJet(model,game.Palette[5],game.Palette[3],game.Palette[7]);
            else if(IsSupplyTransport)
            {
                GameArt.Aircraft(model,game.Palette[18],game.Palette[3],game.Palette[7]);
                GameArt.Shape("Cargo hold",PrimitiveType.Cube,model,new Vector3(-0.2f,-0.28f,0),new Vector3(1.3f,0.55f,0.9f),game.Palette[3]);
            }
            else GameArt.Aircraft(model,game.Palette[4],game.Palette[5],game.Palette[7]);
            if(armored) GameArt.Shape("Armor band",PrimitiveType.Cube,model,Vector3.zero,new Vector3(0.35f,0.65f,0.7f),game.Palette[3]);
            modelRenderers=model.GetComponentsInChildren<Renderer>(); hitProperties=new MaterialPropertyBlock();
            if(role==AircraftRole.Helicopter)
            {
                warningLine=new GameObject("Rocket warning").AddComponent<LineRenderer>();
                warningLine.transform.SetParent(transform,false); warningLine.positionCount=2;
                warningLine.useWorldSpace=true;
                warningLine.sharedMaterial=game.Palette[8]; warningLine.widthMultiplier=0.04f;
                warningLine.enabled=false;
            }
        }

        private void Update()
        {
            if(game==null || !game.Playing) return;
            if(falling) { UpdateCrash(); return; }
            if(dead) return;
            age+=Time.deltaTime;
            if(IsSupplyTransport && age>14) { Escape(); return; }
            if(tintActive && Time.time>=hitUntil)
            {
                foreach(var renderer in modelRenderers) renderer.SetPropertyBlock(null);
                tintActive=false;
            }
            var p=transform.position;
            if(Role==AircraftRole.Helicopter)
            {
                if(Speed>0) p.x=Mathf.MoveTowards(p.x,Mathf.Clamp(game.Player.Body.position.x-direction*5,-27,27),Speed*Time.deltaTime);
                if(rotor!=null) rotor.Rotate(Vector3.up,900*Time.deltaTime,Space.Self);
            }
            else
            {
                p.x+=direction*Speed*Time.deltaTime;
                if(IsSupplyTransport && Mathf.Abs(p.x)>36) { Escape(); return; }
                if(p.x>36) p.x=-36;
                if(p.x<-36) p.x=36;
            }
            if(Speed>0) p.y=altitude+Mathf.Sin(Time.time*0.85f+phase)*0.32f;
            transform.position=p;
            if(IsSupplyTransport) return;
            if(!AttacksEnabled || game.Player.Health<=0)
            {
                warningUntil=0; burstLeft=0;
                if(warningLine!=null) warningLine.enabled=false;
                return;
            }
            if(Role==AircraftRole.Helicopter) UpdateHelicopter(p);
            else if(Role==AircraftRole.FastBomber) UpdateFastBomber(p);
            else if(p.x>=-29 && p.x<=29)
            {
                bombTimeLeft-=Time.deltaTime;
                if(bombTimeLeft>0) return;
                bombTimeLeft=NormalBombInterval();
                LastNormalBombAt=Time.time; LastNormalBombPosition=(Vector2)p+Vector2.down*0.75f;
                NormalBombsReleased++;
                game.DropBomb(LastNormalBombPosition,Vector2.down);
            }
        }

        private float NormalBombInterval()
        {
            return (Random.Range(0,160 >> AttackRank)*2+1)/25f;
        }

        private void UpdateFastBomber(Vector3 p)
        {
            var ahead=(game.Player.Body.position.x-p.x)*direction;
            if(burstLeft==0 && Time.time>=weaponAt && ahead>0.4f && ahead<7)
            {
                burstLeft=3; nextBurst=Time.time;
                weaponAt=Time.time+3.8f;
            }
            if(burstLeft>0 && Time.time>=nextBurst)
            {
                game.DropBomb((Vector2)p+Vector2.down*0.7f,new Vector2(direction*Speed*0.25f,-2.5f));
                burstLeft--; nextBurst=Time.time+0.16f;
            }
        }

        private void UpdateHelicopter(Vector3 p)
        {
            var desired=Mathf.Clamp(game.Player.Body.position.x-direction*5,-27,27);
            if(warningUntil<=0 && Time.time>=weaponAt && Mathf.Abs(p.x-desired)<1.2f)
            {
                LockedAim=(Vector2)game.Player.Body.position+Vector2.up*0.08f;
                warningUntil=Time.time+0.85f;
                game.HelicopterWarnings++;
            }
            if(warningUntil<=0) return;
            var muzzle=(Vector2)p+new Vector2(direction*0.8f,-0.5f);
            warningLine.enabled=true;
            warningLine.SetPosition(0,new Vector3(muzzle.x,muzzle.y,-0.3f));
            warningLine.SetPosition(1,new Vector3(LockedAim.x,LockedAim.y,-0.3f));
            warningLine.widthMultiplier=0.035f+Mathf.PingPong(Time.time*0.12f,0.035f);
            if(Time.time<warningUntil) return;
            game.LaunchEnemyRocket(muzzle,LockedAim);
            warningUntil=0; warningLine.enabled=false;
            weaponAt=Time.time+Mathf.Max(2.4f,3.5f-game.Wave*0.08f);
        }

        public void SetCargo(SupplyKind kind)
        {
            Cargo=kind;
            var marker=new GameObject("Cargo marking").transform; marker.SetParent(model,false);
            marker.localPosition=new Vector3(0,-0.2f,-0.6f); marker.localScale=Vector3.one*1.4f;
            GameArt.SupplyIcon(marker,kind,game.Palette[7]);
        }

        public void Escape()
        {
            if(dead) return;
            dead=true; game.Aircraft.Remove(this);
            if(IsSupplyTransport) game.TransportsEscaped++;
            GetComponent<Collider>().enabled=false; Destroy(gameObject);
        }

        public void Damage(int amount,bool directCombatKill=true)
        {
            if(dead || amount<=0) return;
            game.NotifyAircraftHit(); hitUntil=Time.time+0.12f; tintActive=true;
            hitProperties.SetColor("_BaseColor",new Color(1,0.93f,0.75f));
            foreach(var renderer in modelRenderers) renderer.SetPropertyBlock(hitProperties);
            Health-=amount;
            if(Health>0) return;
            dead=true; warningUntil=0;
            if(warningLine!=null) warningLine.enabled=false;
            GetComponent<Collider>().enabled=false;
            if(!game.AircraftDestroyed(this,directCombatKill)) { Destroy(gameObject); return; }
            falling=true;
            crashVelocity=new Vector2(direction*Mathf.Min(Speed*0.35f,3),-1.5f);
            var charred=new MaterialPropertyBlock();
            charred.SetColor("_BaseColor",new Color(0.18f,0.20f,0.22f));
            foreach(var renderer in modelRenderers) renderer.SetPropertyBlock(charred);
            crashSmoke=new Transform[3];
            for(var i=0;i<crashSmoke.Length;i++)
                crashSmoke[i]=GameArt.Shape("Crash smoke",PrimitiveType.Sphere,transform,Vector3.zero,Vector3.one,game.Palette[17]).transform;
        }

        private void UpdateCrash()
        {
            var dt=Time.deltaTime;
            crashAge+=dt;
            crashVelocity.y-=12*dt;
            var previous=(Vector2)transform.position;
            var next=previous+crashVelocity*dt;
            var belly=Vector2.down*0.45f;
            var landed=Physics.Linecast(previous+belly,next+belly,out var hit,1<<GameArt.TerrainLayer,QueryTriggerInteraction.Ignore);
            if(landed || crashAge>3 || Mathf.Abs(next.x)>32)
            {
                falling=false;
                game.FinishCrash(landed?(Vector2?)hit.point:null);
                Destroy(gameObject);
                return;
            }
            transform.position=next;
            model.Rotate(Vector3.forward,-direction*145*dt,Space.World);
            for(var i=0;i<crashSmoke.Length;i++)
            {
                var lag=(i+1)*0.085f;
                crashSmoke[i].localPosition=-(Vector3)crashVelocity*lag+new Vector3(0,0,-0.1f*i);
                crashSmoke[i].localScale=Vector3.one*(0.35f+i*0.21f);
            }
        }
    }
}
