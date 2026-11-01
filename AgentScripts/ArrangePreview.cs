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
        foreach(var old in game.Aircraft.ToArray())
        {
            if(old==null) continue;
            old.GetComponent<Collider>().enabled=false;
            game.Aircraft.Remove(old);
            Object.Destroy(old.gameObject);
        }
        foreach(var projectile in Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)) projectile.Cancel();
        foreach(var old in game.Supplies.ToArray()) old.Expire();
        foreach(var old in game.Pilots.ToArray()) old.Expire();
        var roles=new[] {AircraftRole.Bomber,AircraftRole.Helicopter,AircraftRole.FastBomber};
        for(var i=0;i<roles.Length;i++)
        {
            var aircraft=game.SpawnAircraft(new Vector2(-11+i*11,9+i*1.1f),1,roles[i]);
            aircraft.Speed=0; aircraft.AttacksEnabled=false;
        }
        var transport=game.SpawnTransport(SupplyKind.JumpBoots,new Vector2(-15,6.8f),1);
        transport.Speed=0;
        game.SpawnSupply(SupplyKind.Emp,new Vector2(-1,3.4f));
        game.SpawnSupply(SupplyKind.JumpBoots,new Vector2(4,2.7f));
        game.SpawnSupply(SupplyKind.RocketSlot,new Vector2(9,1.9f));
        game.SpawnPilot(new Vector2(-9.5f,3.3f));
        game.Ground.CarveBlast(new Vector2(3,game.Ground.SurfaceAt(3)+0.16f),false);
        game.Ground.CarveBlast(new Vector2(-14,game.Ground.SurfaceAt(-14)+0.16f),false);
        Physics.SyncTransforms();
        game.Explode((Vector2)game.Player.Body.position+new Vector2(-2.4f,-1.3f),false);
        return new {staged=true,aircraft=game.Aircraft.Count,removedCells=game.Ground.RemovedCount,coverAbsorption=game.LastCoverAbsorption};
    }
}
