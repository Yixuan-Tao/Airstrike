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
        public int CoyoteJumps { get; private set; }
        public int BufferedLandingJumps { get; private set; }
        public int StepUps { get; private set; }
        public bool BarrelBlocked { get; private set; }
        public Vector2 AimDirection { get; private set; } = Vector2.up;
        public bool Protected => Time.time < protectedUntil;
        private AirstrikeGame game;
        private Transform gun;

        private PhysicsMaterial friction;
        private float protectedUntil, nextShot, nextDig, groundLock;
        public const float CoyoteSeconds = 0.12f, JumpBufferSeconds = 0.14f;
        public const float MaximumAltitude = 14;
        private float jumpBufferedUntil = -1, lastGroundedAt = -100;
        private bool waitingForLanding;
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
            var model = new GameObject("Soldier model",typeof(SoldierAppearance));
            model.transform.SetParent(transform,false);
            model.GetComponent<SoldierAppearance>().Initialize(this,game.Palette);
            gun = new GameObject("Launcher pivot").transform;
            gun.SetParent(transform,false); gun.localPosition = new Vector3(0,0.18f,-0.34f);
            GameArt.Shape("Launcher tube",PrimitiveType.Cylinder,gun,new Vector3(0.43f,0,0),new Vector3(0.20f,0.50f,0.20f),game.Palette[19]).transform.localRotation=Quaternion.Euler(0,0,90);
            GameArt.Shape("Launcher stock",PrimitiveType.Cube,gun,new Vector3(-0.12f,0,0),new Vector3(0.25f,0.25f,0.28f),game.Palette[4]);
            GameArt.Shape("Launcher sight",PrimitiveType.Cube,gun,new Vector3(0.4f,0.18f,0),new Vector3(0.25f,0.12f,0.12f),game.Palette[7]);
            GameArt.Shape("Muzzle band", PrimitiveType.Cube, gun, new Vector3(0.91f,0,0), new Vector3(0.1f,0.3f,0.3f), game.Palette[3]);
            Respawn();
        }

        private void Update()
        {
            if (game == null || !game.Playing || Health <= 0) { move = 0; jumpBufferedUntil = -1; return; }
            var input = game.Controls;
            move = input.move;
            if (input.jump)
            {
                jumpBufferedUntil = Time.time + JumpBufferSeconds;
                waitingForLanding = JumpsLeft == 0;
            }
            var aim = input.aim - (Vector2)gun.position;
            if (aim.sqrMagnitude > 0.05f) AimDirection = aim.normalized;
            gun.rotation = Quaternion.Euler(0,0,Mathf.Atan2(AimDirection.y,AimDirection.x)*Mathf.Rad2Deg);
            BarrelBlocked = Physics.CheckSphere((Vector2)gun.position, 0.1f, 1 << GameArt.TerrainLayer)
                || Physics.SphereCast((Vector2)gun.position, 0.1f, AimDirection, out _, 1.05f, 1 << GameArt.TerrainLayer);
            if (input.fire && Time.time >= nextShot && game.RocketsAlive < game.RocketCapacity)
            {
                nextShot = Time.time + 0.3f;
                game.LaunchRocket(gun.position, AimDirection);
            }
            if (input.dig && Time.time >= nextDig)
            {
                nextDig = Time.time + 0.12f;
                var center = (Vector2)transform.position + Vector2.ClampMagnitude(input.aim - (Vector2)transform.position, 2.15f);
                var removed = game.Ground.Carve(center,0.72f);
                if (removed > 0) game.DigOperations++;
            }
        }

        private void FixedUpdate()
        {
            if (game == null || !game.Playing || Health <= 0) return;
            var floor = default(RaycastHit);
            Grounded = Time.time >= groundLock && Physics.SphereCast(Body.position + Vector3.down*0.43f,
                0.27f, Vector3.down, out floor,0.28f,1 << GameArt.TerrainLayer,QueryTriggerInteraction.Ignore)
                && floor.normal.y > 0.55f;
            if (Grounded && Body.linearVelocity.y < 0.5f)
            {
                JumpsLeft = game.MultiJumpEnabled?int.MaxValue:2; lastGroundedAt = Time.time;
                // Keep small collider-seam hops from consuming the ground jump.
                // Only descend onto the floor actually detected beneath the capsule.
                var floorY = floor.point.y + 0.835f;
                if (Body.linearVelocity.y <= 0 && Body.position.y > floorY + 0.01f)
                {
                    var groundedPosition = Body.position;
                    groundedPosition.y = Mathf.Max(floorY, groundedPosition.y - 0.15f);
                    Body.position = groundedPosition;
                }
            }
            else if (!game.MultiJumpEnabled && !Grounded && JumpsLeft == 2 && Time.time - lastGroundedAt > CoyoteSeconds) JumpsLeft = 1;
            var velocity = Body.linearVelocity;
            velocity.x = Mathf.MoveTowards(velocity.x, move * 7.5f, (Grounded ? 65 : 32) * Time.fixedDeltaTime);
            if (Time.time <= jumpBufferedUntil && JumpsLeft > 0)
            {
                if (!Grounded && JumpsLeft == 2) CoyoteJumps++;
                if (Grounded && waitingForLanding) BufferedLandingJumps++;
                velocity.y = 8.5f; JumpsLeft--; SuccessfulJumps++;
                Grounded = false; groundLock = Time.time + 0.12f;
                jumpBufferedUntil = -1; waitingForLanding = false;
                game.PlaySound(2);
            }
            if (Grounded && Mathf.Abs(move) > 0.1f && velocity.y < 0.5f) TryStep(Mathf.Sign(move));
            if (velocity.y < 0) velocity.y -= 10 * Time.fixedDeltaTime;
            Body.linearVelocity = velocity;
            var position = Body.position;
            if(position.y>MaximumAltitude)
            {
                position.y=MaximumAltitude;
                var limited=Body.linearVelocity; limited.y=Mathf.Min(0,limited.y);
                Body.linearVelocity=limited; Body.position=position;
            }
            if (position.x < -30.8f || position.x > 30.8f)
            {
                position.x = Mathf.Clamp(position.x,-30.8f,30.8f); Body.position = position;
            }
            if (position.y < -11) ApplyDamage(200,true);
        }

        private void TryStep(float direction)
        {
            const int mask = 1 << GameArt.TerrainLayer;
            var feet = Body.position + Vector3.down * 0.825f;
            var forward = Vector3.right * direction;
            if (!Physics.Raycast(feet + Vector3.up * 0.1f, forward, out var face, 0.55f, mask)
                || face.normal.y > 0.45f) return;
            var probe = feet + forward * 0.52f + Vector3.up * 0.62f;
            if (!Physics.Raycast(probe, Vector3.down, out var top, 0.62f, mask) || top.normal.y < 0.65f) return;
            var rise = top.point.y - feet.y;
            if (rise < 0.05f || rise > 0.52f) return;
            var center = Body.position;
            var low = center + Vector3.down * 0.505f;
            var high = center + Vector3.up * 0.505f;
            if (Physics.CapsuleCast(low, high, 0.30f, Vector3.up, out _, rise + 0.03f, mask)) return;
            var candidate = center + Vector3.up * (rise + 0.025f) + forward * 0.08f;
            if (Physics.CheckCapsule(candidate + Vector3.down * 0.505f, candidate + Vector3.up * 0.505f, 0.31f, mask)) return;
            Body.position = candidate;
            StepUps++;
        }

        public void ApplyDamage(int damage, bool bypassProtection = false)
        {
            if (Health <= 0 || !game.Playing || (!bypassProtection && Protected)) return;
            Health = Mathf.Max(0,Health-damage);
            game.NotifyPlayerHit(damage);
            protectedUntil = Time.time + 0.75f;
            game.CameraRig.Shake(0.2f);
            game.PlaySound(3);
            if (Health == 0) game.SoldierDown();
        }

        public void Respawn()
        {
            Health = 100; JumpsLeft = game.MultiJumpEnabled?int.MaxValue:2; jumpBufferedUntil = -1; Grounded = false;
            waitingForLanding = false; lastGroundedAt = Time.time; groundLock = 0;
            Body.position = new Vector3(-6,game.Ground.SurfaceAt(-6)+0.9f,0);
            Body.linearVelocity = Vector3.zero;
            protectedUntil = Time.time + 2.2f;

        }

        public void GrantMultiJump() { JumpsLeft=int.MaxValue; }

        private void OnDestroy() { if (friction != null) Destroy(friction); }
    }
}
