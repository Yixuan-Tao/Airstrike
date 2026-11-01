using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Airstrike
{
    // Opt-in acceptance run. Sends controls through the same path as live input;
    // never synthesizes OS keyboard/mouse events or interacts with other windows.
    public sealed class GameplayAutomation : MonoBehaviour
    {
        [Serializable]
        public sealed class Report
        {
            public bool passed;
            public string error;
            public string unityVersion;
            public bool isEditor;
            public float durationSeconds;
            public List<string> checks = new List<string>();
            public float movedDistance, doubleJumpRise;
            public int terrainCellsRemoved, kills, score, maximumRockets, runtimeErrors;
        }
        private static GameplayAutomation active;
        private AirstrikeGame game;
        private string reportPath;
        private readonly Report report = new Report();
        private bool freezeAircraft, finished;
        private float started;

        public static void AttachIfRequested(AirstrikeGame owner)
        {
            if(active!=null) { active.game=owner; owner.OverrideControls=true; return; }
            var args=Environment.GetCommandLineArgs();
            for(var i=0;i+1<args.Length;i++)
                if(args[i]=="--gameplay-smoke") { Begin(owner,args[i+1]); return; }
        }

        public static void Begin(AirstrikeGame owner,string path)
        {
            if(active!=null) throw new InvalidOperationException("An acceptance run is already active.");
            var driver=new GameObject("Gameplay acceptance driver").AddComponent<GameplayAutomation>();
            active=driver; driver.game=owner; driver.reportPath=Path.GetFullPath(path);
            owner.OverrideControls=true;
            DontDestroyOnLoad(driver.gameObject);
            driver.started=Time.realtimeSinceStartup;
            Application.logMessageReceived+=driver.OnLog;
            Application.targetFrameRate=60; QualitySettings.vSyncCount=0;
            driver.StartCoroutine(driver.Run());
        }

        private void Update()
        {
            if(finished || game==null) return;
            report.maximumRockets=Mathf.Max(report.maximumRockets,game.RocketsAlive);
            if(freezeAircraft)
                foreach(var aircraft in game.Aircraft)
                    if(aircraft!=null) { aircraft.Speed=0; aircraft.DropBombs=false; }
            if(Time.realtimeSinceStartup-started>45) Finish("Acceptance run timed out.");
        }

        private IEnumerator Run()
        {
            var suite=Suite();
            while(!finished)
            {
                object current=null; bool moved=false; string error=null;
                try { moved=suite.MoveNext(); if(moved) current=suite.Current; }
                catch(Exception exception) { error=exception.ToString(); }
                if(error!=null) { Finish(error); yield break; }
                if(!moved) { Finish(null); yield break; }
                yield return current;
            }
        }

        private IEnumerator Suite()
        {
            game.TestControls=new CombatInput { aim=new Vector2(-2,10) };
            yield return new WaitForSeconds(1);
            Check(game.Player.Grounded,"Gravity and ground collision settle the player.");

            var fixture=game.SpawnAircraft((Vector2)game.Player.transform.position+new Vector2(-4,9),1);
            var aircraftStart=fixture.transform.position;
            var bombsBefore=game.BombsDropped;
            yield return new WaitForSeconds(1.5f);
            Check(fixture!=null && fixture.transform.position.x-aircraftStart.x>3,"Enemy aircraft traverses the arena.");
            Check(game.BombsDropped>bombsBefore,"Enemy AI drops a live bomb near the player.");
            freezeAircraft=true;

            var start=game.Player.Body.position;
            game.TestControls=new CombatInput { move=1,aim=new Vector2(0,10) };
            yield return new WaitForSeconds(0.65f);
            report.movedDistance=game.Player.Body.position.x-start.x;
            Check(report.movedDistance>3,"Movement controls move the Rigidbody across the ground.");

            var jumpStart=game.Player.Body.position.y;
            var jumpCount=game.Player.SuccessfulJumps;
            game.TestControls=new CombatInput { jump=true,aim=new Vector2(0,10) };
            yield return null;
            game.TestControls=new CombatInput { aim=new Vector2(0,10) };
            yield return new WaitForSeconds(0.15f);
            var firstHeight=game.Player.Body.position.y;
            game.TestControls=new CombatInput { jump=true,aim=new Vector2(0,10) };
            yield return null;
            game.TestControls=new CombatInput { aim=new Vector2(0,10) };
            yield return new WaitForSeconds(0.28f);
            report.doubleJumpRise=game.Player.Body.position.y-jumpStart;
            Check(game.Player.SuccessfulJumps==jumpCount+2 && game.Player.Body.position.y>firstHeight+0.8f,"A second jump gains height while airborne.");
            game.TestControls=new CombatInput { jump=true,aim=new Vector2(0,10) };
            yield return null;
            game.TestControls=new CombatInput { aim=new Vector2(0,10) };
            yield return new WaitForSeconds(0.1f);
            Check(game.Player.SuccessfulJumps==jumpCount+2,"A third airborne jump is rejected.");
            yield return new WaitForSeconds(2);
            Check(game.Player.Grounded && game.Player.JumpsLeft==2,"Landing restores both jumps.");

            var cells=game.Ground.SolidCount;
            var digX=game.Player.transform.position.x+1.8f;
            game.TestControls=new CombatInput { dig=true,aim=new Vector2(digX,game.Ground.SurfaceAt(digX)-0.8f) };
            yield return new WaitForSeconds(0.22f);
            game.TestControls=new CombatInput { aim=new Vector2(0,10) };
            Check(game.Ground.SolidCount<cells && game.DigOperations>0,"Nearby digging removes terrain cells and rebuilds collision.");

            cells=game.Ground.SolidCount;
            var targetX=game.Player.transform.position.x+6;
            game.TestControls=new CombatInput { fire=true,aim=new Vector2(targetX,game.Ground.SurfaceAt(targetX)-0.3f) };
            yield return new WaitForSeconds(0.1f);
            game.TestControls=new CombatInput { aim=new Vector2(targetX,0) };
            yield return new WaitForSeconds(0.8f);
            Check(game.Ground.SolidCount<cells && game.Explosions>0 && game.RocketsAlive==0,"A fired rocket impacts terrain, excavates a crater and releases the rocket slot.");

            game.Player.Respawn();
            yield return new WaitForSeconds(2.3f);
            var hp=game.Player.Health;
            game.DropBomb((Vector2)game.Player.transform.position+Vector2.up*4,Vector2.zero);
            yield return new WaitForSeconds(1.3f);
            Check(game.Player.Health<hp,"A falling bomb collides and damages the player.");

            game.Player.Body.position=new Vector3(-12,game.Ground.SurfaceAt(-12)+1.1f,0);
            game.Player.Body.linearVelocity=Vector3.zero;
            game.SpawnInterval=0.2f;
            var deadline=Time.time+12;
            while(game.Wave<2 && Time.time<deadline)
            {
                EnemyAircraft target=null;
                foreach(var enemy in game.Aircraft) if(enemy!=null) { target=enemy; break; }
                if(target!=null)
                {
                    target.Speed=0; target.DropBombs=false;
                    target.transform.position=game.Player.transform.position+new Vector3(6,6,0);
                    game.TestControls=new CombatInput { fire=true,aim=target.transform.position };
                }
                else game.TestControls=new CombatInput { aim=new Vector2(-6,8) };
                yield return null;
            }
            game.TestControls=new CombatInput { aim=new Vector2(-6,8) };
            Check(game.Kills>=3 && game.Score>=300,"Aimed rockets destroy aircraft and award score.");
            Check(game.Wave>=2,"Clearing all threats advances the wave.");
            Check(report.maximumRockets==1,"Held fire respects one simultaneous rocket.");
            report.kills=game.Kills; report.score=game.Score; report.terrainCellsRemoved=game.Ground.RemovedCount;

            game.TestControls=new CombatInput { pause=true };
            yield return null;
            game.TestControls=new CombatInput();
            var pausedPosition=game.Player.Body.position;
            yield return new WaitForSecondsRealtime(0.2f);
            Check(game.Paused && Time.timeScale==0 && Vector3.Distance(pausedPosition,game.Player.Body.position)<0.01f,"Pause freezes the simulation.");
            game.TestControls=new CombatInput { pause=true };
            yield return null;
            game.TestControls=new CombatInput();
            Check(!game.Paused && Time.timeScale==1,"Resume restores simulation time.");

            for(var life=0;life<3 && !game.GameOver;life++)
            {
                var lives=game.Lives;
                game.Player.ApplyDamage(1000,true);
                Check(game.Lives==lives-1,"Lethal damage consumes one life.");
                if(!game.GameOver)
                {
                    yield return new WaitForSeconds(1.15f);
                    Check(game.Player.Health==100,"A reserve life respawns a healthy player.");
                }
            }
            Check(game.GameOver && game.Lives==0,"Exhausting all lives reaches the results state.");
            var previous=game;
            game.TestControls=new CombatInput { restart=true };
            yield return null;
            yield return null;
            Check(game!=previous && game.Wave==1 && game.Lives==3 && game.Score==0,"Restart reloads a clean battlefield and resets the run.");
            Check(report.runtimeErrors==0,"No runtime errors or exceptions during acceptance.");
        }

        private void Check(bool condition,string description)
        {
            if(!condition) throw new InvalidOperationException(description);
            report.checks.Add(description);
        }

        private void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) report.runtimeErrors++;
        }

        private void Finish(string error)
        {
            if(finished) return;
            finished=true;
            report.passed=error==null && report.runtimeErrors==0;
            report.error=error; report.unityVersion=Application.unityVersion; report.isEditor=Application.isEditor;
            report.durationSeconds=Time.realtimeSinceStartup-started;
            Application.logMessageReceived-=OnLog;
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath,JsonUtility.ToJson(report,true));
            Debug.Log("SKYBREACH_GAMEPLAY_ACCEPTANCE="+(report.passed?"PASS":"FAIL"));
#if !UNITY_EDITOR
            Application.Quit(report.passed?0:1);
#endif
        }

        private void OnDestroy() { Application.logMessageReceived-=OnLog; if(active==this) active=null; }
    }
}
