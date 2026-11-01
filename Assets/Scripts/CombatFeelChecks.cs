using System;
using System.Collections;
using UnityEngine;

namespace Airstrike
{
    // Deterministic physical fixtures for input timing and collision regressions.
    // Only invoked by the opt-in gameplay acceptance driver.
    public static class CombatFeelChecks
    {
        public static IEnumerator Run(AirstrikeGame game,GameplayAutomation.Report report,Action<bool,string> check)
        {
            var player=game.Player;
            var fixtures=new GameObject("Acceptance collision fixtures");
            Box(fixtures.transform,new Vector3(11,2.75f,0),new Vector3(6,0.5f,3));
            Warp(player,new Vector3(13.2f,3.85f,0),Vector3.zero);
            game.TestControls=new CombatInput { move=1,aim=new Vector2(18,8) };
            yield return new WaitForSeconds(0.12f);
            var deadline=Time.time+1;
            while(player.Grounded && Time.time<deadline) yield return null;
            check(!player.Grounded && player.JumpsLeft==2,"Walking off an edge retains the first jump during coyote time.");
            var coyote=player.CoyoteJumps;
            yield return Pulse(game,new CombatInput { jump=true,aim=new Vector2(18,8) });
            yield return new WaitForFixedUpdate();
            check(player.CoyoteJumps==coyote+1 && player.JumpsLeft==1 && player.Body.linearVelocity.y>0,"A jump just after leaving an edge rises and preserves the air jump.");
            report.coyoteJumps=player.CoyoteJumps;

            Warp(player,new Vector3(10,6,0),Vector3.zero);
            yield return new WaitForSeconds(0.16f);
            yield return Pulse(game,new CombatInput { jump=true });
            yield return new WaitForSeconds(0.16f);
            check(player.JumpsLeft==0,"The landing-buffer fixture starts with both jumps consumed.");
            Warp(player,new Vector3(10,4.08f,0),Vector3.down*3);
            var buffered=player.BufferedLandingJumps;
            var jumps=player.SuccessfulJumps;
            yield return Pulse(game,new CombatInput { jump=true });
            yield return new WaitForSeconds(0.14f);
            check(player.BufferedLandingJumps==buffered+1 && player.SuccessfulJumps==jumps+1 && player.Body.linearVelocity.y>1,
                "A jump pressed shortly before landing executes on contact.");
            report.bufferedJumps=player.BufferedLandingJumps;

            Warp(player,new Vector3(10,6,0),Vector3.zero);
            yield return new WaitForSeconds(0.16f);
            yield return Pulse(game,new CombatInput { jump=true });
            yield return new WaitForSeconds(0.16f);
            Warp(player,new Vector3(10,6,0),Vector3.down*3);
            jumps=player.SuccessfulJumps;
            yield return Pulse(game,new CombatInput { jump=true });
            yield return new WaitForSeconds(0.8f);
            check(player.SuccessfulJumps==jumps && player.Grounded,"An expired jump buffer does not cause a delayed automatic jump.");

            Box(fixtures.transform,new Vector3(12,3.25f,0),new Vector3(1.5f,0.5f,3));
            Warp(player,new Vector3(10,3.85f,0),Vector3.zero);
            game.TestControls=new CombatInput();
            yield return new WaitForSeconds(0.16f);
            var steps=player.StepUps;
            game.TestControls=new CombatInput { move=1 };
            yield return new WaitForSeconds(0.42f);
            game.TestControls=new CombatInput();
            check(player.StepUps>steps && player.Body.position.x>11.7f && player.Body.position.y>3.9f,
                "Ground movement climbs a single terrain-cell step without a jump.");
            report.stepUps=player.StepUps;

            Box(fixtures.transform,new Vector3(12,5.15f,0),new Vector3(3,0.4f,3));
            Warp(player,new Vector3(10,3.85f,0),Vector3.zero);
            yield return new WaitForSeconds(0.16f);
            steps=player.StepUps;
            game.TestControls=new CombatInput { move=1 };
            yield return new WaitForSeconds(0.6f);
            game.TestControls=new CombatInput();
            check(player.StepUps==steps && player.Body.position.x<11.1f && player.Body.position.y<4,
                "Step assistance refuses a rise that would intersect a low ceiling.");
            Clear(fixtures);

            Warp(player,new Vector3(-12,game.Ground.SurfaceAt(-12)+0.85f,0),Vector3.zero);
            yield return new WaitForSeconds(0.15f);
            fixtures=new GameObject("Acceptance thin wall");
            Box(fixtures.transform,new Vector3(-11.4f,player.Body.position.y,0),new Vector3(0.12f,4,3));
            var target=game.SpawnAircraft((Vector2)player.Body.position+new Vector2(4.5f,0.18f),1);
            target.Speed=0; target.AttacksEnabled=false;
            Physics.SyncTransforms();
            var shots=game.ShotsFired; var blocked=game.BlockedMuzzleShots; var targetHealth=target.Health;
            yield return Pulse(game,new CombatInput { fire=true,aim=target.transform.position });
            yield return new WaitForSeconds(0.3f);
            check(game.ShotsFired==shots+1 && game.BlockedMuzzleShots==blocked+1 && game.RocketsAlive==0,
                "A wall inside the barrel sweep receives the impact and releases the rocket slot.");
            check(target!=null && target.Health==targetHealth,"Point-blank firing cannot spawn a rocket beyond a thin wall.");
            report.blockedMuzzleShots=game.BlockedMuzzleShots;
            Clear(fixtures); target.Damage(1000);

            player.Respawn();
            yield return new WaitForSeconds(2.3f);
            game.Ground.Carve(new Vector2(19,-3),0.8f);
            game.Ground.Carve(new Vector2(22,-3),1.2f);
            Warp(player,new Vector3(22,-3,0),Vector3.zero);
            var constraints=player.Body.constraints;
            player.Body.constraints=RigidbodyConstraints.FreezeAll;
            var health=player.Health;
            game.Explode(new Vector2(19,-3),false);
            report.coveredDamage=health-player.Health;
            report.coverExposure=game.LastBlastExposure;
            check(report.coveredDamage==0 && report.coverExposure<0.05f && game.LastDamageBlocked>0,
                "Intact soil shields the body before the same explosion excavates its crater.");
            check(game.CoverEvents>0 && game.CoverFeedbackUntil>Time.time,"A shielded explosion produces visible cover feedback.");
            yield return new WaitForSeconds(0.1f);
            health=player.Health;
            game.Explode(new Vector2(19,-3),false);
            report.exposedDamage=health-player.Health;
            report.breachedExposure=game.LastBlastExposure;
            check(report.exposedDamage>report.coveredDamage+10 && report.breachedExposure>report.coverExposure+0.4f,
                "A second explosion damages the player after the first blast breaches the cover.");
            player.Body.constraints=constraints;
            Warp(player,new Vector3(-6,game.Ground.SurfaceAt(-6)+0.9f,0),Vector3.zero);

            var explosions=game.Explosions;
            for(var i=0;i<80;i++)
            {
                game.Explode(new Vector2(-28+(i%14)*4,-2-(i/14%3)*1.4f),true);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            report.stressExplosions=game.Explosions-explosions;
            var colliders=game.Ground.GetComponentsInChildren<BoxCollider>();
            foreach(var collider in colliders) if(collider.enabled) report.terrainCollidersAfterStress++;
            check(report.stressExplosions>=80 && game.Ground.RemovedCount>80,"Repeated live explosions rebuild dense craters without a runtime exception.");
            var filters=game.Ground.GetComponentsInChildren<MeshFilter>();
            var meshesPresent=filters.Length==16;
            foreach(var filter in filters) meshesPresent &= filter.sharedMesh!=null;
            check(meshesPresent && report.terrainCollidersAfterStress<512,"After repeated excavation, chunk meshes remain valid and obsolete colliders are removed.");
            report.terrainCellsRemoved=game.Ground.RemovedCount;
            player.Respawn();
            yield return new WaitForSeconds(0.3f);
        }

        private static IEnumerator Pulse(AirstrikeGame game,CombatInput input)
        {
            game.TestControls=input;
            yield return null;
            game.TestControls=new CombatInput { aim=input.aim };
            yield return new WaitForFixedUpdate();
        }

        private static void Warp(SoldierController player,Vector3 position,Vector3 velocity)
        {
            player.Body.position=position; player.transform.position=position;
            player.Body.linearVelocity=velocity; Physics.SyncTransforms();
        }

        private static void Box(Transform root,Vector3 position,Vector3 size)
        {
            var go=new GameObject("Fixture collider"); go.transform.SetParent(root,false);
            go.layer=GameArt.TerrainLayer; go.transform.position=position;
            go.AddComponent<BoxCollider>().size=size;
            Physics.SyncTransforms();
        }

        private static void Clear(GameObject root)
        {
            foreach(var collider in root.GetComponentsInChildren<Collider>()) collider.enabled=false;
            UnityEngine.Object.Destroy(root);
            Physics.SyncTransforms();
        }
    }
}
