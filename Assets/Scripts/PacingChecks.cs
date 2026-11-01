using System;
using System.Collections;
using UnityEngine;

namespace Airstrike
{
    public static class PacingChecks
    {
        public static IEnumerator Run(AirstrikeGame game,GameplayAutomation.Report report,Action<bool,string> check,Action<EnemyAircraft,bool> live)
        {
            check(WavePlan.Get(1).Spawns.Count==1 && WavePlan.Get(2).Spawns.Count==2 && WavePlan.Get(3).Spawns.Count==3,
                "The first three waves introduce combat with one, two and three aircraft.");
            var bomber=game.SpawnAircraft(new Vector2(18,10),1);
            bomber.Speed=0; live(bomber,true);
            var started=Time.time; var count=0; var first=0f; var second=0f;
            var deadline=Time.time+13;
            while(count<2 && Time.time<deadline)
            {
                if(bomber.NormalBombsReleased>count)
                {
                    count=bomber.NormalBombsReleased;
                    if(count==1) first=bomber.LastNormalBombAt;
                    if(count==2) second=bomber.LastNormalBombAt;
                }
                yield return null;
            }
            report.normalFirstBombDelay=first-started; report.normalBombInterval=second-first;
            check(count==2 && report.normalFirstBombDelay>=3.98f && report.normalFirstBombDelay<=6.2f,
                "A normal bomber waits four to six seconds before its first attack.");
            check(report.normalBombInterval>=3.98f && report.normalBombInterval<=6.2f,
                "Successive normal-bomber attacks follow the slower randomized four-to-six-second cadence.");
            check(Mathf.Abs(bomber.LastNormalBombPosition.x-game.Player.Body.position.x)>8.5f,
                "Normal bombing can occur away from the player instead of requiring a nearby target.");
            live(bomber,false); bomber.Damage(1000,false);
            foreach(var projectile in UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))
                if(!projectile.IsPlayerRocket) projectile.Cancel();
        }
    }
}
