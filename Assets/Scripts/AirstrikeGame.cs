using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Airstrike
{
    [DefaultExecutionOrder(-100)]
    public sealed class AirstrikeGame : MonoBehaviour
    {
        public const string Version = "0.7.0";
        public event System.Action<Vector2> HostileImpact;
        public int MaximumLives => Rules.LivesCap==0?int.MaxValue:Rules.LivesCap;
        public const float RespawnX = -6;
        public Material[] Palette;
        public Camera MainCamera;
        public SoldierController Player { get; private set; }
        public DestructibleGround Ground { get; private set; }
        public BattleCamera CameraRig { get; private set; }
        public BattleEffects Effects { get; private set; }
        public CombatHud Hud { get; private set; }
        public CombatInput Controls { get; private set; }
        public BalanceSettings Rules { get; private set; }
        public bool RunStarted => Rules!=null;
        public readonly List<EnemyAircraft> Aircraft = new List<EnemyAircraft>();
        public readonly List<SupplyCrate> Supplies = new List<SupplyCrate>();
        public readonly List<RescuePilot> Pilots = new List<RescuePilot>();
        public bool Paused { get; private set; }
        public bool GameOver { get; private set; }
        public bool Playing => RunStarted && !Paused && !GameOver;
        public int Wave { get; private set; } = 1;
        public int Lives { get; private set; }
        public int Score { get; private set; }
        public int Kills { get; private set; }
        public int RocketsAlive, HostileProjectilesAlive, ShotsFired, Explosions, DigOperations, BombsDropped;
        public int EnemyRocketsFired, HelicopterWarnings, PiercedTargets, SuppliesDropped, SuppliesCollected;
        public int RocketCapacity { get; private set; }
        public bool PiercingEnabled { get; private set; }
        public bool MultiJumpEnabled { get; private set; }
        public float PilotDropChance { get; set; }
        public float CrashChance { get; set; }
        public int FallingWrecks { get; private set; }
        public int WrecksLanded { get; private set; }
        public int TransportsSpawned, TransportsShotDown, TransportsEscaped, EmpActivations, EmpKills, PilotsSpawned, PilotsRescued;
        public int HostileAircraftCount { get { var count=0; foreach(var aircraft in Aircraft) if(aircraft!=null && !aircraft.IsSupplyTransport) count++; return count; } }
        public int CompletedWaves { get; private set; }
        public string SupplyMessage => supplyAnnouncement?.Invoke()??"";
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
        internal float SpawnInterval { get; set; } = -1;
        internal float WaveOpeningSeconds { get; set; }
        internal float WaveIntermissionSeconds { get; set; }
        public bool OverrideControls;
        public CombatInput TestControls;
        public int BestWave { get; private set; }
        private float nextSpawn, nextWave = -1, respawnAt = -1;
        private AudioSource audioSource;

        private readonly AudioClip[] sounds = new AudioClip[4];
        private Vector2 lastAim = new Vector2(-3,9);
        private WaveDefinition currentPlan;
        private int spawnCursor, waveSeed;
        private float[] entryOffsets;
        private float waveStarted;
        private static BalanceMode? restartMode;
        private System.Func<string> supplyAnnouncement;
        private string BestWaveKey => Rules.Mode==BalanceMode.PC?"Skybreach.BestWave":"Skybreach.BestWave.Classic";

        private void Awake()
        {
            Time.timeScale = 1;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 1;
            if (Palette == null || Palette.Length < 20) { Debug.LogError("Game palette is missing."); enabled = false; return; }
            if (MainCamera == null) MainCamera = Camera.main;
            BuildBackdrop();
            Hud = new GameObject("Combat interface").AddComponent<CombatHud>();
            Hud.Initialize(this);
            var mode=restartMode;
            restartMode=null;
            var args=System.Environment.GetCommandLineArgs();
            if(mode.HasValue || System.Array.IndexOf(args,"--gameplay-smoke")>=0 || System.Array.IndexOf(args,"--pacing-probe")>=0)
                StartRun(mode??BalanceMode.PC);
            MenuAutomation.AttachIfRequested(this);
        }

        public bool StartRun(BalanceMode mode)
        {
            if(RunStarted) return false;
            try { Rules=BalanceSettings.Load(mode); }
            catch(System.Exception error) when(error is System.IO.IOException || error is System.FormatException || error is System.UnauthorizedAccessException)
            {
                Hud.ShowBalanceError(error.Message);
                return false;
            }
            Lives=Rules.InitialLives;
            RocketCapacity=Rules.RocketSlots;
            CrashChance=Rules.CrashChance;
            PilotDropChance=Rules.PilotChance;
            WaveOpeningSeconds=Rules.FirstSpawnDelay;
            WaveIntermissionSeconds=Rules.WaveIntermission;
            Ground = new GameObject("Destructible battlefield").AddComponent<DestructibleGround>();
            Ground.Initialize(Rules,Palette[0],Palette[1],Palette[2],Palette[15],Palette[16],Palette[19],Palette[7]);
            Effects=new GameObject("Battle effects pool").AddComponent<BattleEffects>();
            Effects.Initialize(Palette);
            Player = new GameObject("Soldier").AddComponent<SoldierController>();
            Player.Initialize(this);
            CameraRig = MainCamera.gameObject.AddComponent<BattleCamera>();
            CameraRig.Target = Player.transform;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0; audioSource.volume = 0.28f;
            CreateSounds();
            BestWave = Application.isBatchMode ? 0 : PlayerPrefs.GetInt(BestWaveKey,0);
            waveSeed=System.Environment.TickCount;
            BeginWave();
            Hud.EnterBattle();
            SetPaused(false);
            GameplayAutomation.AttachIfRequested(this);
            PacingAutomation.AttachIfRequested(this);
            return true;
        }

        private void Update()
        {
            var keyboard=Keyboard.current;
            if(!OverrideControls && !Application.isBatchMode && Application.isFocused && keyboard!=null)
            {
                if(keyboard.f1Key.wasPressedThisFrame) Hud.ToggleLanguage();
                if(!RunStarted)
                {
                    if(keyboard.digit1Key.wasPressedThisFrame) StartRun(BalanceMode.PC);
                    if(keyboard.digit2Key.wasPressedThisFrame) StartRun(BalanceMode.Classic);
                    return;
                }
                if((Paused || GameOver) && keyboard.mKey.wasPressedThisFrame) { ReturnToMenu(); return; }
            }
            if(!RunStarted) return;
            Controls = OverrideControls ? TestControls : ReadInput();
            if (Controls.restart) { Restart(); return; }
            if (Controls.pause && !GameOver) SetPaused(!Paused);
            if (Controls.quit && (Paused || GameOver)) Application.Quit();
            if (!Playing) return;
            SurvivalSeconds += Time.deltaTime;
            if (respawnAt > 0 && Time.time >= respawnAt) { Player.Respawn(); respawnAt = -1; }
            if (EnemiesPending > 0 && Time.time >= nextSpawn && HostileAircraftCount<Rules.ActiveAircraftCap)
            {
                var direction = Random.value < 0.5f ? 1 : -1;
                var spec=currentPlan.Spawns[spawnCursor++];
                var height=spec.Role==AircraftRole.Helicopter?Random.Range(8.5f,10.5f):Random.Range(9.2f,13.8f);
                SpawnAircraft(new Vector2(direction>0?-35:35,height),direction,spec.Role,spec.Armored,spec.AttackRank,spec.SpeedBoosted);
                if(spawnCursor==1)
                {
                    var cargo=WavePlan.Cargo(Wave,Rules,waveSeed);
                    if(cargo.HasValue) SpawnTransport(cargo.Value,new Vector2(direction>0?30:-30,8.5f),-direction);
                }
                EnemiesPending--;
                if(EnemiesPending>0) nextSpawn=SpawnInterval>0?Time.time+SpawnInterval:waveStarted+entryOffsets[spawnCursor];
            }
            Aircraft.RemoveAll(a=>a==null);
            Supplies.RemoveAll(s=>s==null);
            Pilots.RemoveAll(p=>p==null);
            if (EnemiesPending == 0 && (Rules.CargoBlocksWave?Aircraft.Count:HostileAircraftCount) == 0)
            {
                if (nextWave < 0)
                {
                    CompletedWaves=Wave;
                    nextWave = Time.time + WaveIntermissionSeconds;
                }
                if (Time.time >= nextWave) { Wave++; BeginWave(); }
            }
            else nextWave=-1;
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
            currentPlan=WavePlan.Get(Wave,Rules,waveSeed); spawnCursor=0;
            EnemiesPending = currentPlan.Spawns.Count;
            entryOffsets=new float[EnemiesPending];
            var spread=Mathf.Min(Rules.EntrySpreadBase+Wave*Rules.EntrySpreadGrowth,Rules.EntrySpreadCap);
            for(var i=1;i<entryOffsets.Length;i++) entryOffsets[i]=Random.Range(0.15f,spread);
            System.Array.Sort(entryOffsets);
            waveStarted=Time.time+(Wave==1 || SpawnInterval>0?WaveOpeningSeconds:0);
            nextSpawn=waveStarted;
            nextWave = -1;
            WaveBannerUntil = Time.time + 3.4f;
        }

        public EnemyAircraft SpawnAircraft(Vector2 position, int direction,AircraftRole role=AircraftRole.Bomber,bool armored=false,int attackRank=0,bool speedBoosted=false)
        {
            var go = new GameObject("Raider W"+Wave);
            go.transform.position = position;
            var aircraft = go.AddComponent<EnemyAircraft>();
            aircraft.Initialize(this,direction,Wave,role,armored,attackRank,speedBoosted);
            Aircraft.Add(aircraft);
            return aircraft;
        }

        public EnemyAircraft SpawnTransport(SupplyKind cargo,Vector2 position,int direction)
        {
            // Optional targets are bounded even when the player ignores them.
            foreach(var old in Aircraft.ToArray()) if(old!=null && old.IsSupplyTransport) old.Escape();
            var transport=SpawnAircraft(position,direction,AircraftRole.SupplyTransport);
            transport.SetCargo(cargo); TransportsSpawned++;
            Announce(()=>UiLanguage.Text("CARGO: ","补给机：")+SupplyRules.Name(cargo)+UiLanguage.Text("  /  SHOOT IT DOWN","  /  击落获取"));
            return transport;
        }

        public bool CanReachPickup(Vector2 position)
        {
            return Player.Health>0 && Vector2.Distance(position,Player.Body.position)<Rules.PickupReach
                && !Physics.Linecast(Player.Body.position,position,1 << GameArt.TerrainLayer,QueryTriggerInteraction.Ignore);
        }

        public RescuePilot SpawnPilot(Vector2 position)
        {
            Pilots.RemoveAll(p=>p==null);
            if(Pilots.Count>=Rules.PilotCap) Pilots[0].Expire();
            var go=new GameObject("Wounded pilot"); go.transform.position=position;
            var pilot=go.AddComponent<RescuePilot>(); pilot.Initialize(this);
            Pilots.Add(pilot); PilotsSpawned++;
            Announce("WOUNDED PILOT  /  REACH FOR AN EXTRA LIFE","受伤飞行员  /  接近可获得一条命");
            return pilot;
        }

        public void Rescue(RescuePilot pilot)
        {
            if(!Playing || Player.Health<=0 || !Pilots.Remove(pilot)) return;
            PilotsRescued++;
            if(Lives<MaximumLives) { Lives++; Announce("PILOT CAPTURED  /  EXTRA LIFE","已救援飞行员  /  生命 +1"); }
            else
            {
                Score+=Rules.DuplicateScore;
                Announce("LIVES FULL  /  RESCUE +"+Rules.DuplicateScore,"生命已满  /  救援加 "+Rules.DuplicateScore+" 分");
            }
            PlaySound(2);
        }

        private void Announce(string english,string chinese) => Announce(()=>UiLanguage.Text(english,chinese));
        private void Announce(System.Func<string> message) { supplyAnnouncement=message; SupplyFeedbackUntil=Time.time+3; }

        private void ActivateEmp()
        {
            EmpActivations++;
            foreach(var aircraft in Aircraft.ToArray())
            {
                if(aircraft==null || aircraft.IsSupplyTransport) continue;
                var viewport=MainCamera.WorldToViewportPoint(aircraft.transform.position);
                if(viewport.z<=0 || viewport.x<0 || viewport.x>1 || viewport.y<0 || viewport.y>1) continue;
                aircraft.Damage(10000,false); EmpKills++;
            }
            Effects.Pulse(Player.Body.position);
        }

        public void LaunchRocket(Vector2 origin, Vector2 direction)
        {
            direction.Normalize();
            var go = new GameObject("Player rocket"); go.transform.position = origin;
            RocketsAlive++; ShotsFired++;
            var projectile = go.AddComponent<CombatProjectile>();
            projectile.Initialize(this,direction*Rules.RocketSpeed,ProjectileKind.PlayerRocket);
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
            go.AddComponent<CombatProjectile>().Initialize(this,(target-position).normalized*Rules.RocketHelicopterSpeed,ProjectileKind.EnemyRocket);
            PlaySound(0);
        }

        public SupplyCrate SpawnSupply(SupplyKind kind,Vector2 position)
        {
            Supplies.RemoveAll(s=>s==null);
            if(Supplies.Count>=Rules.CrateCap) Supplies[0].Expire();
            var go=new GameObject("Supply "+kind); go.transform.position=position;
            var crate=go.AddComponent<SupplyCrate>(); crate.Initialize(this,kind);
            Supplies.Add(crate); SuppliesDropped++;
            return crate;
        }

        public void CollectSupply(SupplyCrate crate)
        {
            if(!Supplies.Remove(crate)) return;
            SuppliesCollected++;
            var capacityLimit=Rules.RocketSlotsCap==0?int.MaxValue:Rules.RocketSlotsCap;
            if(PiercingEnabled) capacityLimit=Mathf.Min(capacityLimit,Rules.PiercingSlotsCap);
            if(crate.Kind==SupplyKind.RocketSlot && RocketCapacity<capacityLimit)
            { RocketCapacity++; Announce("ROCKET SLOTS  "+RocketCapacity,"火箭位  "+RocketCapacity); }
            else if(crate.Kind==SupplyKind.Piercing && !PiercingEnabled)
            {
                PiercingEnabled=true;
                RocketCapacity=Mathf.Min(RocketCapacity,Rules.PiercingSlotsCap);
                Announce("PIERCING ROCKETS ONLINE","穿甲火箭已启用");
            }
            else if(crate.Kind==SupplyKind.JumpBoots && !MultiJumpEnabled)
            { MultiJumpEnabled=true; Player.GrantMultiJump(); Announce("JUMP BOOTS  /  AIR JUMPS UNLOCKED","跳跳靴  /  连续空中跳跃已解锁"); }
            else if(crate.Kind==SupplyKind.Emp)
            { var before=EmpKills; ActivateEmp(); Announce("EMP DISCHARGED  /  "+(EmpKills-before)+" TARGETS","电磁脉冲已释放  /  击落 "+(EmpKills-before)+" 架"); }
            else
            {
                Score+=Rules.DuplicateScore;
                Announce("UPGRADE ACTIVE  /  +"+Rules.DuplicateScore+" SCORE","升级已生效  /  加 "+Rules.DuplicateScore+" 分");
            }
            PlaySound(2);
        }

        public void Explode(Vector2 position, bool rocket,ISet<EnemyAircraft> excludedTargets=null,EnemyAircraft directTarget=null)
        {
            Explosions++;
            // Resolve exposure BEFORE excavating: intact soil absorbs this blast,
            // then its crater can leave the player exposed to the next bomb.
            if (rocket)
            {
                var targets = Aircraft.ToArray();
                foreach (var target in targets)
                    if (target != null && (excludedTargets==null || !excludedTargets.Contains(target))
                        && (target==directTarget || Vector2.Distance(target.transform.position,position)<Rules.RocketDamageRadius))
                        target.Damage(Mathf.RoundToInt(Rules.RocketDamage*Ground.BlastTransmission(position,target.transform.position)));
            }
            else if (Player.Health > 0)
            {
                var distance = Vector2.Distance(Player.transform.position,position);
                if (distance < Rules.BombDamageRadius)
                {
                    var center=(Vector2)Player.Body.position;
                    LastBlastOrigin=position;
                    LastBlastExposure=Ground.BlastTransmission(position,center+Vector2.up*0.60f)*0.25f
                        + Ground.BlastTransmission(position,center+Vector2.up*0.08f)*0.55f
                        + Ground.BlastTransmission(position,center-Vector2.up*0.55f)*0.20f;
                    var unshielded=Mathf.RoundToInt(Mathf.Lerp(Rules.BombDamageNear,Rules.BombDamageFar,distance/Rules.BombDamageRadius));
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
            if(!rocket)
                foreach(var pilot in Pilots.ToArray())
                    if(pilot!=null && Vector2.Distance(pilot.transform.position,position)<Rules.BombDamageRadius
                        && Ground.BlastTransmission(position,pilot.transform.position)>0.2f) pilot.Expire();
            Ground.CarveBlast(position,rocket);
            if(!rocket) HostileImpact?.Invoke(position);
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

        public bool AircraftDestroyed(EnemyAircraft aircraft,bool directCombatKill)
        {
            Aircraft.Remove(aircraft);
            var falling=false;
            if(aircraft.IsSupplyTransport)
            { TransportsShotDown++; SpawnSupply(aircraft.Cargo,aircraft.transform.position); }
            else
            {
                Kills++; Score += aircraft.ScoreValue*Wave;
                if(!Rules.PilotRequiresCrash && directCombatKill && Random.value<PilotDropChance) SpawnPilot(aircraft.transform.position);
                falling=directCombatKill && FallingWrecks<Rules.FallingWreckCap
                    && Mathf.Abs(aircraft.transform.position.x)<=30 && Random.value<CrashChance;
                if(Rules.PilotRequiresCrash && falling && Random.value<PilotDropChance) SpawnPilot(aircraft.transform.position);
            }
            Flash(aircraft.transform.position,2.5f);
            PlaySound(1);
            CameraRig.Shake(0.12f);
            if(!falling) return false;
            FallingWrecks++;
            return true;
        }

        public void FinishCrash(Vector2? impact)
        {
            FallingWrecks--;
            if(!impact.HasValue) return;
            var position=impact.Value;
            if(Player.Health>0 && (Rules.CrashSolidChance==1 || Random.value<Rules.CrashSolidChance)
                && Ground.TryPlaceWreck(position,Player.Body.position)) WrecksLanded++;
            Flash(position,1.4f);
            PlaySound(1);
        }

        public void SoldierDown()
        {
            Lives--;
            Flash(Player.transform.position,1.6f);
            if (Lives <= 0)
            {
                GameOver = true;
                UpdateCursor();
                BestWave = Mathf.Max(BestWave,CompletedWaves);
                if (!OverrideControls && !Application.isBatchMode)
                {
                    PlayerPrefs.SetInt(BestWaveKey,BestWave); PlayerPrefs.Save();
                }
            }
            else
            {
                if(Rules.LoseUpgrade) LoseUpgrade();
                respawnAt = Time.time + Rules.RespawnDelay;
            }
        }

        private void LoseUpgrade()
        {
            var choices=new List<SupplyKind>();
            if(MultiJumpEnabled) choices.Add(SupplyKind.JumpBoots);
            if(PiercingEnabled) choices.Add(SupplyKind.Piercing);
            if(RocketCapacity>Rules.RocketSlots) choices.Add(SupplyKind.RocketSlot);
            if(choices.Count==0) return;
            var lost=choices[Random.Range(0,choices.Count)];
            if(lost==SupplyKind.JumpBoots) MultiJumpEnabled=false;
            else if(lost==SupplyKind.Piercing) PiercingEnabled=false;
            else RocketCapacity--;
            Announce(()=>UiLanguage.Text("UPGRADE LOST: ","失去升级：")+SupplyRules.Name(lost));
        }

        public void SetPaused(bool pause)
        {
            Paused=pause; Time.timeScale=pause?0:1;
            UpdateCursor();
        }
        private void UpdateCursor()
        {
            if(!Application.isBatchMode && !Application.isEditor) Cursor.visible=!Playing;
        }
        public void Restart() => Reload(Rules?.Mode);
        public void ReturnToMenu() => Reload(null);
        private void Reload(BalanceMode? mode)
        {
            restartMode=mode;
            Time.timeScale=1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

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
