using System.Collections.Generic;
using UnityEngine;

namespace Airstrike
{
    public enum ProjectileKind { PlayerRocket, Bomb, EnemyRocket }

    public sealed class CombatProjectile : MonoBehaviour
    {
        private AirstrikeGame game;
        private Vector2 velocity;
        private ProjectileKind kind;
        private bool finished, piercing;
        private float age;
        private HashSet<EnemyAircraft> pierced;
        private readonly RaycastHit[] hits=new RaycastHit[32];
        public bool IsPlayerRocket => kind==ProjectileKind.PlayerRocket;
        public bool IsEnemyRocket => kind==ProjectileKind.EnemyRocket;
        public void Cancel() { Finish(); }

        public void Initialize(AirstrikeGame owner,Vector2 initialVelocity,ProjectileKind type)
        {
            game=owner; velocity=initialVelocity; kind=type;
            piercing=IsPlayerRocket && owner.PiercingEnabled;
            if(piercing) pierced=new HashSet<EnemyAircraft>();
            transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg-90);
            GameArt.Shape(kind.ToString(),PrimitiveType.Capsule,transform,Vector3.zero,
                kind==ProjectileKind.Bomb?new Vector3(0.32f,0.38f,0.32f):new Vector3(0.16f,0.34f,0.16f),game.Palette[IsPlayerRocket?3:5]);
            if(kind!=ProjectileKind.Bomb)
            {
                var trail=gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial=game.Palette[8]; trail.time=0.24f; trail.startWidth=0.12f; trail.endWidth=0; trail.minVertexDistance=0.1f;
                if(IsEnemyRocket)
                {
                    var tint=new MaterialPropertyBlock(); tint.SetColor("_BaseColor",new Color(1,0.25f,0.13f));
                    trail.SetPropertyBlock(tint);
                }
            }
        }

        private void FixedUpdate()
        {
            if(finished || game==null || !game.Playing) return;
            age+=Time.fixedDeltaTime;
            if(kind==ProjectileKind.Bomb) velocity.y-=game.Rules.BombGravity*Time.fixedDeltaTime;
            var direction=velocity.normalized;
            var remaining=velocity.magnitude*Time.fixedDeltaTime;
            var start=(Vector2)transform.position;
            // Terrain remains solid even when a shot begins touching a wall.
            if(Physics.CheckSphere(start,IsPlayerRocket?0.1f:0.16f,1 << GameArt.TerrainLayer,QueryTriggerInteraction.Ignore))
            { Impact(start); return; }
            for(var pass=0;pass<3 && remaining>0;pass++)
            {
                if(!NearestHit(start,direction,remaining,out var hit))
                { transform.position=start+direction*remaining; break; }
                var enemy=hit.collider.GetComponent<EnemyAircraft>();
                if(TryPierce(enemy))
                {
                    var travelled=Mathf.Min(remaining,hit.distance+0.01f);
                    start+=direction*travelled; remaining-=travelled;
                    transform.position=start;
                }
                else { Impact(hit.point,hit.collider); return; }
            }
            transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg-90);
            var lifetime=IsPlayerRocket?game.Rules.RocketLifetime:kind==ProjectileKind.Bomb?8:4.2f;
            if(age>lifetime || Mathf.Abs(transform.position.x)>40 || transform.position.y>25 || transform.position.y<-12) Finish();
        }

        private bool NearestHit(Vector2 origin,Vector2 direction,float distance,out RaycastHit nearest)
        {
            var mask=(1 << GameArt.TerrainLayer)|(1 << (IsPlayerRocket?GameArt.EnemyLayer:GameArt.PlayerLayer));
            var count=Physics.SphereCastNonAlloc(origin,IsPlayerRocket?0.1f:0.16f,direction,hits,distance,mask,QueryTriggerInteraction.Ignore);
            // Dense collision fixtures must not silently discard a nearer wall.
            var buffer=count==hits.Length?Physics.SphereCastAll(origin,IsPlayerRocket?0.1f:0.16f,direction,distance,mask,QueryTriggerInteraction.Ignore):hits;
            if(buffer!=hits) count=buffer.Length;
            nearest=default; var found=false; var closest=float.PositiveInfinity;
            for(var i=0;i<count;i++)
            {
                var enemy=buffer[i].collider.GetComponent<EnemyAircraft>();
                if(enemy!=null && pierced!=null && pierced.Contains(enemy)) continue;
                if(buffer[i].distance>=closest) continue;
                closest=buffer[i].distance; nearest=buffer[i]; found=true;
            }
            return found;
        }

        private bool TryPierce(EnemyAircraft enemy)
        {
            if(!piercing || enemy==null || (game.Rules.PierceLimit>0 && pierced.Count>=game.Rules.PierceLimit)) return false;
            pierced.Add(enemy); game.PiercedTargets++;
            enemy.Damage(game.Rules.RocketDamage); game.Flash(enemy.transform.position,0.8f);
            return true;
        }

        public void Impact(Vector2 point,Collider collider=null)
        {
            if(finished) return;
            var enemy=collider==null?null:collider.GetComponent<EnemyAircraft>();
            if(TryPierce(enemy))
            { transform.position=point-velocity.normalized*0.1f; return; }
            transform.position=new Vector3(point.x,point.y,0);
            game.Explode(point,IsPlayerRocket,pierced,enemy);
            Finish();
        }

        private void Finish()
        {
            if(finished) return;
            finished=true;
            if(IsPlayerRocket) game.RocketsAlive=Mathf.Max(0,game.RocketsAlive-1);
            else game.HostileProjectilesAlive=Mathf.Max(0,game.HostileProjectilesAlive-1);
            Destroy(gameObject);
        }
    }
}
