using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Airstrike
{
    [DefaultExecutionOrder(-100)]
    public sealed class AirstrikeGame : MonoBehaviour
    {
        public const string Version = "0.4.0";
        public Material[] Palette;
        public Camera MainCamera;
        public SoldierController Player { get; private set; }
        public DestructibleGround Ground { get; private set; }
        public BattleCamera CameraRig { get; private set; }
        public BattleEffects Effects { get; private set; }
        public CombatHud Hud { get; private set; }
        public CombatInput Controls { get; private set; }
        public readonly List<EnemyAircraft> Aircraft = new List<EnemyAircraft>();
        public readonly List<SupplyCrate> Supplies = new List<SupplyCrate>();
        public bool Paused { get; private set; }
        public bool GameOver { get; private set; }
        public bool Playing => !Paused && !GameOver;
        public int Wave { get; private set; } = 1;
        public int Lives { get; private set; } = 3;
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public int RocketsAlive, HostileProjectilesAlive, ShotsFired, Explosions, DigOperations, BombsDropped;
        public int EnemyRocketsFired, HelicopterWarnings, PiercedTargets, SuppliesDropped, SuppliesCollected;
        public int RocketCapacity { get; private set; } = 1;
        public bool PiercingEnabled { get; private set; }
        public int CompletedWaves { get; private set; }
        public string SupplyMessage { get; private set; }
        public float SupplyFeedbackUntil { get; private set; }
        public string WaveName => currentPlan==null?"":currentPlan.Name;
        public int BlockedMuzzleShots, CoverEvents;
        public float LastBlastExposure { get; private set; } = 1;
        public int LastDamageBlocked { get; private set; }
        public float CoverFeedbackUntil { get; private set; }
        public float LastCoverAbsorption { get; private set; }
        public float HurtFeedbackUntil { get; private set; }
        public float HitFeedbackUntil { get; private set; }
        public Vector2 LastBlastOrigin { get; private set; }
        public int EnemiesPending { get; private set; }
        public float SurvivalSeconds { get; private set; }
        public float WaveBannerUntil { get; private set; }
        public float SpawnInterval = -1;
        public float WaveOpeningSeconds = 1.4f, WaveIntermissionSeconds = 2.6f;
        public bool OverrideControls;
        public CombatInput TestControls;
        public int BestWave { get; private set; }
        private float nextSpawn, nextWave = -1, respawnAt = -1;
        private AudioSource audioSource;

        private readonly AudioClip[] sounds = new AudioClip[4];
        private Vector2 lastAim = new Vector2(-3,9);
        private WaveDefinition currentPlan;
        private int spawnCursor;

        private void Awake()
        {
            Time.timeScale = 1;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
            if (Palette == null || Palette.Length < 20) { Debug.LogError("Game palette is missing."); enabled = false; return; }
            if (MainCamera == null) MainCamera = Camera.main;
            Ground = new GameObject("Destructible battlefield").AddComponent<DestructibleGround>();
            Ground.Initialize(Palette[0],Palette[1],Palette[2],Palette[15],Palette[16]);
            BuildBackdrop();
            Effects=new GameObject("Battle effects pool").AddComponent<BattleEffects>();
            Effects.Initialize(Palette);
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
                var spec=currentPlan.Spawns[spawnCursor++];
                var height=spec.Role==AircraftRole.Helicopter?Random.Range(8.5f,10.5f):Random.Range(9.2f,13.8f);
                SpawnAircraft(new Vector2(direction>0?-29:29,height),direction,spec.Role,spec.Armored);
                EnemiesPending--;
                nextSpawn = Time.time + (SpawnInterval>0?SpawnInterval:currentPlan.SpawnSeconds);
            }
            Aircraft.RemoveAll(a=>a==null);
            Supplies.RemoveAll(s=>s==null);
            if (EnemiesPending == 0 && Aircraft.Count == 0 && HostileProjectilesAlive == 0)
            {
                if (nextWave < 0)
                {
                    CompletedWaves=Wave;
                    var reward=WavePlan.Reward(Wave);
                    if(reward.HasValue) SpawnSupply(reward.Value,new Vector2(Mathf.Clamp(Player.Body.position.x+2,-29,29),Ground.SurfaceAt(Player.Body.position.x)+8));
                    nextWave = Time.time + WaveIntermissionSeconds;
                }
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
            currentPlan=WavePlan.Get(Wave); spawnCursor=0;
            EnemiesPending = currentPlan.Spawns.Count;
            nextSpawn = Time.time + WaveOpeningSeconds;
            nextWave = -1;
            WaveBannerUntil = Time.time + 3.4f;
        }

        public EnemyAircraft SpawnAircraft(Vector2 position, int direction,AircraftRole role=AircraftRole.Bomber,bool armored=false)
        {
            var go = new GameObject("Raider W"+Wave);
            go.transform.position = position;
            var aircraft = go.AddComponent<EnemyAircraft>();
            aircraft.Initialize(this,direction,Wave,role,armored);
            Aircraft.Add(aircraft);
            return aircraft;
        }

        public void LaunchRocket(Vector2 origin, Vector2 direction)
        {
            direction.Normalize();
            var go = new GameObject("Player rocket"); go.transform.position = origin;
            RocketsAlive++; ShotsFired++;
            var projectile = go.AddComponent<CombatProjectile>();
            projectile.Initialize(this,direction*27,ProjectileKind.PlayerRocket);
            // Sweep the entire barrel first: a thin wall between the breech and
            // muzzle must receive the impact rather than being skipped at spawn.
            var mask = (1 << GameArt.TerrainLayer) | (1 << GameArt.EnemyLayer);
            if (Physics.CheckSphere(origin,0.1f,mask,QueryTriggerInteraction.Ignore))
            {
                BlockedMuzzleShots++;
                var overlaps=Physics.OverlapSphere(origin,0.1f,mask,QueryTriggerInteraction.Ignore);
                Collider contact=null;
                foreach(var collider in overlaps) { contact=collider; if(collider.gameObject.layer==GameArt.TerrainLayer) break; }
                projectile.Impact(origin,contact);
            }
            else if (Physics.SphereCast(origin,0.1f,direction,out var hit,1.05f,mask,QueryTriggerInteraction.Ignore))
            {
                BlockedMuzzleShots++;
                projectile.Impact(hit.point,hit.collider);
            }
            else go.transform.position = origin + direction*1.05f;
            Flash(origin+direction*0.8f,0.38f);
            PlaySound(0);
        }

        public void DropBomb(Vector2 position, Vector2 velocity)
        {
            var go = new GameObject("Falling bomb"); go.transform.position = position;
            HostileProjectilesAlive++; BombsDropped++;
            go.AddComponent<CombatProjectile>().Initialize(this,velocity,ProjectileKind.Bomb);
        }

        public void LaunchEnemyRocket(Vector2 position,Vector2 target)
        {
            var go=new GameObject("Helicopter rocket"); go.transform.position=position;
            HostileProjectilesAlive++; EnemyRocketsFired++;
            go.AddComponent<CombatProjectile>().Initialize(this,(target-position).normalized*14,ProjectileKind.EnemyRocket);
            PlaySound(0);
        }

        public SupplyCrate SpawnSupply(SupplyKind kind,Vector2 position)
        {
            Supplies.RemoveAll(s=>s==null);
            if(Supplies.Count>=3) Supplies[0].Expire();
            var go=new GameObject("Supply "+kind); go.transform.position=position;
            var crate=go.AddComponent<SupplyCrate>(); crate.Initialize(this,kind);
            Supplies.Add(crate); SuppliesDropped++;
            return crate;
        }

        public void CollectSupply(SupplyCrate crate)
        {
            if(!Supplies.Remove(crate)) return;
            SuppliesCollected++;
            if(crate.Kind==SupplyKind.RocketSlot && RocketCapacity<3)
            { RocketCapacity++; SupplyMessage="ROCKET SLOTS  "+RocketCapacity; }
            else if(crate.Kind==SupplyKind.Piercing && !PiercingEnabled)
            { PiercingEnabled=true; SupplyMessage="PIERCING ROCKETS ONLINE"; }
            else { Score+=150; SupplyMessage="UPGRADE ACTIVE  /  +150 SCORE"; }
            SupplyFeedbackUntil=Time.time+3;
            PlaySound(2);
        }

        public void Explode(Vector2 position, bool rocket,ISet<EnemyAircraft> excludedTargets=null)
        {
            Explosions++;
            // Resolve exposure BEFORE excavating: intact soil absorbs this blast,
            // then its crater can leave the player exposed to the next bomb.
            if (rocket)
            {
                var targets = Aircraft.ToArray();
                foreach (var target in targets)
                    if (target != null && (excludedTargets==null || !excludedTargets.Contains(target)) && Vector2.Distance(target.transform.position,position)<3)
                        target.Damage(Mathf.RoundToInt(110*Ground.BlastTransmission(position,target.transform.position)));
            }
            else if (Player.Health > 0)
            {
                var distance = Vector2.Distance(Player.transform.position,position);
                if (distance < 3.3f)
                {
                    var center=(Vector2)Player.Body.position;
                    LastBlastOrigin=position;
                    LastBlastExposure=Ground.BlastTransmission(position,center+Vector2.up*0.60f)*0.25f
                        + Ground.BlastTransmission(position,center+Vector2.up*0.08f)*0.55f
                        + Ground.BlastTransmission(position,center-Vector2.up*0.55f)*0.20f;
                    var unshielded=Mathf.RoundToInt(Mathf.Lerp(85,25,distance/3.3f));
                    var damage=Mathf.RoundToInt(unshielded*LastBlastExposure);
                    LastDamageBlocked=unshielded-damage;
                    if(LastDamageBlocked>0 && !Player.Protected)
                    {
                        CoverEvents++; CoverFeedbackUntil=Time.time+1.6f; LastCoverAbsorption=1-LastBlastExposure;
                    }
                    var previousHealth=Player.Health;
                    if(damage>0) Player.ApplyDamage(damage);
                    if(Player.Health>0 && Player.Health<previousHealth)
                        Player.Body.AddForce(new Vector3((Player.transform.position.x-position.x)*2,3,0)*LastBlastExposure,ForceMode.Impulse);
                }
            }
            Ground.Carve(position,rocket?1.5f:2.1f);
            Flash(position,rocket?2.0f:2.8f);
            CameraRig.Shake(rocket?0.08f:0.16f);
            PlaySound(1);
        }

        public void NotifyPlayerHit(int damage) { if(damage>0) HurtFeedbackUntil=Time.time+0.4f; }
        public void NotifyAircraftHit() { HitFeedbackUntil=Time.time+0.18f; }

        public void Flash(Vector2 position, float radius)
        {
            Effects.Emit(position,radius);
        }

        public void AircraftDestroyed(EnemyAircraft aircraft)
        {
            Aircraft.Remove(aircraft);
            Kills++; Score += aircraft.ScoreValue*Wave;
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
                BestWave = Mathf.Max(BestWave,CompletedWaves);
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
            var backdrop=new GameObject("Battlefield backdrop").AddComponent<BattleBackdrop>();
            backdrop.Initialize(Palette,MainCamera);
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

            if (!Application.isBatchMode && !Application.isEditor) Cursor.visible=true;
        }
    }
}
