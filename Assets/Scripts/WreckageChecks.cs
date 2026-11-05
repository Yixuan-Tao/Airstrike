using System;
using System.Collections;
using UnityEngine;

namespace Airstrike
{
    public static class WreckageChecks
    {
        public static IEnumerator Run(AirstrikeGame game,Action<bool,string> check)
        {
            const float x=24.25f;
            var ground=game.Ground;
            var top=ground.SurfaceAt(x);
            var soil=ground.InspectWindow(x).remainingCells;
            var kills=game.Kills;
            var active=game.HostileAircraftCount;
            var target=game.SpawnAircraft(new Vector2(x,top+5),1);
            target.Speed=0;
            target.AttacksEnabled=false;
            Physics.SyncTransforms();
            game.CrashChance=1;
            game.LaunchRocket(new Vector2(x-5,top+5),Vector2.right);
            var deadline=Time.time+1;
            while(game.FallingWrecks==0 && Time.time<deadline) yield return null;
            game.CrashChance=0;
            check(game.FallingWrecks==1 && game.Kills==kills+1 && game.HostileAircraftCount==active
                && target!=null && !target.GetComponent<Collider>().enabled,
                "A real rocket triggers a falling wreck, scores once, and removes the aircraft from the active wave.");
            var altitude=target.transform.position.y;
            yield return new WaitForSeconds(0.25f);
            check(target!=null && target.transform.position.y<altitude && ground.WreckCells==0,
                "The destroyed aircraft falls before it becomes solid terrain.");
            deadline=Time.time+2;
            while(game.FallingWrecks>0 && Time.time<deadline) yield return null;
            check(game.WrecksLanded==1 && game.FallingWrecks==0 && ground.WreckCells==4 && game.Kills==kills+1,
                "Landing leaves four destructible metal cells without scoring again.");
            var center=new Vector2(x,top+0.25f);
            var mask=1<<GameArt.TerrainLayer;
            check(ground.SurfaceAt(x)==top+0.5f && ground.InspectWindow(x).remainingCells==soil
                && Physics.Linecast(center+Vector2.left*2,center+Vector2.right*2,mask)
                && ground.BlastTransmission(center+Vector2.left*2,center+Vector2.right*2)<0.01f,
                "The wreck forms a solid step, blocks a blast ray, and remains separate from soil retention.");

            var player=game.Player;
            var startX=x-1.9f;
            var start=new Vector3(startX,ground.SurfaceAt(startX)+0.9f,0);
            player.Body.position=start;
            player.transform.position=start;
            player.Body.linearVelocity=Vector3.zero;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(0.2f);
            deadline=Time.time+1.2f;
            var steps=player.StepUps;
            game.TestControls=new CombatInput {move=1};
            while(player.Body.position.x<x-0.4f && Time.time<deadline) yield return null;
            game.TestControls=new CombatInput();
            deadline=Time.time+0.8f;
            while((!player.Grounded || Mathf.Abs(player.Body.linearVelocity.x)>0.1f) && Time.time<deadline) yield return null;
            check(player.StepUps>steps && player.Grounded && player.Body.position.y>top+1.15f && Mathf.Abs(player.Body.position.x-x)<0.85f,
                "The soldier steps onto the low wreck without jumping.");

            var digOps=game.DigOperations;
            game.TestControls=new CombatInput {dig=true,aim=center};
            yield return new WaitForSeconds(0.2f);
            game.TestControls=new CombatInput();
            check(game.DigOperations>digOps && ground.WreckCells<4,"The normal digging input removes metal cells.");
            ground.CarveBlast(center+Vector2.right*0.65f,false);
            ground.CarveBlast(center+Vector2.left*0.65f,false);
            check(ground.WreckCells==0 && !Physics.Linecast(center+Vector2.left*2,center+Vector2.right*2,mask),
                "Blast excavation clears the rest of the wreck and its terrain collision.");

            var protectedSite=new Vector2(AirstrikeGame.RespawnX,ground.SurfaceAt(AirstrikeGame.RespawnX));
            var occupiedSite=new Vector2(12.25f,ground.SurfaceAt(12.25f));
            check(!ground.TryPlaceWreck(protectedSite,center) && !ground.TryPlaceWreck(occupiedSite,occupiedSite+Vector2.up*0.85f),
                "Metal cannot be placed in the respawn corridor or around the player's body.");
            foreach(var site in new[] {-28.25f,-24.75f,-20.75f,-16.75f,4.25f,8.25f,12.25f,16.25f,20.25f})
                ground.TryPlaceWreck(new Vector2(site,ground.SurfaceAt(site)),center);
            check(ground.WreckCells==DestructibleGround.MaximumWreckCells
                && !ground.TryPlaceWreck(new Vector2(27.25f,ground.SurfaceAt(27.25f)),center),
                "Accumulated wreckage stops at twenty-four cells even with space for more.");
            player.Respawn();
        }
    }
}
