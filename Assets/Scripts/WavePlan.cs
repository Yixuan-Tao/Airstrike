using System;
using System.Collections.Generic;

namespace Airstrike
{
    public enum AircraftRole { Bomber, FastBomber, Helicopter }
    public enum SupplyKind { RocketSlot, Piercing }

    public readonly struct AircraftSpawn
    {
        public readonly AircraftRole Role;
        public readonly bool Armored;
        public AircraftSpawn(AircraftRole role,bool armored=false) { Role=role; Armored=armored; }
    }

    public sealed class WaveDefinition
    {
        public string Name { get; }
        public float SpawnSeconds { get; }
        public IReadOnlyList<AircraftSpawn> Spawns { get; }
        public WaveDefinition(string name,float interval,params AircraftSpawn[] spawns)
        { Name=name; SpawnSeconds=interval; Spawns=Array.AsReadOnly(spawns); }
    }

    public static class WavePlan
    {
        private static readonly AircraftSpawn B=new AircraftSpawn(AircraftRole.Bomber);
        private static readonly AircraftSpawn A=new AircraftSpawn(AircraftRole.Bomber,true);
        private static readonly AircraftSpawn F=new AircraftSpawn(AircraftRole.FastBomber);
        private static readonly AircraftSpawn H=new AircraftSpawn(AircraftRole.Helicopter);
        private static readonly WaveDefinition[] Opening = {
            new WaveDefinition("FIRST CONTACT",2.7f,B,B,B),
            new WaveDefinition("FAST MOVERS",2.5f,B,F,B,F),
            new WaveDefinition("ROCKET LOCK",2.5f,H,B,F,B),
            new WaveDefinition("HARD TARGETS",2.2f,A,F,B,H,F),
            new WaveDefinition("CROSS FIRE",2.0f,F,H,F,B,H,F),
            new WaveDefinition("HOLD THE SECTOR",1.8f,H,F,A,H,F,F,B)
        };

        public static WaveDefinition Get(int wave)
        {
            if(wave<1) throw new ArgumentOutOfRangeException(nameof(wave));
            if(wave<=Opening.Length) return Opening[wave-1];
            var count=Math.Min(8+(wave-7)/2,12);
            var spawns=new AircraftSpawn[count];
            for(var i=0;i<count;i++) spawns[i]=i%4==0?H:i%4==1?F:wave>=8 && i%3==0?A:B;
            return new WaveDefinition("ENDLESS SKY",Math.Max(1.15f,1.8f-(wave-6)*0.06f),spawns);
        }

        public static SupplyKind? Reward(int completedWave)
        {
            if(completedWave<2 || completedWave%2!=0) return null;
            return completedWave%4==0?SupplyKind.Piercing:SupplyKind.RocketSlot;
        }
    }
}
