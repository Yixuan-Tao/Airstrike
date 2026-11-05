using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace Airstrike
{
    public static class PacingChecks
    {
        public static IEnumerator Run(AirstrikeGame game,GameplayAutomation.Report report,Action<bool,string> check,Action<EnemyAircraft,bool> live)
        {
            var rules=game.Rules;
            check(WavePlan.Get(1,rules).Spawns.Count==1 && WavePlan.Get(2,rules).Spawns.Count==2 && WavePlan.Get(3,rules).Spawns.Count==3,
                "The first three waves introduce combat with one, two and three aircraft.");
            check(WavePlan.Budget(6,rules)==7 && WavePlan.Budget(10,rules)==13 && WavePlan.Budget(20,rules)==28 && WavePlan.Budget(30,rules)==45
                && Enumerable.Range(1,40).All(wave=>WavePlan.Get(wave,rules).Spawns.Sum(spawn=>spawn.Cost)==WavePlan.Budget(wave,rules)),
                "The first forty wave plans spend the original wave budget on aircraft and upgrades.");
            check(Enumerable.Range(1,32).All(wave=>WavePlan.Cargo(wave,rules).HasValue==(wave%4==0)),
                "Cargo opportunities occur every fourth wave.");
            var bomber=game.SpawnAircraft(new Vector2(34,10),1);
            bomber.Speed=0; live(bomber,true);
            var stored=bomber.BombTimeRemaining;
            yield return new WaitForSeconds(0.4f);
            check(bomber.NormalBombsReleased==0 && bomber.BombTimeRemaining==stored,"A normal bomber's countdown stops outside the battle lane.");
            bomber.transform.position=new Vector2(18,10);
            var started=Time.time; var count=0; var first=0f; var second=0f;
            var deadline=Time.time+26;
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
            check(count==2 && Mathf.Abs(report.normalFirstBombDelay-stored)<0.12f,
                "The stored randomized delay resumes on entering the battle lane.");
            check(report.normalBombInterval>=0.02f && report.normalBombInterval<=12.9f,
                "Successive attacks follow the original randomized countdown range.");
            check(Mathf.Abs(bomber.LastNormalBombPosition.x-game.Player.Body.position.x)>8.5f,
                "Normal bombing can occur away from the player instead of requiring a nearby target.");
            live(bomber,false); bomber.Damage(1000,false);
            foreach(var projectile in UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))
                if(!projectile.IsPlayerRocket) projectile.Cancel();
        }
    }
}
