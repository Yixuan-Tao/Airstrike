using UnityEngine;

namespace Airstrike
{
    public sealed class SupplyCrate : MonoBehaviour
    {
        public SupplyKind Kind { get; private set; }
        public bool Collected { get; private set; }
        public bool Landed { get; private set; }
        private AirstrikeGame game;
        private Transform parachute;
        private float age;

        public void Initialize(AirstrikeGame owner,SupplyKind kind)
        {
            game=owner; Kind=kind;
            var color=owner.Palette[kind==SupplyKind.RocketSlot?3:kind==SupplyKind.Piercing?4:kind==SupplyKind.Emp?19:18];
            GameArt.Shape("Supply box",PrimitiveType.Cube,transform,Vector3.zero,Vector3.one*0.65f,color);
            GameArt.Shape("Supply band",PrimitiveType.Cube,transform,new Vector3(0,0,-0.34f),new Vector3(0.15f,0.68f,0.04f),owner.Palette[8]);
            var icon=new GameObject("Supply symbol").transform; icon.SetParent(transform,false);
            icon.localPosition=new Vector3(0,0,-0.37f);
            GameArt.SupplyIcon(icon,kind,owner.Palette[kind==SupplyKind.RocketSlot || kind==SupplyKind.JumpBoots?7:8]);
            parachute=new GameObject("Supply parachute").transform; parachute.SetParent(transform,false);
            GameArt.Shape("Canopy",PrimitiveType.Sphere,parachute,new Vector3(0,1.2f,0),new Vector3(1.6f,0.3f,0.8f),color);
            for(var i=-1;i<=1;i+=2)
                GameArt.Shape("Cord",PrimitiveType.Cube,parachute,new Vector3(i*0.4f,0.65f,0),new Vector3(0.025f,0.9f,0.025f),owner.Palette[7]);
        }

        private void FixedUpdate()
        {
            if(Collected || game==null || !game.Playing) return;
            age+=Time.fixedDeltaTime;
            if(age>game.Rules.CrateLifetime) { Expire(); return; }
            var fall=game.Rules.CrateFallSpeed*Time.fixedDeltaTime;
            Landed=Physics.SphereCast(transform.position,0.32f,Vector3.down,out var hit,fall+0.02f,
                1 << GameArt.TerrainLayer,QueryTriggerInteraction.Ignore);
            if(Landed) transform.position=new Vector3(transform.position.x,hit.point.y+0.34f,0);
            else transform.position+=Vector3.down*fall;
            parachute.gameObject.SetActive(!Landed);
            if(game.CanReachPickup(transform.position))
            {
                Collected=true;
                game.CollectSupply(this);
                Destroy(gameObject);
            }
        }

        public void Expire() { if(game!=null) game.Supplies.Remove(this); Destroy(gameObject); }
    }
}
