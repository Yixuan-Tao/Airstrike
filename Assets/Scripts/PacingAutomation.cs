using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Airstrike
{
    // Opt-in, accelerated terrain observations. These are protected control
    // fixtures, not human survival, movement, difficulty or frame-rate tests.
    public sealed class PacingAutomation : MonoBehaviour
    {
        [Serializable] public sealed class Sample
        {
            public float seconds;
            public TerrainWindow anchorWindow, playerWindow;
        }
        [Serializable] public sealed class Scenario
        {
            public string name;
            public float seconds;
            public int bombs, impacts, localImpacts, releasesAwayFromPlayer;
            public List<float> releaseTimes=new List<float>(), impactXs=new List<float>();
            public List<Sample> samples=new List<Sample>();
            public TerrainWindow finalWindow;
        }
        [Serializable] public sealed class Report
        {
            public string version=AirstrikeGame.Version, error="";
            public bool passed;
            public int seed=1739, runtimeErrors;
            public float windowWidth=14, simulationSecondsPerCase=120, timeScale=8;
            public string scope="One first-wave bomber; protected surface-following player; legacy cadence replay uses current shallow craters. Patrol is a position probe, not validated navigation.";
            public List<Scenario> scenarios=new List<Scenario>();
            public List<string> checks=new List<string>();
        }

        private static PacingAutomation active;
        private AirstrikeGame game;
        private string path;
        private readonly Report report=new Report();
        private float started;
        private bool finished;

        public static void AttachIfRequested(AirstrikeGame owner)
        {
            if(active!=null) { active.game=owner; owner.OverrideControls=true; owner.PilotDropChance=0; return; }
            var args=Environment.GetCommandLineArgs();
            for(var i=0;i+1<args.Length;i++) if(args[i]=="--pacing-probe")
            {
                var driver=new GameObject("Pacing terrain observation").AddComponent<PacingAutomation>();
                active=driver; driver.game=owner; driver.path=Path.GetFullPath(args[i+1]);
                owner.OverrideControls=true; owner.PilotDropChance=0;
                DontDestroyOnLoad(driver.gameObject); driver.started=Time.realtimeSinceStartup;
                Application.logMessageReceived+=driver.OnLog;
                Application.targetFrameRate=60; QualitySettings.vSyncCount=0;
                driver.StartCoroutine(driver.Run());
                return;
            }
        }

        private void Update()
        { if(!finished && Time.realtimeSinceStartup-started>100) Finish("Pacing observation timed out."); }

        private IEnumerator Run()
        {
            var stack=new Stack<IEnumerator>(); stack.Push(Suite());
            while(!finished)
            {
                if(stack.Count==0) { Finish(null); yield break; }
                object current=null; bool moved=false; string error=null;
                try { moved=stack.Peek().MoveNext(); if(moved) current=stack.Peek().Current; }
                catch(Exception exception) { error=exception.ToString(); }
                if(error!=null) { Finish(error); yield break; }
                if(!moved) { stack.Pop(); continue; }
                if(current is IEnumerator nested) stack.Push(nested); else yield return current;
            }
        }

        private IEnumerator Suite()
        {
            var names=new[] {"legacy_targeted_stationary","new_stationary","new_patrol"};
            for(var index=0;index<names.Length;index++)
            {
                if(index>0) { game.Restart(); yield return null; yield return null; }
                UnityEngine.Random.InitState(report.seed);
                var result=new Scenario {name=names[index]}; report.scenarios.Add(result);
                yield return Observe(result,index==0,index==2);
            }
            var legacy=report.scenarios[0]; var current=report.scenarios[1];
            Check(current.releasesAwayFromPlayer>0,"New normal bombing releases ordnance beyond the old player-proximity gate.");
            Check(current.localImpacts/(float)current.impacts<legacy.localImpacts/(float)legacy.impacts,
                "The controlled new flight concentrates a smaller fraction of explosions inside the fourteen-unit anchor region.");
            Check(current.finalWindow.minimumColumnSoil>=3,
                "The stationary new-cadence fixture retains at least three units of excavatable soil in every sampled column after 120 simulated seconds.");
            Check(report.runtimeErrors==0,"No runtime errors during the three controlled terrain observations.");
        }

        private IEnumerator Observe(Scenario result,bool legacy,bool patrol)
        {
            const float anchor=-6;
            var player=game.Player;
            player.Body.constraints=RigidbodyConstraints.FreezeAll;
            var begin=Time.time; var protectAt=-1f; var sampleAt=30f;
            var oldShotAt=0f; var direction=1; var normalCount=0;
            EnemyAircraft bomber=null;
            Action<Vector2> onImpact=point=>
            {
                result.impacts++; result.impactXs.Add(point.x);
                if(Mathf.Abs(point.x-anchor)<=report.windowWidth*0.5f) result.localImpacts++;
            };
            game.HostileImpact+=onImpact;
            Application.targetFrameRate=60; QualitySettings.vSyncCount=0;
            Time.timeScale=report.timeScale;
            try
            {
                while(Time.time-begin<report.simulationSecondsPerCase)
                {
                    var elapsed=Time.time-begin;
                    if(Time.time>=protectAt) { player.Respawn(); protectAt=Time.time+0.5f; }
                    var x=anchor+(patrol?Mathf.Sin(elapsed*0.1f)*5:0);
                    var p=new Vector3(x,game.Ground.SurfaceAt(x)+0.9f,0);
                    player.Body.position=p; player.transform.position=p; player.Body.linearVelocity=Vector3.zero;
                    Physics.SyncTransforms();
                    if(bomber==null)
                    {
                        bomber=game.Aircraft.Find(a=>a!=null && a.Role==AircraftRole.Bomber);
                        if(bomber!=null)
                        {
                            direction=bomber.transform.position.x<0?1:-1;
                            oldShotAt=Time.time+1.2f;
                            bomber.AttacksEnabled=!legacy;
                        }
                    }
                    if(bomber!=null)
                    {
                        if(legacy && Time.time>=oldShotAt && Mathf.Abs(bomber.transform.position.x-x)<8.5f)
                        {
                            oldShotAt=Time.time+2.42f;
                            game.DropBomb((Vector2)bomber.transform.position+Vector2.down*0.75f,new Vector2(direction*bomber.Speed*0.2f,-1));
                            result.releaseTimes.Add(elapsed);
                        }
                        else if(!legacy && bomber.NormalBombsReleased>normalCount)
                        {
                            normalCount=bomber.NormalBombsReleased;
                            result.releaseTimes.Add(bomber.LastNormalBombAt-begin);
                            if(Mathf.Abs(bomber.LastNormalBombPosition.x-x)>8.5f) result.releasesAwayFromPlayer++;
                        }
                    }
                    if(elapsed>=sampleAt)
                    {
                        result.samples.Add(Snapshot(elapsed,anchor,x)); sampleAt+=30;
                    }
                    yield return null;
                }
                result.seconds=Time.time-begin; result.bombs=game.BombsDropped;
                result.finalWindow=game.Ground.InspectWindow(anchor,report.windowWidth);
                result.samples.Add(Snapshot(result.seconds,anchor,player.Body.position.x));
                Check(game.Wave==1 && game.HostileAircraftCount==1 && result.bombs>=6 && result.impacts>0,
                    result.name+": a single live first-wave flight produced repeated physical bomb impacts.");
            }
            finally { game.HostileImpact-=onImpact; Time.timeScale=1; }
        }

        private Sample Snapshot(float seconds,float anchor,float playerX) => new Sample {
            seconds=seconds,anchorWindow=game.Ground.InspectWindow(anchor,report.windowWidth),
            playerWindow=game.Ground.InspectWindow(playerX,report.windowWidth)
        };
        private void Check(bool condition,string message)
        { if(!condition) throw new InvalidOperationException(message); report.checks.Add(message); }
        private void OnLog(string message,string stack,LogType type)
        { if(type==LogType.Error || type==LogType.Exception || type==LogType.Assert) report.runtimeErrors++; }
        private void Finish(string error)
        {
            if(finished) return; finished=true; Time.timeScale=1;
            report.error=error??""; report.passed=error==null && report.runtimeErrors==0;
            Application.logMessageReceived-=OnLog;
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path,JsonUtility.ToJson(report,true));
#if !UNITY_EDITOR
            Application.Quit(report.passed?0:1);
#endif
        }
        private void OnDestroy() { Application.logMessageReceived-=OnLog; if(active==this) active=null; }
    }
}
