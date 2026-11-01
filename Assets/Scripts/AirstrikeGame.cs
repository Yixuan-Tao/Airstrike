using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Airstrike
{
    [DefaultExecutionOrder(-100)]
    public sealed class AirstrikeGame : MonoBehaviour
    {
        public Material[] Palette;
        public Camera MainCamera;
        public SoldierController Player { get; private set; }
        public DestructibleGround Ground { get; private set; }
        public BattleCamera CameraRig { get; private set; }
        public CombatHud Hud { get; private set; }
        public CombatInput Controls { get; private set; }
        public readonly List<EnemyAircraft> Aircraft = new List<EnemyAircraft>();
        public bool Paused { get; private set; }
        public bool GameOver { get; private set; }
        public bool Playing => !Paused && !GameOver;
        public int Wave { get; private set; } = 1;
        public int Lives { get; private set; } = 3;
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public int RocketsAlive, BombsAlive, ShotsFired, Explosions, DigOperations, BombsDropped;
        public int EnemiesPending { get; private set; }
        public float SurvivalSeconds { get; private set; }
        public float WaveBannerUntil { get; private set; }
        public float SpawnInterval = 2.6f;
        public bool OverrideControls;
        public CombatInput TestControls;
        public int BestWave { get; private set; }
        private float nextSpawn, nextWave = -1, respawnAt = -1;
        private AudioSource audioSource;
        private Mesh ridgeMesh;
        private readonly AudioClip[] sounds = new AudioClip[4];
        private Vector2 lastAim = new Vector2(-3,9);

        private void Awake()
        {
            Time.timeScale = 1;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
            if (Palette == null || Palette.Length < 10) { Debug.LogError("Game palette is missing."); enabled = false; return; }
            if (MainCamera == null) MainCamera = Camera.main;
            Ground = new GameObject("Destructible battlefield").AddComponent<DestructibleGround>();
            Ground.Initialize(Palette[0],Palette[1],Palette[2]);
            BuildBackdrop();
            Player = new GameObject("Soldier").AddComponent<SoldierController>();
            Player.Initialize(this);
            CameraRig = MainCamera.gameObject.AddComponent<BattleCamera>();
            CameraRig.Target = Player.transform;
            Hud = new GameObject("Combat interface").AddComponent<CombatHud>();
            Hud.Initialize(this);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0; audioSource.volume = 0.28f;
            CreateSounds();
            BestWave = Application.isBatchMode ? 0 : PlayerPrefs.GetInt("Skybreach.BestWave",0);
            BeginWave();
            GameplayAutomation.AttachIfRequested(this);
            if (!Application.isBatchMode && !Application.isEditor) Cursor.visible = false;
        }

        private void Update()
        {
            Controls = OverrideControls ? TestControls : ReadInput();
            if (Controls.restart) { Restart(); return; }
            if (Controls.pause && !GameOver) SetPaused(!Paused);
            if (Controls.quit && (Paused || GameOver)) Application.Quit();
            if (!Playing) return;
            SurvivalSeconds += Time.deltaTime;
            if (respawnAt > 0 && Time.time >= respawnAt) { Player.Respawn(); respawnAt = -1; }
            if (EnemiesPending > 0 && Time.time >= nextSpawn)
            {
                var direction = Random.value < 0.5f ? 1 : -1;
                SpawnAircraft(new Vector2(direction>0?-29:29,Random.Range(9.2f,13.8f)),direction);
                EnemiesPending--;
                nextSpawn = Time.time + Mathf.Max(1,SpawnInterval-Wave*0.08f);
            }
            Aircraft.RemoveAll(a=>a==null);
            if (EnemiesPending == 0 && Aircraft.Count == 0 && BombsAlive == 0)
            {
                if (nextWave < 0) nextWave = Time.time + 2.6f;
                if (Time.time >= nextWave) { Wave++; BeginWave(); }
            }
        }

        private CombatInput ReadInput()
        {
            // Batch-mode development must never consume the user's other desktop work.
            var input = new CombatInput { aim = lastAim };
            if (Application.isBatchMode || !Application.isFocused) return input;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                input.move = ((keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)?1:0)
                    - ((keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)?1:0);
                input.jump = keyboard.spaceKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame;
                input.pause = keyboard.escapeKey.wasPressedThisFrame;
                input.restart = keyboard.rKey.wasPressedThisFrame;
                input.quit = keyboard.qKey.wasPressedThisFrame;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                var ray = MainCamera.ScreenPointToRay(mouse.position.ReadValue());
                if (new Plane(Vector3.forward,Vector3.zero).Raycast(ray,out var enter)) lastAim = ray.GetPoint(enter);
                input.aim = lastAim;
                input.fire = mouse.leftButton.isPressed;
                input.dig = mouse.rightButton.isPressed;
            }
            return input;
        }

        private void BeginWave()
        {
            EnemiesPending = Mathf.Min(2+Wave,12);
            nextSpawn = Time.time + 1.4f;
            nextWave = -1;
            WaveBannerUntil = Time.time + 3.4f;
        }

        public EnemyAircraft SpawnAircraft(Vector2 position, int direction)
        {
            var go = new GameObject("Raider W"+Wave);
            go.transform.position = position;
            var aircraft = go.AddComponent<EnemyAircraft>();
            aircraft.Initialize(this,direction,Wave);
            Aircraft.Add(aircraft);
            return aircraft;
        }

        public void LaunchRocket(Vector2 position, Vector2 direction)
        {
            var go = new GameObject("Player rocket"); go.transform.position = position;
            RocketsAlive++; ShotsFired++;
            go.AddComponent<CombatProjectile>().Initialize(this,direction.normalized*27,true);
            PlaySound(0);
        }

        public void DropBomb(Vector2 position, Vector2 velocity)
        {
            var go = new GameObject("Falling bomb"); go.transform.position = position;
            BombsAlive++; BombsDropped++;
            go.AddComponent<CombatProjectile>().Initialize(this,velocity,false);
        }

        public void Explode(Vector2 position, bool rocket)
        {
            Explosions++;
            Ground.Carve(position,rocket?1.5f:2.1f);
            Flash(position,rocket?2.0f:2.8f);
            if (rocket)
            {
                var targets = Aircraft.ToArray();
                foreach (var target in targets)
                    if (target != null && Vector2.Distance(target.transform.position,position)<3)
                        target.Damage(110);
            }
            else if (Player.Health > 0)
            {
                var distance = Vector2.Distance(Player.transform.position,position);
                if (distance < 3.3f)
                {
                    Player.ApplyDamage(Mathf.RoundToInt(Mathf.Lerp(85,25,distance/3.3f)));
                    if (Player.Health > 0) Player.Body.AddForce(new Vector3((Player.transform.position.x-position.x)*2,3,0),ForceMode.Impulse);
                }
            }
            CameraRig.Shake(rocket?0.08f:0.16f);
            PlaySound(1);
        }

        public void Flash(Vector2 position, float radius)
        {
            var go = new GameObject("Blast flash"); go.transform.position = new Vector3(position.x,position.y,-0.15f);
            go.AddComponent<BlastVisual>().Initialize(radius,Palette[8]);
        }

        public void AircraftDestroyed(EnemyAircraft aircraft)
        {
            Aircraft.Remove(aircraft);
            Kills++; Score += 100*Wave;
            Flash(aircraft.transform.position,2.5f);
            PlaySound(1);
            CameraRig.Shake(0.12f);
        }

        public void SoldierDown()
        {
            Lives--;
            Flash(Player.transform.position,1.6f);
            if (Lives <= 0)
            {
                GameOver = true;
                BestWave = Mathf.Max(BestWave,Wave-1);
                if (!OverrideControls && !Application.isBatchMode)
                {
                    PlayerPrefs.SetInt("Skybreach.BestWave",BestWave); PlayerPrefs.Save();
                }
            }
            else respawnAt = Time.time + 1;
        }

        public void SetPaused(bool pause) { Paused = pause; Time.timeScale = pause?0:1; }
        public void Restart() { Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }

        private void BuildBackdrop()
        {
            var root = new GameObject("Backdrop").transform;
            var ridge = new GameObject("Distant ridgeline",typeof(MeshFilter),typeof(MeshRenderer));
            ridge.transform.SetParent(root,false);
            var vertices=new List<Vector3>();
            var indices=new List<int>();
            for (var i=0;i<=28;i++)
            {
                var x=-70+i*5;
                var height=4.7f+Mathf.PerlinNoise(i*0.55f,3)*5;
                vertices.Add(new Vector3(x,-12,15)); vertices.Add(new Vector3(x,height,15));
                if(i==0) continue;
                var n=(i-1)*2;
                indices.Add(n); indices.Add(n+1); indices.Add(n+3);
                indices.Add(n); indices.Add(n+3); indices.Add(n+2);
            }
            ridgeMesh=new Mesh {name="Continuous mountain silhouette"};
            ridgeMesh.SetVertices(vertices); ridgeMesh.SetTriangles(indices,0);
            ridgeMesh.RecalculateNormals(); ridgeMesh.RecalculateBounds();
            ridge.GetComponent<MeshFilter>().sharedMesh=ridgeMesh;
            var ridgeRenderer=ridge.GetComponent<MeshRenderer>();
            ridgeRenderer.sharedMaterial=Palette[6]; ridgeRenderer.receiveShadows=false;
            ridgeRenderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            for (var i=0;i<9;i++)
            {
                var tower = GameArt.Shape("Distant tower",PrimitiveType.Cube,root,new Vector3(-35+i*9,0.8f,10),new Vector3(0.18f,3.8f,0.25f),Palette[7]);
                GameArt.Shape("Tower crossbar",PrimitiveType.Cube,tower.transform,new Vector3(0,0.2f,0),new Vector3(9,0.035f,1),Palette[7]);
            }
            GameArt.Shape("Sun",PrimitiveType.Sphere,root,new Vector3(14,10.5f,24),Vector3.one*3.4f,Palette[9]);
        }

        private void CreateSounds()
        {
            if (Application.isBatchMode) return;
            var random = new System.Random(71);
            for (var kind=0;kind<sounds.Length;kind++)
            {
                const int sampleRate=22050;
                var length=kind==1?0.5f:0.16f;
                var samples=new float[(int)(length*sampleRate)];
                for(var i=0;i<samples.Length;i++)
                {
                    var t=i/(float)sampleRate; var envelope=Mathf.Pow(1-t/length,2);
                    var noise=(float)(random.NextDouble()*2-1);
                    var frequency=kind==2?400+t*1300:kind==1?55:kind==0?140:95;
                    samples[i]=(Mathf.Sin(t*frequency*6.283f)*0.5f+noise*(kind==2?0.06f:0.5f))*envelope;
                }
                sounds[kind]=AudioClip.Create("Synth effect "+kind,samples.Length,1,sampleRate,false);
                sounds[kind].SetData(samples,0);
            }
        }

        public void PlaySound(int kind) { if (audioSource!=null && sounds[kind]!=null) audioSource.PlayOneShot(sounds[kind]); }

        private void OnDestroy()
        {
            Time.timeScale=1;
            foreach(var clip in sounds) if(clip!=null) Destroy(clip);
            if(ridgeMesh!=null) Destroy(ridgeMesh);
            if (!Application.isBatchMode && !Application.isEditor) Cursor.visible=true;
        }
    }
}
