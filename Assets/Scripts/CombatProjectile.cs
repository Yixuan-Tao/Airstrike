using UnityEngine;

namespace Airstrike
{
    public sealed class CombatProjectile : MonoBehaviour
    {
        private AirstrikeGame game;
        private Vector2 velocity;
        private bool rocket, finished;
        private float age;
        public bool IsPlayerRocket => rocket;
        public void Cancel() { Finish(); }

        public void Initialize(AirstrikeGame owner, Vector2 initialVelocity, bool playerRocket)
        {
            game = owner; velocity = initialVelocity; rocket = playerRocket;
            transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg-90);
            var visual = GameArt.Shape(rocket ? "Rocket casing" : "Bomb casing", PrimitiveType.Capsule,
                transform,Vector3.zero,rocket ? new Vector3(0.15f,0.32f,0.15f) : new Vector3(0.32f,0.38f,0.32f),game.Palette[rocket?3:5]);
            if (rocket)
            {
                var trail = gameObject.AddComponent<TrailRenderer>();
                trail.sharedMaterial = game.Palette[8]; trail.time = 0.24f;
                trail.startWidth = 0.12f; trail.endWidth = 0;
                trail.minVertexDistance = 0.1f;
            }
            visual.name = rocket ? "Rocket" : "Bomb";
        }

        private void FixedUpdate()
        {
            if (finished || game == null || !game.Playing) return;
            age += Time.fixedDeltaTime;
            if (!rocket) velocity.y -= 10 * Time.fixedDeltaTime;
            var delta = velocity * Time.fixedDeltaTime;
            var mask = (1 << GameArt.TerrainLayer) | (1 << (rocket?GameArt.EnemyLayer:GameArt.PlayerLayer));
            if (Physics.SphereCast(transform.position,rocket?0.1f:0.18f,delta.normalized,out var hit,
                delta.magnitude,mask,QueryTriggerInteraction.Ignore))
            {
                transform.position = new Vector3(hit.point.x,hit.point.y,0);
                Detonate(); return;
            }
            transform.position += (Vector3)delta;
            transform.rotation = Quaternion.Euler(0,0,Mathf.Atan2(velocity.y,velocity.x)*Mathf.Rad2Deg-90);
            if (age > (rocket?2.5f:8) || Mathf.Abs(transform.position.x)>40 || transform.position.y>25 || transform.position.y<-12)
                Finish();
        }

        private void Detonate()
        {
            if (finished) return;
            game.Explode(transform.position,rocket);
            Finish();
        }

        public void Impact(Vector2 point)
        {
            if(finished) return;
            transform.position=new Vector3(point.x,point.y,0);
            Detonate();
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            if (rocket) game.RocketsAlive = Mathf.Max(0,game.RocketsAlive-1);
            else game.BombsAlive = Mathf.Max(0,game.BombsAlive-1);
            Destroy(gameObject);
        }
    }
}
