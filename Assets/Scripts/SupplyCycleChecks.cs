using System;
using System.Collections;
using UnityEngine;

namespace Airstrike
{
    public static class SupplyCycleChecks
    {
        public static IEnumerator Run(AirstrikeGame game,GameplayAutomation.Report report,Action<bool,string> check,Action<EnemyAircraft,bool> live)
        {
            var player=game.Player;
            // One offscreen hostile keeps the scheduler stable while optional
            // targets, drops, rescues and expiry are exercised independently.
            var keeper=game.SpawnAircraft(new Vector2(34,15),1);
            keeper.Speed=0; keeper.AttacksEnabled=false;
            foreach(var aircraft in game.Aircraft.ToArray()) if(aircraft!=null && aircraft!=keeper) aircraft.Damage(1000,false);
            foreach(var crate in game.Supplies.ToArray()) crate.Expire();
            foreach(var pilot in game.Pilots.ToArray()) pilot.Expire();
            foreach(var projectile in UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)) projectile.Cancel();
            var platform=Box(new Vector3(0,3.75f,0),new Vector3(24,0.5f,3));
            Warp(player,new Vector3(-8,4.85f,0));
            var constraints=player.Body.constraints; player.Body.constraints=RigidbodyConstraints.FreezeAll;
            game.TestControls=new CombatInput();

            var transport=game.SpawnTransport(SupplyKind.JumpBoots,new Vector2(-2,9),1);
            live(transport,true);
            var start=transport.transform.position; var bombs=game.BombsDropped; var drops=game.SuppliesDropped;
            yield return new WaitForSeconds(0.3f);
            check(transport!=null && transport.transform.position.x>start.x+1 && game.BombsDropped==bombs,
                "A cargo transport flies across the battlefield without attacking the player.");
            check(game.SuppliesDropped==drops && !game.MultiJumpEnabled,
                "A living cargo transport does not grant its upgrade or release a crate.");
            transport.Speed=0; transport.AttacksEnabled=false; live(transport,false);
            game.TestControls=new CombatInput { fire=true,aim=transport.transform.position };
            yield return new WaitForSeconds(0.7f);
            game.TestControls=new CombatInput();
            report.transportRocketDrops=game.SuppliesDropped-drops;
            check(transport==null && report.transportRocketDrops==1 && !game.MultiJumpEnabled,
                "An aimed player rocket shoots down a cargo transport and releases exactly one uncollected crate.");
            var boots=game.Supplies.Find(c=>c!=null && c.Kind==SupplyKind.JumpBoots);
            check(boots!=null,"The dropped crate preserves the transport's jump-boots cargo.");
            Warp(player,new Vector3(boots.transform.position.x,4.85f,0));
            var deadline=Time.time+3;
            while(!game.MultiJumpEnabled && Time.time<deadline) yield return null;
            check(game.MultiJumpEnabled,"Reaching the descending boots crate unlocks repeated air jumps.");
            player.Body.constraints=constraints;
            yield return new WaitForSeconds(0.2f);
            var jumps=player.SuccessfulJumps;
            for(var i=0;i<3;i++) { yield return Pulse(game,new CombatInput { jump=true }); yield return new WaitForSeconds(0.12f); }
            check(player.SuccessfulJumps>=jumps+3 && player.JumpsLeft>2 && !player.Grounded,
                "Jump boots permit a third jump before landing while ordinary double-jump limits remain separately tested.");
            Warp(player,new Vector3(player.Body.position.x,game.Rules.AltitudeLimit-0.05f,0));
            yield return Pulse(game,new CombatInput { jump=true });
            yield return new WaitForSeconds(0.1f);
            check(player.Body.position.y<=game.Rules.AltitudeLimit+0.01f,
                "Repeated air jumps respect the battle's upper boundary instead of leaving the camera view.");
            var score=game.Score;
            game.SpawnSupply(SupplyKind.JumpBoots,player.Body.position);
            yield return new WaitForSeconds(0.1f);
            check(game.MultiJumpEnabled && game.Score==score+150,"Duplicate jump boots convert to score instead of stacking another effect.");
            var lives=game.Lives; player.ApplyDamage(1000,true);
            yield return new WaitForSeconds(1.2f);
            report.bootsSurviveRespawn=game.Lives==lives-1 && player.Health==100 && game.MultiJumpEnabled && player.JumpsLeft>2;
            check(report.bootsSurviveRespawn,"Jump boots persist through a reserve-life respawn.");

            Warp(player,new Vector3(0,5,0)); player.Body.constraints=RigidbodyConstraints.FreezeAll;
            yield return new WaitForSeconds(0.4f);
            var visibleA=game.SpawnAircraft(new Vector2(3,9),1);
            var visibleB=game.SpawnAircraft(new Vector2(-4,8),1,AircraftRole.Helicopter);
            var outside=game.SpawnAircraft(new Vector2(-34,9),1);
            transport=game.SpawnTransport(SupplyKind.Emp,new Vector2(8,9),1);
            foreach(var aircraft in new[] {visibleA,visibleB,outside,transport}) { aircraft.Speed=0; aircraft.AttacksEnabled=false; }
            game.DropBomb(new Vector2(14,12),Vector2.zero);
            var empKills=game.EmpKills; var pilots=game.PilotsSpawned;
            game.PilotDropChance=1;
            game.SpawnSupply(SupplyKind.Emp,player.Body.position);
            yield return new WaitForSeconds(0.12f);
            report.empTargetsCleared=game.EmpKills-empKills;
            check(visibleA==null && visibleB==null && report.empTargetsCleared==2,
                "Collecting EMP immediately knocks down the two hostile aircraft in camera view.");
            check(outside!=null && transport!=null && game.HostileProjectilesAlive>0,
                "EMP leaves offscreen hostiles, cargo transports and already-falling ordnance intact.");
            check(game.PilotsSpawned==pilots,"EMP defeats do not generate rescue pilots or a chained life windfall.");
            game.PilotDropChance=0;
            game.SpawnSupply(SupplyKind.Emp,player.Body.position);
            yield return new WaitForSeconds(0.1f);
            check(game.Effects.GetComponentsInChildren<EmpPulse>(true).Length==1,
                "Repeated EMP pickups reuse a single pulse effect.");
            yield return new WaitForSeconds(0.8f);
            check(!game.Effects.GetComponentInChildren<EmpPulse>(true).gameObject.activeSelf,
                "The EMP pulse deactivates after its visual lifetime.");
            foreach(var projectile in UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)) projectile.Cancel();
            outside.Damage(1000,false);

            drops=game.SuppliesDropped;
            transport=game.SpawnTransport(SupplyKind.Piercing,new Vector2(35.9f,9),1);
            var escaped=game.TransportsEscaped;
            live(transport,true);
            yield return new WaitForSeconds(0.15f);
            live(transport,false);
            check(transport==null && game.TransportsEscaped==escaped+1 && game.SuppliesDropped==drops,
                "An unshot cargo plane exits the map without releasing free cargo.");

            game.PilotDropChance=1;
            var casualty=game.SpawnAircraft(new Vector2(6,7.5f),1); casualty.Speed=0; casualty.AttacksEnabled=false;
            pilots=game.PilotsSpawned; lives=game.Lives;
            casualty.Damage(110);
            game.PilotDropChance=0;
            check(game.PilotsSpawned==pilots+1 && game.Pilots.Count==1,
                "A defeated hostile aircraft can release a wounded pilot through the configured drop chance.");
            var rescue=game.Pilots[0]; var pilotY=rescue.transform.position.y;
            yield return new WaitForSeconds(0.25f);
            check(rescue!=null && rescue.transform.position.y<pilotY-0.3f && game.Lives==lives,
                "A wounded pilot descends by parachute and does not grant a life remotely.");
            deadline=Time.time+2;
            while(rescue!=null && !rescue.Landed && Time.time<deadline) yield return null;
            check(rescue!=null && rescue.Landed,"A descending pilot lands on the current terrain collision surface.");
            Warp(player,new Vector3(rescue.transform.position.x-0.65f,4.85f,0));
            yield return new WaitForSeconds(0.1f);
            report.rescueLifeGain=game.Lives-lives;
            check(rescue==null && report.rescueLifeGain==1,"Approaching a wounded pilot captures him and grants exactly one life.");

            Warp(player,new Vector3(0,5,0));
            var wall=Box(new Vector3(0.4f,5,0),new Vector3(0.12f,3,3));
            var rescued=game.PilotsRescued; var activations=game.EmpActivations;
            rescue=game.SpawnPilot(new Vector2(0.8f,5));
            game.SpawnSupply(SupplyKind.Emp,new Vector2(0.8f,5));
            yield return new WaitForSeconds(0.12f);
            check(game.PilotsRescued==rescued && game.EmpActivations==activations,
                "A thin soil wall blocks nearby pilot capture and supply pickup.");
            wall.GetComponent<Collider>().enabled=false; UnityEngine.Object.Destroy(wall);
            yield return new WaitForSeconds(0.12f);
            check(game.PilotsRescued==rescued+1 && game.EmpActivations==activations+1,
                "Removing the blocking wall lets the player collect the nearby pilot and crate.");
            while(game.Lives<game.MaximumLives)
            {
                game.SpawnPilot(player.Body.position);
                yield return new WaitForSeconds(0.08f);
            }
            score=game.Score;
            game.SpawnPilot(player.Body.position);
            yield return new WaitForSeconds(0.08f);
            report.maximumRescueLives=game.Lives;
            check(game.Lives==game.MaximumLives && game.Score==score+150,
                "Rescue lives stop at six and further rescues award score.");
            rescue=game.SpawnPilot(new Vector2(15,7));
            game.Explode(new Vector2(15,7),false);
            check(rescue.Finished && !game.Pilots.Contains(rescue),"An exposed wounded pilot can be lost to an enemy explosion.");

            for(var i=0;i<4;i++)
            {
                game.SpawnPilot(new Vector2(15+i,8));
                game.SpawnSupply(SupplyKind.Emp,new Vector2(-15-i,8));
            }
            check(game.Pilots.Count==3 && game.Supplies.Count==3,"Uncollected pilots and supply crates each stay bounded at three.");
            transport=game.SpawnTransport(SupplyKind.Emp,new Vector2(-28,9),1); transport.Speed=0;
            var cargoPosition=transport.transform.position; var cratePosition=game.Supplies[0].transform.position;
            game.SetPaused(true);
            yield return new WaitForSecondsRealtime(0.2f);
            check(transport.transform.position==cargoPosition && game.Supplies[0].transform.position==cratePosition,
                "Pause freezes optional aircraft and descending rewards together with combat.");
            game.SetPaused(false);
            var wave=game.Wave;
            game.WaveOpeningSeconds=1000; // Keep the next wave's ordinary AI out of the expiry fixture.
            game.DropBomb(new Vector2(30,22),Vector2.zero);
            keeper.Damage(1000,false);
            deadline=Time.time+1;
            while(game.Wave==wave && Time.time<deadline) yield return null;
            check(game.Wave>wave && transport!=null && game.Pilots.Count==3 && game.Supplies.Count==3 && game.HostileProjectilesAlive>0,
                "The live scheduler enters the next wave while an ignored cargo plane, pilots, crates and a regular bomb remain.");
            keeper=game.SpawnAircraft(new Vector2(34,15),1); keeper.Speed=0; keeper.AttacksEnabled=false;
            // Advance only this opt-in acceptance run's simulation clock; no
            // real-time stability/performance claim follows from this fixture.
            try { Time.timeScale=12; yield return new WaitForSeconds(36); }
            finally { Time.timeScale=1; }
            report.optionalResourcesExpired=game.Pilots.Count==0 && game.Supplies.Count==0;
            check(report.optionalResourcesExpired && transport==null,
                "Ignored pilots, crates and cargo planes expire without leaving permanent reward objects.");
            check(game.HostileAircraftCount==1 && game.Aircraft.Contains(keeper),
                "Optional resource objects do not count as hostile aircraft.");
            player.Body.constraints=constraints;
            Warp(player,new Vector3(-6,game.Ground.SurfaceAt(-6)+0.9f,0));
            platform.GetComponent<Collider>().enabled=false; UnityEngine.Object.Destroy(platform);
        }

        private static GameObject Box(Vector3 position,Vector3 size)
        {
            var go=new GameObject("Supply-cycle collision fixture"); go.layer=GameArt.TerrainLayer;
            go.transform.position=position; go.AddComponent<BoxCollider>().size=size; Physics.SyncTransforms(); return go;
        }
        private static void Warp(SoldierController player,Vector3 position)
        { player.Body.position=position; player.transform.position=position; player.Body.linearVelocity=Vector3.zero; Physics.SyncTransforms(); }
        private static IEnumerator Pulse(AirstrikeGame game,CombatInput input)
        { game.TestControls=input; yield return null; game.TestControls=new CombatInput(); yield return new WaitForFixedUpdate(); }
    }
}
