using Airstrike;
using UnityEngine;

// Staged runtime scene for the preview image; not an unattended-play benchmark.
public static class ArrangePreview
{
    public static object Arrange()
    {
        var game=Object.FindFirstObjectByType<AirstrikeGame>();
        if(game==null) throw new System.InvalidOperationException("Open Assets/Scenes/Prototype.unity and enter Play Mode first.");
        game.OverrideControls=true;
        game.TestControls=new CombatInput { aim=new Vector2(3,10) };
        foreach(var old in game.Aircraft.ToArray()) if(old!=null) old.Damage(1000);
        var roles=new[] {AircraftRole.Bomber,AircraftRole.Helicopter,AircraftRole.FastBomber};
        for(var i=0;i<roles.Length;i++)
        {
            var aircraft=game.SpawnAircraft(new Vector2(-11+i*11,9+i*1.1f),1,roles[i]);
            aircraft.Speed=0; aircraft.AttacksEnabled=false;
        }
        game.SpawnSupply(SupplyKind.RocketSlot,game.Player.Body.position);
        game.SpawnSupply(SupplyKind.Piercing,game.Player.Body.position);
        game.SpawnSupply(SupplyKind.RocketSlot,new Vector2(3,4.5f));
        game.Ground.Carve(new Vector2(3,-0.2f),2);
        game.Ground.Carve(new Vector2(-14,-0.3f),1.7f);
        Physics.SyncTransforms();
        game.Explode((Vector2)game.Player.Body.position+new Vector2(-2.4f,-1.3f),false);
        return new {staged=true,aircraft=game.Aircraft.Count,removedCells=game.Ground.RemovedCount,coverAbsorption=game.LastCoverAbsorption};
    }
}
