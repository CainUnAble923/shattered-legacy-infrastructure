// PetPerchVerification.cs
//
// cc-P809: the pet perch. Notes in shard-migration notes/cc-P809-pet-perch-horse-barding.md.
//
// Facts: the craft row; a house co-owner perches their own tamed chicken lizard or bird, which leaves their followers and
// becomes a blessed, unmoving perched pet with its look and name; strangers, others' pets and other kinds are refused; the
// pet follows the perch and travels in the deed; every new type saves and loads; the settings file is refused whole.

using System;
using System.Collections.Generic;
using Server;
using Server.Engines.Craft;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PetPerchVerification : IDisposable
{
    private static readonly Point3D Yard = new(5480, 3860, 0);
    private readonly List<IEntity> _made = [];

    public PetPerchVerification()
    {
        PetPerchSettings.Current = new PetPerchSettings();
    }

    public void Dispose()
    {
        foreach (var e in _made)
        {
            e?.Delete();
        }

        PetPerchSettings.Current = new PetPerchSettings();
    }

    private T Track<T>(T e) where T : IEntity
    {
        _made.Add(e);
        return e;
    }

    // The host has no multi data: a small house whose floor is the 7 x 7 it stands on (as BeekeepingVerification).
    private sealed class FloorHouse : SmallOldHouse
    {
        private static readonly MultiComponentList Floor = MakeFloor();

        public FloorHouse(Mobile owner) : base(owner, 0x64)
        {
        }

        private static MultiComponentList MakeFloor()
        {
            var list = new List<MultiTileEntry>();
            for (short x = -3; x <= 3; x++)
            {
                for (short y = -3; y <= 3; y++)
                {
                    list.Add(new MultiTileEntry(0x495, x, y, 0, (TileFlag)1));
                }
            }

            return new MultiComponentList(list);
        }

        public override MultiComponentList Components => Floor;
        public override bool IsAosRules => false;
    }

    private PlayerMobile Player(string name = "Owner")
    {
        var pm = Track(new PlayerMobile { Player = true, Race = Race.Human, Name = name });
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Yard, Map.Trammel);
        return pm;
    }

    private (BaseHouse House, PlayerMobile Owner, PetPerchAddon Perch) HouseWithPerch()
    {
        var owner = Player();
        var house = Track(new FloorHouse(owner));
        house.MoveToWorld(Yard, Map.Trammel);
        var perch = Track(new PetPerchAddon());
        perch.MoveToWorld(new Point3D(Yard.X + 1, Yard.Y, 0), Map.Trammel);
        return (house, owner, perch);
    }

    private T Tamed<T>(Mobile owner) where T : BaseCreature, new()
    {
        var pet = Track(new T());
        pet.MoveToWorld(Yard, Map.Trammel);
        pet.SetControlMaster(owner);
        return pet;
    }

    [Fact]
    public void ACarpenterMakesThePerchDeed()
    {
        if (DefCarpentry.CraftSystem == null)
        {
            DefCarpentry.Initialize();
        }

        PetPerch.Register();
        PetPerch.Register(); // idempotent

        var row = DefCarpentry.CraftSystem.CraftItems.SearchFor(typeof(PetPerchDeed));
        Assert.NotNull(row);
        Assert.Equal(typeof(Log), row.Resources[0].ItemType);
        Assert.Equal(100, row.Resources[0].Amount);
        Assert.Equal(50.0, row.Skills[0].MinSkill);
        Assert.Equal(0x2FB6, new PetPerchAddon().Components[0].ItemID);
    }

    [Fact]
    public void AnOwnersTamedChickenLizardSettlesOnThePerchAndLeavesTheirFollowers()
    {
        var (_, owner, perch) = HouseWithPerch();
        var lizard = Tamed<ChickenLizard>(owner);
        lizard.Name = "Pecky";
        lizard.Hue = 0x59;
        var followers = owner.Followers;
        Assert.True(followers >= 1);

        var perched = Track(PetPerch.Place(owner, perch, lizard));

        Assert.NotNull(perched);
        Assert.True(lizard.Deleted);
        Assert.Equal(followers - lizard.ControlSlots, owner.Followers);
        Assert.Same(perched, perch.Pet);
        Assert.Equal(716, (int)perched.Body);
        Assert.Equal(0x59, perched.Hue);
        Assert.Equal("Pecky", perched.Name);
        Assert.True(perched.Blessed);
        Assert.True(perched.Frozen);
        Assert.True(perched.CantWalk);
        Assert.Equal(perch.Z + 12, perched.Z);
        Assert.False(perched.Controlled);
        Assert.True(perched.CanBeRenamedBy(owner));

        // One pet to a perch.
        var bird = Tamed<Bird>(owner);
        Assert.Null(PetPerch.Place(owner, perch, bird));
        Assert.False(bird.Deleted);
    }

    [Fact]
    public void StrangersOthersPetsAndOtherKindsAreRefused()
    {
        var (_, owner, perch) = HouseWithPerch();
        var stranger = Player("Stranger");

        var theirs = Tamed<ChickenLizard>(stranger);
        Assert.Null(PetPerch.Place(stranger, perch, theirs)); // not a co-owner
        Assert.Null(PetPerch.Place(owner, perch, theirs));    // not the owner's pet
        Assert.False(theirs.Deleted);

        var horse = Tamed<Horse>(owner);
        Assert.Null(PetPerch.Place(owner, perch, horse));     // not a kind the perch takes
        Assert.False(horse.Deleted);

        var wild = Track(new Eagle());
        wild.MoveToWorld(Yard, Map.Trammel);
        Assert.Null(PetPerch.Place(owner, perch, wild));      // not tamed

        var eagle = Tamed<Eagle>(owner);
        var perched = Track(PetPerch.Place(owner, perch, eagle));
        Assert.NotNull(perched);
        Assert.Equal(5, (int)perched.Body);
        Assert.Equal("a perched eagle", perched.Name ?? perched.DefaultName);
        Assert.False(perched.CanBeRenamedBy(stranger));
    }

    [Fact]
    public void ThePetFollowsThePerchAndTravelsInItsDeed()
    {
        var (_, owner, perch) = HouseWithPerch();
        var perched = Track(PetPerch.Place(owner, perch, Tamed<ChickenLizard>(owner)));

        perch.Location = new Point3D(perch.X, perch.Y + 1, perch.Z);
        Assert.Equal(new Point3D(perch.X, perch.Y, perch.Z + 12), perched.Location);

        var deed = Track(Assert.IsType<PetPerchDeed>(perch.Deed));
        Assert.Same(perched, deed.Pet);
        perch.Delete();
        Assert.Equal(Map.Internal, perched.Map);
        Assert.False(perched.Deleted);

        var again = Track(Assert.IsType<PetPerchAddon>(deed.Addon));
        Assert.Same(perched, again.Pet);

        // A deed thrown away with its pet still put away takes the pet with it.
        var deed2 = new PetPerchDeed(perched);
        deed2.Delete();
        Assert.True(perched.Deleted);
    }

    [Fact]
    public void EveryNewTypeSavesAndLoads()
    {
        var pet = Track(new PerchedPet { Name = "Pecky" });
        var addon = Track(new PetPerchAddon(pet));
        var deed = Track(new PetPerchDeed(pet));

        Assert.Same(pet, RoundTrip(addon).Pet);
        Assert.Same(pet, RoundTrip(deed).Pet);

        pet.Birth = Core.Now.AddDays(-15);
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        pet.Serialize(writer);
        writer.Flush();
        var copy = (PerchedPet)Activator.CreateInstance(typeof(PerchedPet), pet.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        Assert.Equal(pet.Birth, copy.Birth);
        Assert.Equal("chicken lizard", copy.Kind);
        Assert.Equal(2, copy.Weeks);
    }

    private static T RoundTrip<T>(T original) where T : Item
    {
        var buffer = new byte[65536];
        var writer = new BufferWriter(buffer, true);
        original.Serialize(writer);
        writer.Flush();
        var copy = (T)Activator.CreateInstance(original.GetType(), original.Serial)!;
        copy.Deserialize(new BufferReader(buffer));
        return copy;
    }

    [Fact]
    public void ABadSettingsFileIsRefusedWhole()
    {
        var errors = new List<string>();
        Assert.Null(PetSettingsFile.Parse<PetPerchSettings>("{\"Kinds\": [\"Longsword\"]}", PetPerchSettings.Validate, errors));
        Assert.NotEmpty(errors);

        errors.Clear();
        var ok = PetSettingsFile.Parse<PetPerchSettings>(PetSettingsFile.ToJson(new PetPerchSettings()), PetPerchSettings.Validate, errors);
        Assert.NotNull(ok);
        Assert.Equal(new[] { "ChickenLizard", "Eagle", "Bird" }, ok.Kinds);
    }
}
