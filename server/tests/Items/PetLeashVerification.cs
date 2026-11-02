// cc-P764: the pet leash (server/customizations/ClusterFPetLeash.cs), a port of Queen's "Pet shrink for 57.2".
// Shrink and restore keep the pet's name, owner, loyalty and bond; only the owner can call it out; it is refused in a
// fight, on a summon and on a pet carrying items; the P762 pet choice scroll gives a leash; the animal trainer sells it;
// and the follower count is right after a restart (ShrunkPet.Initialize, which LeashedPet relies on).

using System.Linq;
using System.Reflection;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Tutorial;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PetLeashVerification
{
    private static readonly Point3D Spot = new(1410, 1740, 0);

    private readonly ITestOutputHelper _out;

    public PetLeashVerification(ITestOutputHelper output) => _out = output;

    private static PlayerMobile Player(string name)
    {
        var pm = new PlayerMobile { Player = true, Name = name, FollowersMax = 5 };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Spot, Map.Trammel);
        return pm;
    }

    private static Dog DogOf(PlayerMobile pm, string name = "Rex")
    {
        var dog = new Dog { Name = name };
        dog.MoveToWorld(Spot, Map.Trammel);
        Assert.True(dog.SetControlMaster(pm));
        dog.IssueOrder(OrderType.Follow, pm, pm);
        return dog;
    }

    private static PetLeash LeashIn(PlayerMobile pm)
    {
        var leash = new PetLeash();
        pm.Backpack!.DropItem(leash);
        return leash;
    }

    // Setting Combatant also records aggression, which counts as fighting for clusterf.petLeash.combatSeconds: the fight
    // is over when both are cleared.
    private static void Calm(params Mobile[] mobiles)
    {
        foreach (var m in mobiles)
        {
            m.Combatant = null;
            m.Aggressed.Clear();
            m.Aggressors.Clear();
        }
    }

    private static void Cleanup(params IEntity?[] things)
    {
        foreach (var e in things)
        {
            e?.Delete();
        }
    }

    [Fact]
    public void P764_ShrinkAndRestoreKeepNameOwnerAndLoyalty()
    {
        var pm = Player("Leasher");
        var dog = DogOf(pm);
        dog.Loyalty = 70;
        var leash = LeashIn(pm);
        Assert.Equal(1, pm.Followers);

        var fig = leash.Use(pm, dog);

        Assert.NotNull(fig);
        _out.WriteLine($"shrunk: fig={fig!.Name} item=0x{fig.ItemID:X4} map={dog.Map} followers={pm.Followers}");
        Assert.True(fig.IsChildOf(pm.Backpack));
        Assert.Equal(1.0, fig.Weight);
        Assert.Equal(1.0, leash.Weight);
        Assert.Equal(ShrinkTable.Lookup(dog), fig.ItemID);
        Assert.Equal(Map.Internal, dog.Map);
        Assert.Equal(0, pm.Followers);
        Assert.False(leash.Deleted); // unlimited by default

        Assert.True(fig.Restore(pm));

        _out.WriteLine($"restored: name={dog.Name} master={dog.ControlMaster?.Name} loyalty={dog.Loyalty} map={dog.Map}");
        Assert.Equal("Rex", dog.Name);
        Assert.Same(pm, dog.ControlMaster);
        Assert.True(dog.Controlled);
        Assert.Equal(70, dog.Loyalty);
        Assert.False(dog.IsBonded);
        Assert.Equal(Map.Trammel, dog.Map);
        Assert.Equal(OrderType.Follow, dog.ControlOrder);
        Assert.Same(pm, dog.ControlTarget);
        Assert.Equal(1, pm.Followers);
        Assert.True(fig.Deleted);

        Cleanup(dog, pm);
    }

    [Fact]
    public void P764_ANonOwnerCannotRestore()
    {
        var owner = Player("Owner");
        var other = Player("Finder");
        var dog = DogOf(owner);
        var fig = LeashIn(owner).Use(owner, dog)!;
        Assert.NotNull(fig);

        other.Backpack!.DropItem(fig);
        Assert.False(fig.Restore(other));

        Assert.Equal(Map.Internal, dog.Map);
        Assert.Same(owner, dog.ControlMaster);
        Assert.Equal(0, other.Followers);
        Assert.False(fig.Deleted);

        // Back with its owner, it comes out.
        owner.Backpack!.DropItem(fig);
        Assert.True(fig.Restore(owner));
        Assert.Same(owner, dog.ControlMaster);

        Cleanup(dog, owner, other);
    }

    [Fact]
    public void P764_ShrinkAndRestoreAreRefusedInAFight()
    {
        var pm = Player("Fighter");
        var dog = DogOf(pm);
        var leash = LeashIn(pm);
        var rat = new SewerRat();
        rat.MoveToWorld(Spot, Map.Trammel);

        pm.Combatant = rat;
        Assert.Null(leash.Use(pm, dog));
        Assert.Equal(Map.Trammel, dog.Map);
        Calm(pm, dog, rat);

        dog.Combatant = rat;
        Assert.Null(leash.Use(pm, dog));
        Assert.Equal(Map.Trammel, dog.Map);
        Calm(pm, dog, rat);

        var fig = leash.Use(pm, dog)!;
        Assert.NotNull(fig);

        pm.Combatant = rat;
        Assert.False(fig.Restore(pm));
        Assert.Equal(Map.Internal, dog.Map);
        Calm(pm, rat);
        Assert.True(fig.Restore(pm));

        Cleanup(dog, rat, pm);
    }

    [Fact]
    public void P764_ASummonCannotBeLeashed()
    {
        var pm = Player("Summoner");
        var wolf = new GreyWolf { Summoned = true };
        wolf.MoveToWorld(Spot, Map.Trammel);
        Assert.True(wolf.SetControlMaster(pm));
        var leash = LeashIn(pm);

        Assert.Null(leash.Use(pm, wolf));
        Assert.Equal(Map.Trammel, wolf.Map);
        Assert.Empty(pm.Backpack!.Items.OfType<LeashedPet>());

        Cleanup(wolf, pm);
    }

    [Fact]
    public void P764_APetCarryingItemsCannotBeLeashed()
    {
        var pm = Player("Packer");
        var horse = new PackHorse();
        horse.MoveToWorld(Spot, Map.Trammel);
        Assert.True(horse.SetControlMaster(pm));
        horse.Backpack!.DropItem(new Gold(10));
        var leash = LeashIn(pm);

        Assert.Null(leash.Use(pm, horse));
        Assert.Equal(Map.Trammel, horse.Map);

        // Unloaded, it goes.
        foreach (var item in horse.Backpack.Items.ToList())
        {
            item.Delete();
        }

        var fig = leash.Use(pm, horse);
        Assert.NotNull(fig);
        Assert.True(fig!.Restore(pm));

        Cleanup(horse, pm);
    }

    [Fact]
    public void P764_SomeoneElsesPetCannotBeLeashed()
    {
        var owner = Player("RealOwner");
        var thief = Player("Thief");
        var dog = DogOf(owner);

        Assert.Null(LeashIn(thief).Use(thief, dog));
        Assert.Equal(Map.Trammel, dog.Map);
        Assert.Same(owner, dog.ControlMaster);

        Cleanup(dog, owner, thief);
    }

    [Fact]
    public void P764_TheStarterScrollGivesALeash()
    {
        var pm = Player("Starter");
        var scroll = new PetChoiceScroll();
        pm.Backpack!.DropItem(scroll);

        var pet = scroll.Choose(pm, true);

        Assert.NotNull(pet);
        Assert.Single(pm.Backpack.Items.OfType<PetLeash>());
        Assert.True(scroll.Deleted);

        Cleanup(pet, pm);
    }

    [Fact]
    public void P764_TheAnimalTrainerSellsTheLeash()
    {
        var info = new SBPetRebirth().BuyInfo.Single(b => b.Type == typeof(PetLeash));
        Assert.Equal(ClusterFPetLeash.Price, info.Price);
        Assert.InRange(info.Price, 1, 1000);
    }

    [Fact]
    public void P764_TheFollowerCountIsRightAfterARestartWhileLeashed()
    {
        var pm = Player("Restarter");
        var dog = DogOf(pm);
        var fig = LeashIn(pm).Use(pm, dog)!;
        Assert.NotNull(fig);

        // As ShrunkPetVerification: the owner's Followers and AllFollowers are not saved; the pet's own load re-adds it.
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        dog.Serialize(writer);
        writer.Flush();
        pm.Followers = 0;
        pm.RemoveFollower(dog);
        dog.Deserialize(new BufferReader(buffer));
        _out.WriteLine($"after the pet's load: followers={pm.Followers}");

        typeof(ShrunkPet).GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, null);
        Assert.Equal(0, pm.Followers);

        Assert.True(fig.Restore(pm));
        Assert.Equal(dog.ControlSlots, pm.Followers);

        Cleanup(dog, pm);
    }
}
