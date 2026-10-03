// Probes for two defects in server/customizations/ShrunkPet.cs, found by reading during cc-P41
// (shard-migration notes/cc-P41-companions-research.md section 5 item 3) and proved here.
//
// 1. RESTORE LEFT THE PET WITH NO ONE TO FOLLOW. Restore called SetControlMaster, which clears
//    ControlTarget (pinned BaseCreature.cs:3782), then assigned ControlOrder = Follow raw. Pinned
//    DoOrderFollow with no ControlTarget drops the order to None on the next AI tick
//    (UOContent/Mobiles/AI/BaseAI/PetOrders.cs:638-644), so the pet appeared and stood still.
//
// 2. FOLLOWER COUNT DRIFTED ACROSS A RESTART. TryShrink took the pet's slots off the owner by hand
//    and kept Master = owner. Mobile.Followers is not saved (pinned Mobile.cs:6331-6334 only skips
//    an old field) and is rebuilt at load by every creature's own Deserialize calling AddFollowers
//    (BaseCreature.cs:2514, 2639-2647), so after a restart the stored pet counted again, and
//    restore then added its slots a second time.
//
// 3. A SHRUNK PET LEFT ITS FIGURINE ON LOGOUT AND LOGIN, and at every restart. It stayed in the
//    owner's AllFollowers, so pinned's SE auto-stable (logout, and CheckPets at startup) stabled it
//    and ClaimAutoStabledPets put it back at the owner's feet on login, orphaning the figurine.
//
// The restart in test 2 is a real serialize/deserialize of the pet, which is what runs
// BaseCreature.cs:2514. The owner's own reload is represented by zeroing Followers and emptying
// AllFollowers, neither of which is saved. The shard's after-load hook is then invoked the
// way AssemblyHandler.Invoke("Initialize") invokes it (pinned Main.cs:672-674: World.Load, then
// Initialize), by reflection, so this file compiles against the shard with or without the hook.

using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ShrunkPetVerification
{
    private static readonly Point3D Spot = new(1400, 1740, 0);

    private readonly ITestOutputHelper _out;

    public ShrunkPetVerification(ITestOutputHelper output) => _out = output;

    private static (PlayerMobile Pm, GreyWolf Pet) Owner()
    {
        var pm = new PlayerMobile { Player = true, FollowersMax = 5 };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Spot, Map.Trammel);

        var pet = new GreyWolf();
        pet.MoveToWorld(Spot, Map.Trammel);
        Assert.True(pet.SetControlMaster(pm));
        pet.IsBonded = true;
        pet.IssueOrder(OrderType.Follow, pm, pm); // what "all follow me" does

        return (pm, pet);
    }

    private static ShrunkPet Shrink(PlayerMobile pm, BaseCreature pet)
    {
        var fig = ShrunkPet.TryShrink(pm, pet);
        Assert.NotNull(fig);
        pm.Backpack!.DropItem(fig);
        return fig!;
    }

    [Fact]
    public void ARestoredPetKeepsFollowingAfterAnAiTick()
    {
        var (pm, pet) = Owner();
        var fig = Shrink(pm, pet);

        fig.OnDoubleClick(pm);

        _out.WriteLine($"after restore: order={pet.ControlOrder} target={pet.ControlTarget?.Name ?? "null"} " +
                       $"map={pet.Map}");
        var targetAfterRestore = pet.ControlTarget;

        // One tick of the Follow order. Distance 0, so FollowTarget does not try to walk.
        pet.AIObject.DoOrderFollow();
        _out.WriteLine($"after one DoOrderFollow: order={pet.ControlOrder}");

        Assert.Equal(Map.Trammel, pet.Map);
        Assert.Equal(OrderType.Follow, pet.ControlOrder);
        Assert.Same(pm, targetAfterRestore);

        pet.Delete();
        pm.Delete();
    }

    [Fact]
    public void FollowerCountSurvivesARestartWhileShrunk()
    {
        var (pm, pet) = Owner();
        var slots = pet.ControlSlots;
        Assert.Equal(slots, pm.Followers);

        var fig = Shrink(pm, pet);
        _out.WriteLine($"shrunk, same session: followers={pm.Followers}");
        Assert.Equal(0, pm.Followers);

        // --- restart ---
        // Neither Mobile.Followers nor PlayerMobile.AllFollowers is saved, so a loaded owner starts
        // with 0 and an empty set. Deserializing the pet runs BaseCreature.cs:2514 AddFollowers, which
        // refills both. It is read back into the same object so the figurine's reference still holds.
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        pet.Serialize(writer);
        writer.Flush();

        pm.Followers = 0;
        pm.RemoveFollower(pet);
        pet.Deserialize(new BufferReader(buffer));
        _out.WriteLine($"after the pet's load: followers={pm.Followers}");

        typeof(ShrunkPet).GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, null);
        var afterLoad = pm.Followers;
        _out.WriteLine($"after Initialize: followers={afterLoad}");

        // Startup's CheckPets (PlayerMobile.cs:950, :1064-1083) runs after every Initialize, then login.
        if ((pm.AllFollowers?.Count ?? 0) > (pm.AutoStabled?.Count ?? 0))
        {
            pm.AutoStablePets();
        }

        pm.ClaimAutoStabledPets();
        _out.WriteLine($"after startup and login: map={pet.Map} stabled={pet.IsStabled} followers={pm.Followers}");
        var mapAfterLogin = pet.Map;

        // --- restore ---
        fig.OnDoubleClick(pm);
        _out.WriteLine($"after restore: followers={pm.Followers} (slots={slots})");

        Assert.Equal(0, afterLoad);
        Assert.Equal(Map.Internal, mapAfterLogin);
        Assert.Equal(Map.Trammel, pet.Map);
        Assert.Equal(slots, pm.Followers);

        pet.Delete();
        pm.Delete();
    }

    [Fact]
    public void AFigurineRestoredByAnotherPlayerCountsOnceForThemAndZeroForTheShrinker()
    {
        // The figurine is a movable item, so it can change hands. Restore then calls
        // SetControlMaster with a new master, whose Master setter (BaseCreature.cs:1163-1170) takes
        // the slots off the old master (who no longer had them) and adds them to the new one.
        var (pm, pet) = Owner();
        var fig = Shrink(pm, pet);

        var other = new PlayerMobile { Player = true, FollowersMax = 5 };
        other.AddItem(new Backpack());
        other.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        other.Backpack!.DropItem(fig);

        fig.OnDoubleClick(other);

        _out.WriteLine($"restored by another player: shrinker={pm.Followers} holder={other.Followers} " +
                       $"(slots={pet.ControlSlots})");

        Assert.Same(other, pet.ControlMaster);
        Assert.Equal(0, pm.Followers);
        Assert.Equal(pet.ControlSlots, other.Followers);

        pet.Delete();
        pm.Delete();
        other.Delete();
    }

    [Fact]
    public void AShrunkPetStaysInItsFigurineAcrossALogoutAndLogin()
    {
        // Pinned auto-stables every controlled follower in AllFollowers on logout
        // (PlayerMobile.cs:1522 -> AutoStablePets, :3508) and at every server start (CheckPets, :950,
        // :1064-1083), then brings auto-stabled pets back to the player's feet on login
        // (ClaimAutoStabledPets, :1276, :3570). A shrunk pet that is still in AllFollowers takes part.
        Assert.True(Core.SE, "AutoStablePets is SE-only; without SE this test proves nothing");

        var (pm, pet) = Owner();
        var fig = Shrink(pm, pet);

        pm.AutoStablePets();       // what logout runs
        _out.WriteLine($"after logout: map={pet.Map} stabled={pet.IsStabled} master={pet.ControlMaster?.Name ?? "null"} " +
                       $"followers={pm.Followers}");
        var stabledOnLogout = pet.IsStabled;

        pm.ClaimAutoStabledPets(); // what login runs
        _out.WriteLine($"after login: map={pet.Map} stabled={pet.IsStabled} followers={pm.Followers}");

        Assert.False(stabledOnLogout);
        Assert.Equal(Map.Internal, pet.Map);
        Assert.Equal(0, pm.Followers);

        fig.OnDoubleClick(pm);
        _out.WriteLine($"after restore: map={pet.Map} followers={pm.Followers} order={pet.ControlOrder}");

        Assert.Equal(Map.Trammel, pet.Map);
        Assert.Equal(pet.ControlSlots, pm.Followers);
        Assert.Contains(pet, pm.AllFollowers!);
        Assert.True(fig.Deleted);

        pet.Delete();
        pm.Delete();
    }

    [Fact]
    public void FollowerCountIsUnchangedByAShrinkAndRestoreInOneSession()
    {
        // Control: the in-session path was already right and must stay right.
        var (pm, pet) = Owner();
        var fig = Shrink(pm, pet);
        fig.OnDoubleClick(pm);

        _out.WriteLine($"same-session round trip: followers={pm.Followers}");
        Assert.Equal(pet.ControlSlots, pm.Followers);
        Assert.Same(pm, pet.ControlMaster);
        Assert.True(pet.IsBonded);

        pet.Delete();
        pm.Delete();
    }
}
