// cc-P610 (P533 U7, P425 #1, P504 #2): a saved Rootdeep entrance is safe when the server's map overlay is missing, and a
// Young player's real entry warns and changes protection. Notes: shard-migration notes/cc-P610-fix-drafts-from-p533.md.
//
// Facts:
//   1. Seeded and saved with the map built, then started without it: the saved way down is shut (a step on the cellar
//      hatch stays in the Second Bank of Haven; cc-P678, was the burrow mouth in Old Haven), the ladder up still works,
//      and starting again with the map restores it; a teleporter staff switched off themselves stays off, through a
//      start without the map and one with it. The world-up step is the server's, not the seeder's (P425 #1).
//   2. A Young player with a connection who steps onto the cellar hatch gets the dungeon warning, and Young protection
//      holds in the bank and not on the Landing (P504 #2).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class RootdeepMapSafetyVerification
{
    private static readonly Point3D BesideHatch = new(3621, 2620, 0); // cc-P678: where the ladder lands

    public RootdeepMapSafetyVerification()
    {
        RootdeepVaultRegion.Initialize();
        ApprenticeTrainingRegion.Initialize();
    }

    private static PlayerMobile NewPlayer(Point3D at)
    {
        var pm = new PlayerMobile { Player = true, Race = Race.Human, Name = "Walker" };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(at, Map.Trammel);
        return pm;
    }

    private static Point3D Arrival => new(ClusterFRootdeepSeeder.Arrival.X, ClusterFRootdeepSeeder.Arrival.Y, 0);

    private static List<Teleporter> Ours() =>
        World.Items.Values.OfType<Teleporter>().Where(t => !t.Deleted && t.Name?.StartsWith("Rootdeep: ", StringComparison.Ordinal) == true).ToList();

    /// <summary>The server coming up after a load (reflection: also runs, and fails, against the code before it).</summary>
    private static void WorldUp() =>
        typeof(ClusterFRootdeepSeeder).GetMethod("CheckMapAtStartup", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);

    private static Teleporter Reload(Teleporter t)
    {
        var buffer = new byte[4096];
        var writer = new BufferWriter(buffer, true);
        t.Serialize(writer);
        writer.Flush();
        var (serial, at, map) = (t.Serial, t.Location, t.Map);
        t.Delete();
        var copy = new Teleporter(serial);
        World.AddEntity(copy);
        copy.Deserialize(new BufferReader(buffer));
        copy.MoveToWorld(at, map);
        return copy;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void ASavedWayDownIsShutWhileTheMapIsMissingAndOpensWhenItIsBack()
    {
        var mapBuilt = ClusterFRootdeepSeeder.MapBuilt;
        var walker = NewPlayer(BesideHatch);
        try
        {
            // Placed as the seeder places them (its own method), then saved and loaded.
            ClusterFRootdeepSeeder.MapBuilt = () => true;
            var hole = Assert.Single(ClusterFRootdeepSeeder.Teleporters, r => r.To == Arrival);
            Assert.Equal((3618, 2620), (hole.X, hole.Y));
            var down = Reload(ClusterFRootdeepSeeder.PlaceTeleporter(Map.Trammel, hole));
            var up = Reload(ClusterFRootdeepSeeder.PlaceTeleporter(Map.Trammel, ClusterFRootdeepSeeder.LadderUp));

            // The overlay is gone at the next start.
            ClusterFRootdeepSeeder.MapBuilt = () => false;
            WorldUp();
            down.OnMoveOver(walker);
            Assert.Equal(BesideHatch, walker.Location); // not sent into the unbuilt interior
            Assert.True(up.Active, "the ladder up still takes anyone inside back out");

            // The overlay is back.
            ClusterFRootdeepSeeder.MapBuilt = () => true;
            WorldUp();
            Assert.True(down.Active);
            down.OnMoveOver(walker);
            Assert.Equal(Arrival, walker.Location);

            // Staff switch it off themselves: a start without the map, then one with it, leave it off.
            down.Active = false;
            ClusterFRootdeepSeeder.MapBuilt = () => false;
            WorldUp();
            ClusterFRootdeepSeeder.MapBuilt = () => true;
            WorldUp();
            Assert.False(down.Active, "a teleporter staff switched off stays off");
        }
        finally
        {
            ClusterFRootdeepSeeder.MapBuilt = mapBuilt;
            walker.Delete();
            foreach (var t in Ours())
            {
                t.Delete();
            }
        }
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void AYoungPlayersRealEntryWarnsAndLeavesYoungProtectionBehind()
    {
        var mapBuilt = ClusterFRootdeepSeeder.MapBuilt;
        var pm = NewPlayer(BesideHatch);
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        pm.Young = true;
        Teleporter t = null;

        try
        {
            ClusterFRootdeepSeeder.MapBuilt = () => true;
            var hole = Assert.Single(ClusterFRootdeepSeeder.Teleporters, r => r.To == Arrival);
            t = ClusterFRootdeepSeeder.PlaceTeleporter(Map.Trammel, hole);

            var approach = Region.Find(BesideHatch, Map.Trammel).GetRegion<BaseRegion>();
            Assert.NotNull(approach);
            Assert.True(approach.YoungProtected, "the bank protects the Young");
            Assert.Null(pm.FindGump<YoungDungeonWarningGump>());

            t.OnMoveOver(pm); // the step onto the hatch
            Assert.Equal(Arrival, pm.Location);
            var landing = pm.Region.GetRegion<BaseRegion>();
            Assert.IsType<OldHavenBurrowsRegion>(landing);
            Assert.False(landing.YoungProtected, "the Landing does not");
            Assert.NotNull(pm.FindGump<YoungDungeonWarningGump>());
        }
        finally
        {
            ClusterFRootdeepSeeder.MapBuilt = mapBuilt;
            t?.Delete();
            pm.NetState = null;
            ns.Mobile = null;
            ns.Dispose();
            pm.Delete();
        }
    }
}
