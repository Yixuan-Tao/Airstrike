using UnityEngine;

namespace Airstrike
{
    public sealed class EnemyAircraft : MonoBehaviour
    {
        public float Speed = 5;
        public bool DropBombs = true;
        public int Health { get; private set; }
        private AirstrikeGame game;
        private int direction;
        private float altitude, bombAt, phase;
        private bool dead;

        public void Initialize(AirstrikeGame owner, int travelDirection, int wave)
        {
            game = owner; direction = travelDirection;
            altitude = transform.position.y; phase = Random.Range(0,6.28f);
            Health = wave >= 4 && Random.value < 0.35f ? 180 : 100;
            Speed = 4.2f + Mathf.Min(wave,10)*0.45f;
            bombAt = Time.time + 1.2f;
            gameObject.layer = GameArt.EnemyLayer;
            var box = gameObject.AddComponent<BoxCollider>(); box.size = new Vector3(3.3f,1.2f,2.4f);
            var model = new GameObject("Aircraft model").transform;
            model.SetParent(transform,false);
            if (direction < 0) model.localRotation = Quaternion.Euler(0,180,0);
            GameArt.Aircraft(model,game.Palette[4],game.Palette[5],game.Palette[7]);
            if (Health > 100) GameArt.Shape("Armor band",PrimitiveType.Cube,model,Vector3.zero,new Vector3(0.35f,0.65f,0.7f),game.Palette[3]);
        }

        private void Update()
        {
            if (dead || game == null || !game.Playing) return;
            var p = transform.position;
            p.x += direction * Speed * Time.deltaTime;
            if (Speed > 0) p.y = altitude + Mathf.Sin(Time.time*0.85f+phase)*0.32f;
            if (p.x > 36) p.x = -36;
            if (p.x < -36) p.x = 36;
            transform.position = p;
            if (DropBombs && Time.time >= bombAt && Mathf.Abs(p.x-game.Player.transform.position.x)<8.5f)
            {
                bombAt = Time.time + Mathf.Max(1,2.5f-game.Wave*0.08f);
                game.DropBomb((Vector2)p+Vector2.down*0.75f,new Vector2(direction*Speed*0.2f,-1));
            }
        }

        public void Damage(int amount)
        {
            if (dead) return;
            Health -= amount;
            if (Health > 0) return;
            dead = true;
            GetComponent<Collider>().enabled = false;
            game.AircraftDestroyed(this);
            Destroy(gameObject);
        }
    }
}
