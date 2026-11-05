using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Airstrike
{
    public static class BattleVarietyChecks
    {
        public static IEnumerator Run(AirstrikeGame game,GameplayAutomation.Report report,Action<bool,string> check,Action<EnemyAircraft,bool> live)
        {
            var player=game.Player;
            var fast=game.SpawnAircraft((Vector2)player.Body.position+new Vector2(-6,9),1,AircraftRole.FastBomber);
            live(fast,true);
            var x=fast.transform.position.x; var bombs=game.BombsDropped;
            yield return new WaitForSeconds(0.3f);
            check(fast.transform.position.x-x>2,"A fast bomber crosses the arena faster than the standard aircraft.");
            yield return new WaitForSeconds(0.8f);
            report.fastBurstBombs=game.BombsDropped-bombs;
            check(report.fastBurstBombs==3,"A fast bomber releases a three-bomb burst while crossing the player.");
            live(fast,false); fast.AttacksEnabled=false; fast.Damage(1000); CancelProjectiles(false);

            var heli=game.SpawnAircraft((Vector2)player.Body.position+new Vector2(-8,8),1,AircraftRole.Helicopter);
            live(heli,true);
            var warnings=game.HelicopterWarnings; var rockets=game.EnemyRocketsFired;
            var deadline=Time.time+4;
            while(!heli.IsTelegraphing && Time.time<deadline) yield return null;
            check(heli.IsTelegraphing && game.EnemyRocketsFired==rockets,"A helicopter warns before releasing an aimed rocket.");
            var locked=heli.LockedAim;
            yield return new WaitForSeconds(0.2f);
            check(Mathf.Abs(heli.transform.position.x-(player.Body.position.x-5))<0.5f,"The helicopter settles into a hovering attack offset.");
            Warp(player,player.Body.position+Vector3.right*4);
            yield return new WaitForSeconds(0.1f);
            check(Vector2.Distance(heli.LockedAim,locked)<0.01f && game.EnemyRocketsFired==rockets,
                "The warning locks a position and gives the player time to move away.");
            deadline=Time.time+2;
            while(game.EnemyRocketsFired==rockets && Time.time<deadline) yield return null;
            CombatProjectile enemyRocket=null;
            foreach(var projectile in UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))
                if(projectile.IsEnemyRocket) { enemyRocket=projectile; break; }
            check(enemyRocket!=null,"The helicopter launches a live enemy rocket after the warning.");
            var rocketStart=(Vector2)enemyRocket.transform.position;
            yield return new WaitForSeconds(0.1f);
            check(enemyRocket!=null && Vector2.Dot(((Vector2)enemyRocket.transform.position-rocketStart).normalized,(locked-rocketStart).normalized)>0.98f,
                "The enemy rocket flies toward the warned position instead of tracking the dodge.");
            report.helicopterWarnings=game.HelicopterWarnings-warnings; report.aimedEnemyRockets=game.EnemyRocketsFired-rockets;
            live(heli,false); heli.AttacksEnabled=false; heli.Damage(1000); CancelProjectiles(false);

            var floor=new GameObject("Acceptance supply platform"); floor.layer=GameArt.TerrainLayer;
            floor.transform.position=new Vector3(0,4.75f,0); floor.AddComponent<BoxCollider>().size=new Vector3(12,0.5f,3);
            Warp(player,new Vector3(-3,5.85f,0));
            game.TestControls=new CombatInput();
            yield return new WaitForSeconds(0.2f);
            var crate=game.SpawnSupply(SupplyKind.RocketSlot,new Vector2(-0.8f,8.35f));
            var crateY=crate.transform.position.y;
            yield return new WaitForSeconds(0.3f);
            check(crate!=null && crate.transform.position.y<crateY-0.4f && game.RocketCapacity==1,
                "A supply crate descends and does not grant its upgrade remotely.");
            deadline=Time.time+3;
            while(game.RocketCapacity==1 && Time.time<deadline)
            {
                var error=-0.8f-player.Body.position.x;
                game.TestControls=new CombatInput { move=Mathf.Abs(error)>0.2f?Mathf.Sign(error):0 };
                yield return null;
            }
            game.TestControls=new CombatInput();
            check(game.RocketCapacity==2 && game.SuppliesCollected>0,"Walking to a dropped rocket-slot crate increases simultaneous rocket capacity.");
            report.upgradedCapacity=game.RocketCapacity;
            var shots=game.ShotsFired;
            game.TestControls=new CombatInput { fire=true,aim=new Vector2(30,player.Body.position.y+0.18f) };
            yield return new WaitForSeconds(0.7f);
            game.TestControls=new CombatInput();
            check(game.RocketsAlive==2 && game.ShotsFired==shots+2,"Held fire uses both unlocked slots without exceeding capacity.");
            CancelProjectiles(true);

            game.SpawnSupply(SupplyKind.Piercing,player.Body.position);
            yield return new WaitForSeconds(0.1f);
            check(game.PiercingEnabled && game.RocketCapacity==2,"A piercing crate unlocks penetration without changing rocket slots.");
            report.suppliesPickedUp=game.SuppliesCollected;
            floor.GetComponent<Collider>().enabled=false; UnityEngine.Object.Destroy(floor);

            var constraints=player.Body.constraints;
            player.Body.constraints=RigidbodyConstraints.FreezeAll;
            Warp(player,new Vector3(-18,6,0));
            var first=game.SpawnAircraft(new Vector2(-12,6.18f),1);
            var second=game.SpawnAircraft(new Vector2(-7,6.18f),1);
            first.Speed=second.Speed=0; first.AttacksEnabled=second.AttacksEnabled=false;
            Physics.SyncTransforms();
            shots=game.ShotsFired; var kills=game.Kills;
            yield return new WaitForSeconds(0.35f);
            yield return Pulse(game,new CombatInput { fire=true,aim=new Vector2(20,6.18f) });
            yield return new WaitForSeconds(0.65f);
            report.piercingKills=game.Kills-kills;
            check(first==null && second==null && report.piercingKills==2 && game.ShotsFired==shots+1,
                "One piercing rocket destroys two separated aircraft along its path.");

            first=game.SpawnAircraft(new Vector2(-12,6.18f),1,AircraftRole.Bomber,true);
            second=game.SpawnAircraft(new Vector2(-8,6.18f),1);
            first.Speed=second.Speed=0; first.AttacksEnabled=second.AttacksEnabled=false;
            Physics.SyncTransforms();
            yield return Pulse(game,new CombatInput { fire=true,aim=new Vector2(20,6.18f) });
            yield return new WaitForSeconds(0.65f);
            report.armoredPiercingHealth=first==null?0:first.Health;
            check(first!=null && first.Health==70 && second==null,
                "Penetration hits an armored aircraft once and excludes it from the second impact blast.");
            first.Damage(1000);

            var wall=new GameObject("Acceptance piercing wall"); wall.layer=GameArt.TerrainLayer;
            wall.transform.position=new Vector3(-15,6.18f,0); wall.AddComponent<BoxCollider>().size=new Vector3(0.15f,3,3);
            var behind=game.SpawnAircraft(new Vector2(-10,6.18f),1); behind.Speed=0; behind.AttacksEnabled=false;
            Physics.SyncTransforms();
            yield return Pulse(game,new CombatInput { fire=true,aim=behind.transform.position });
            yield return new WaitForSeconds(0.4f);
            check(behind!=null && behind.Health==100 && game.RocketsAlive==0,"Piercing rockets still stop at terrain instead of passing through walls.");
            wall.GetComponent<Collider>().enabled=false; UnityEngine.Object.Destroy(wall); behind.Damage(1000);
            player.Body.constraints=constraints;
            var lives=game.Lives; player.ApplyDamage(1000,true);
            yield return new WaitForSeconds(1.2f);
            report.upgradesSurviveRespawn=player.Health==100 && game.Lives==lives-1 && game.RocketCapacity==2 && game.PiercingEnabled;
            check(report.upgradesSurviveRespawn,"Collected upgrades persist through a reserve-life respawn.");

            var score=game.Score;
            game.SpawnSupply(SupplyKind.RocketSlot,player.Body.position);
            game.SpawnSupply(SupplyKind.RocketSlot,player.Body.position);
            yield return new WaitForSeconds(0.1f);
            check(game.RocketCapacity==3 && game.Score==score+150,"Rocket slots stop at three and a duplicate supply converts to score.");
            score=game.Score;
            game.SpawnSupply(SupplyKind.Piercing,player.Body.position);
            yield return new WaitForSeconds(0.1f);
            check(game.PiercingEnabled && game.Score==score+150,"A duplicate piercing supply gives score without stacking penetration.");
            report.maximumCapacity=game.RocketCapacity; report.duplicateSupplyBonus=300;
            constraints=player.Body.constraints;
            player.Body.constraints=RigidbodyConstraints.FreezeAll;
            Warp(player,new Vector3(-18,6,0));
            shots=game.ShotsFired;
            game.TestControls=new CombatInput { fire=true,aim=new Vector2(30,6.18f) };
            yield return new WaitForSeconds(1.05f);
            game.TestControls=new CombatInput();
            check(game.RocketsAlive==3 && game.ShotsFired==shots+3,"The fully upgraded launcher uses three slots and rejects a fourth shot.");
            CancelProjectiles(true);
            player.Body.constraints=constraints;
            Warp(player,new Vector3(-6,game.Ground.SurfaceAt(-6)+0.9f,0));

            game.SpawnInterval=0.06f; game.WaveOpeningSeconds=0.08f; game.WaveIntermissionSeconds=0.12f;
            var drops=game.SuppliesDropped;
            var cargoDestroyed=0;
            var seenWaves=new HashSet<int>(); var fastInWave2=false; var heliInWave3=false; var armorInWave4=false;
            deadline=Time.time+15;
            while(game.Wave<=6 && Time.time<deadline)
            {
                if(seenWaves.Add(game.Wave)) report.waveObservations.Add(game.Wave+": "+game.WaveName+" / "+WavePlan.Get(game.Wave).Spawns.Count+" scheduled");
                foreach(var aircraft in game.Aircraft.ToArray())
                {
                    if(aircraft==null) continue;
                    if(aircraft.SpawnWave==2 && aircraft.Role==AircraftRole.FastBomber) fastInWave2=true;
                    if(aircraft.SpawnWave==3 && aircraft.Role==AircraftRole.Helicopter) heliInWave3=true;
                    if(aircraft.SpawnWave==4 && aircraft.Armored) armorInWave4=true;
                    if(aircraft.IsSupplyTransport) cargoDestroyed++;
                    aircraft.Damage(1000);
                }
                CancelProjectiles(false);
                yield return null;
            }
            report.authoredWavesCleared=game.CompletedWaves; report.waveSupplyDrops=game.SuppliesDropped-drops;
            check(game.Wave>=7 && fastInWave2 && heliInWave3 && armorInWave4,"The live scheduler progresses through all six authored waves and introduces the intended threats.");
            report.authoredTransports=game.TransportsSpawned;
            check(report.authoredTransports==1,"The opening six waves launch a single optional cargo transport on wave four.");
            check(report.waveSupplyDrops==cargoDestroyed,"Only shot-down transports drop cargo; clearing a wave grants no automatic crate.");
        }

        private static IEnumerator Pulse(AirstrikeGame game,CombatInput input)
        { game.TestControls=input; yield return null; game.TestControls=new CombatInput(); yield return new WaitForFixedUpdate(); }

        private static void Warp(SoldierController player,Vector3 position)
        { player.Body.position=position; player.transform.position=position; player.Body.linearVelocity=Vector3.zero; Physics.SyncTransforms(); }

        private static void CancelProjectiles(bool playerRockets)
        {
            foreach(var projectile in UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))
                if(projectile.IsPlayerRocket==playerRockets) projectile.Cancel();
        }
    }
}
