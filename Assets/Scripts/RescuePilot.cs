using UnityEngine;

namespace Airstrike
{
    public sealed class RescuePilot : MonoBehaviour
    {
        public bool Landed { get; private set; }
        public bool Finished { get; private set; }
        private AirstrikeGame game;
        private Transform parachute, model;
        private float age;
        private int direction;

        public void Initialize(AirstrikeGame owner)
        {
            game=owner; direction=Random.value<0.5f?-1:1;
            model=new GameObject("Wounded pilot model").transform; model.SetParent(transform,false);
            GameArt.Shape("Flight suit",PrimitiveType.Capsule,model,Vector3.zero,new Vector3(0.38f,0.32f,0.32f),game.Palette[18]);
            GameArt.Shape("Pilot helmet",PrimitiveType.Sphere,model,new Vector3(0,0.4f,0),Vector3.one*0.3f,game.Palette[4]);
            GameArt.Shape("Pilot visor",PrimitiveType.Cube,model,new Vector3(0,0.42f,-0.15f),new Vector3(0.23f,0.1f,0.04f),game.Palette[7]);
            for(var i=-1;i<=1;i+=2)
                GameArt.Shape("Pilot boot",PrimitiveType.Cube,model,new Vector3(i*0.12f,-0.35f,0),new Vector3(0.14f,0.18f,0.24f),game.Palette[7]);
            GameArt.Shape("Rescue cross horizontal",PrimitiveType.Cube,model,new Vector3(0,0.04f,-0.18f),new Vector3(0.25f,0.07f,0.03f),game.Palette[5]);
            GameArt.Shape("Rescue cross vertical",PrimitiveType.Cube,model,new Vector3(0,0.04f,-0.18f),new Vector3(0.07f,0.25f,0.03f),game.Palette[5]);
            parachute=new GameObject("Pilot parachute").transform; parachute.SetParent(transform,false);
            GameArt.Shape("Pilot canopy",PrimitiveType.Sphere,parachute,new Vector3(0,1.5f,0),new Vector3(1.6f,0.32f,0.8f),game.Palette[18]);
            for(var i=-1;i<=1;i+=2)
                GameArt.Shape("Pilot cord",PrimitiveType.Cube,parachute,new Vector3(i*0.4f,0.9f,0),new Vector3(0.025f,1.1f,0.025f),game.Palette[7]);
        }

        private void FixedUpdate()
        {
            if(Finished || game==null || !game.Playing) return;
            age+=Time.fixedDeltaTime;
            if(age>game.Rules.PilotLifetime || transform.position.y<game.Rules.TerrainBottom-3) { Expire(); return; }
            var p=transform.position;
            var fall=game.Rules.PilotFallSpeed*Time.fixedDeltaTime;
            const int mask=1 << GameArt.TerrainLayer;
            Landed=Physics.SphereCast(p,0.4f,Vector3.down,out var floor,fall+0.04f,mask,QueryTriggerInteraction.Ignore);
            if(Landed)
            {
                p.y=floor.point.y+0.43f;
                var ahead=p+Vector3.right*(direction*0.5f);
                var blocked=Physics.SphereCast(p,0.25f,Vector3.right*direction,out _,0.55f,mask,QueryTriggerInteraction.Ignore);
                var safe=Physics.Raycast(ahead+Vector3.up*0.2f,Vector3.down,0.85f,mask,QueryTriggerInteraction.Ignore);
                if(blocked || !safe || Mathf.Abs(p.x)>30) direction=-direction;
                else p.x+=direction*game.Rules.PilotWalkSpeed*Time.fixedDeltaTime;
            }
            else p.y-=fall;
            transform.position=p; parachute.gameObject.SetActive(!Landed);
            model.localRotation=Quaternion.Euler(0,0,Landed?Mathf.Sin(age*7)*4:0);
            if(game.CanReachPickup(p))
            {
                game.Rescue(this);
                Expire();
            }
        }

        public void Expire()
        {
            if(Finished) return;
            Finished=true;
            if(game!=null) game.Pilots.Remove(this);
            Destroy(gameObject);
        }
    }
}
