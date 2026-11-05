using System;
using System.Collections;
using UnityEngine;

namespace Airstrike
{
    // Repeatable terrain budgets, separate from the accelerated wave scheduler.
    // These are fixed impact fixtures, not a prediction of a human playthrough.
    public static class TerrainBalanceChecks
    {
        public static IEnumerator Run(AirstrikeGame game, GameplayAutomation.Report report, Action<bool, string> check)
        {
            var legacy = Create(game, "Legacy crater fixture");
            var shallow = Create(game, "Shallow crater fixture");
            try
            {
                var top = shallow.SurfaceAt(0);
                report.legacyBombCells = legacy.Carve(new Vector2(0, top + 0.16f), 2.1f);
                report.shallowBombCells = shallow.CarveBlast(new Vector2(0, top + 0.16f), false);
                report.legacyBombDepth = top - legacy.SurfaceAt(0);
                report.shallowBombDepth = top - shallow.SurfaceAt(0);
                check(report.shallowBombCells > 0 && report.shallowBombCells <= report.legacyBombCells * 0.4f,
                    "A surface bomb excavates at most forty percent of the previous circular crater's cells.");
                check(report.shallowBombDepth > 0 && report.shallowBombDepth <= 0.75f,
                    "One surface bomb makes a shallow, visible crater instead of removing several layers.");
                var rocketTop = shallow.SurfaceAt(12);
                var rocketCells = shallow.CarveBlast(new Vector2(12, rocketTop + 0.1f), true);
                check(rocketCells > 0 && rocketCells <= 8 && rocketTop - shallow.SurfaceAt(12) <= 1,
                    "Player rockets also excavate a compact crater and preserve deeper terrain.");
            }
            finally { Destroy(legacy); Destroy(shallow); }
            yield return null;

            legacy = Create(game, "Legacy bombardment fixture");
            shallow = Create(game, "Shallow bombardment fixture");
            try
            {
                var initial = shallow.SolidCount;
                for (var wave = 0; wave < 6; wave++)
                {
                    // Eighteen ground impacts per group, spread across 24 lanes.
                    // Both algorithms receive the same lanes at their own surface.
                    for (var i = 0; i < 18; i++)
                    {
                        var x = (((wave * 18 + i) * 7) % 24 - 11.5f) * 2;
                        legacy.Carve(new Vector2(x, legacy.SurfaceAt(x) + 0.16f), 2.1f);
                        shallow.CarveBlast(new Vector2(x, shallow.SurfaceAt(x) + 0.16f), false);
                    }
                    report.legacyTerrainRetention.Add(legacy.SolidCount / (float)initial);
                    report.shallowTerrainRetention.Add(shallow.SolidCount / (float)initial);
                    yield return null;
                }
                var retained = report.shallowTerrainRetention[5];
                check(retained >= 0.75f && retained > report.legacyTerrainRetention[5] + 0.35f,
                    "Six groups of eighteen distributed impacts leave at least seventy-five percent of the soil.");
                var digX = 5f;
                var digCells = shallow.Carve(new Vector2(digX, shallow.SurfaceAt(digX) - 0.3f), 0.72f);
                check(digCells > 0, "Manual digging can still reshape bombarded terrain after 108 ground impacts.");
            }
            finally { Destroy(legacy); Destroy(shallow); }
            yield return null;

            legacy = Create(game, "Legacy focused bombardment fixture");
            shallow = Create(game, "Shallow focused bombardment fixture");
            try
            {
                var top = shallow.SurfaceAt(0);
                var initialWindow=shallow.InspectWindow(0);
                var initialSoil=shallow.SolidCount;
                for (var i = 0; i < 6; i++)
                {
                    legacy.Carve(new Vector2(0, legacy.SurfaceAt(0) + 0.16f), 2.1f);
                    shallow.CarveBlast(new Vector2(0, shallow.SurfaceAt(0) + 0.16f), false);
                }
                report.legacyRepeatedDepth = top - legacy.SurfaceAt(0);
                report.shallowRepeatedDepth = top - shallow.SurfaceAt(0);
                check(report.shallowRepeatedDepth > 1 && report.shallowRepeatedDepth <= 3.1f
                    && report.shallowRepeatedDepth < report.legacyRepeatedDepth * 0.5f,
                    "Six hits at one site deepen the crater gradually while leaving soil above bedrock.");
                check(shallow.SolidCount>initialSoil*0.95f && shallow.InspectWindow(0).centerColumnSoil<initialWindow.centerColumnSoil*0.7f,
                    "A concentrated crater consumes local digging depth even while more than ninety-five percent of global terrain remains.");
                var before = shallow.SolidCount;
                shallow.CarveBlast(new Vector2(0, shallow.SurfaceAt(0) + 0.16f), false);
                check(shallow.SolidCount < before,
                    "Further hits keep excavating the crater rather than reaching an artificial damage cap.");
            }
            finally { Destroy(legacy); Destroy(shallow); }
            yield return null;

            // Exercise the actual bomb -> collision -> explosion -> terrain path.
            var liveTop = game.Ground.SurfaceAt(-26);
            var removed = game.Ground.RemovedCount;
            var explosions = game.Explosions;
            game.DropBomb(new Vector2(-26, liveTop + 3), Vector2.zero);
            yield return new WaitForSeconds(1.2f);
            report.liveBombCells = game.Ground.RemovedCount - removed;
            check(game.Explosions > explosions && report.liveBombCells > 0 && report.liveBombCells <= 8,
                "A real falling bomb reaches the shallow-crater excavation path on ground impact.");
        }

        private static DestructibleGround Create(AirstrikeGame game, string name)
        {
            var root = new GameObject(name);
            root.SetActive(false); // Grid fixtures must never collide with the live world.
            var ground = root.AddComponent<DestructibleGround>();
            ground.Initialize(game.Rules,game.Palette[0],game.Palette[1],game.Palette[2],game.Palette[15],game.Palette[16],game.Palette[19],game.Palette[7]);
            return ground;
        }

        private static void Destroy(DestructibleGround ground)
        {
            if (ground != null) UnityEngine.Object.Destroy(ground.gameObject);
        }
    }
}
