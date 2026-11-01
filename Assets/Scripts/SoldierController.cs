using UnityEngine;

namespace Airstrike
{
    public struct CombatInput
    {
        public float move;
        public bool jump, fire, dig, pause, restart, quit;
        public Vector2 aim;
    }

    public sealed class SoldierController : MonoBehaviour
    {
        public Rigidbody Body { get; private set; }
        public int Health { get; private set; } = 100;
        public int JumpsLeft { get; private set; } = 2;
        public bool Grounded { get; private set; }
        public int SuccessfulJumps { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.up;
        public bool Protected => Time.time < protectedUntil;
        private AirstrikeGame game;
        private Transform gun;
        private Renderer suit;
        private PhysicsMaterial friction;
        private float protectedUntil, nextShot, nextDig, groundLock;
        private bool jumpQueued;
        private float move;

        public void Initialize(AirstrikeGame owner)
        {
            game = owner;
            gameObject.layer = GameArt.PlayerLayer;
            var capsule = gameObject.AddComponent<CapsuleCollider>();
            capsule.height = 1.65f; capsule.radius = 0.32f;
            friction = new PhysicsMaterial("Soldier friction") { dynamicFriction = 0, staticFriction = 0, frictionCombine = PhysicsMaterialCombine.Minimum };
            capsule.material = friction;
            Body = gameObject.AddComponent<Rigidbody>();
            Body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            var coat = GameArt.Shape("Coat", PrimitiveType.Capsule, transform, new Vector3(0,-0.08f,0), new Vector3(0.65f,0.65f,0.65f), game.Palette[3]);
            suit = coat.GetComponent<Renderer>();
            GameArt.Shape("Helmet", PrimitiveType.Sphere, transform, new Vector3(0,0.61f,0), new Vector3(0.72f,0.38f,0.68f), game.Palette[4]);
            GameArt.Shape("Visor", PrimitiveType.Cube, transform, new Vector3(0,0.55f,-0.32f), new Vector3(0.44f,0.14f,0.08f), game.Palette[7]);
            GameArt.Shape("Boot L", PrimitiveType.Cube, transform, new Vector3(-0.18f,-0.68f,0), new Vector3(0.24f,0.28f,0.54f), game.Palette[2]);
            GameArt.Shape("Boot R", PrimitiveType.Cube, transform, new Vector3(0.18f,-0.68f,0), new Vector3(0.24f,0.28f,0.54f), game.Palette[2]);
            gun = new GameObject("Launcher pivot").transform;
            gun.SetParent(transform,false); gun.localPosition = new Vector3(0,0.18f,-0.34f);
            GameArt.Shape("Launcher", PrimitiveType.Cube, gun, new Vector3(0.43f,0,0), new Vector3(1.05f,0.23f,0.25f), game.Palette[4]);
            GameArt.Shape("Muzzle band", PrimitiveType.Cube, gun, new Vector3(0.91f,0,0), new Vector3(0.1f,0.3f,0.3f), game.Palette[3]);
            Respawn();
        }

        private void Update()
        {
            if (game == null || !game.Playing || Health <= 0) { move = 0; jumpQueued = false; return; }
            var input = game.Controls;
            move = input.move;
            if (input.jump) jumpQueued = true;
            var aim = input.aim - (Vector2)gun.position;
            if (aim.sqrMagnitude > 0.05f) AimDirection = aim.normalized;
            gun.rotation = Quaternion.Euler(0,0,Mathf.Atan2(AimDirection.y,AimDirection.x)*Mathf.Rad2Deg);
            if (input.fire && Time.time >= nextShot && game.RocketsAlive < 1)
            {
                nextShot = Time.time + 0.3f;
                game.LaunchRocket((Vector2)gun.position + AimDirection * 1.05f, AimDirection);
            }
            if (input.dig && Time.time >= nextDig)
            {
                nextDig = Time.time + 0.12f;
                var center = (Vector2)transform.position + Vector2.ClampMagnitude(input.aim - (Vector2)transform.position, 2.15f);
                var removed = game.Ground.Carve(center,0.72f);
                if (removed > 0) game.DigOperations++;
            }
            if (suit != null) suit.enabled = !Protected || Mathf.Repeat(Time.time,0.15f) < 0.1f;
        }

        private void FixedUpdate()
        {
            if (game == null || !game.Playing || Health <= 0) return;
            Grounded = Time.time >= groundLock && Physics.SphereCast(transform.position + Vector3.down*0.43f,
                0.27f, Vector3.down, out _,0.18f,1 << GameArt.TerrainLayer,QueryTriggerInteraction.Ignore);
            if (Grounded && Body.linearVelocity.y < 0.5f) JumpsLeft = 2;
            var velocity = Body.linearVelocity;
            velocity.x = Mathf.MoveTowards(velocity.x, move * 7.5f, (Grounded ? 65 : 32) * Time.fixedDeltaTime);
            if (jumpQueued)
            {
                if (JumpsLeft > 0)
                {
                    velocity.y = 8.5f; JumpsLeft--; SuccessfulJumps++;
                    Grounded = false; groundLock = Time.time + 0.12f;
                    game.PlaySound(2);
                }
                jumpQueued = false;
            }
            if (velocity.y < 0) velocity.y -= 10 * Time.fixedDeltaTime;
            Body.linearVelocity = velocity;
            var position = Body.position;
            if (position.x < -30.8f || position.x > 30.8f)
            {
                position.x = Mathf.Clamp(position.x,-30.8f,30.8f); Body.position = position;
            }
            if (position.y < -11) ApplyDamage(200,true);
        }

        public void ApplyDamage(int damage, bool bypassProtection = false)
        {
            if (Health <= 0 || !game.Playing || (!bypassProtection && Protected)) return;
            Health = Mathf.Max(0,Health-damage);
            protectedUntil = Time.time + 0.75f;
            game.CameraRig.Shake(0.2f);
            game.PlaySound(3);
            if (Health == 0) game.SoldierDown();
        }

        public void Respawn()
        {
            Health = 100; JumpsLeft = 2; jumpQueued = false; Grounded = false;
            Body.position = new Vector3(-6,game.Ground.SurfaceAt(-6)+1.1f,0);
            Body.linearVelocity = Vector3.zero;
            protectedUntil = Time.time + 2.2f;
            suit.enabled = true;
        }

        private void OnDestroy() { if (friction != null) Destroy(friction); }
    }
}
