// PetPerch.cs
//
// cc-P809 (P796 rank 2): one generic house perch for a small pet, our own build on the parrot perch pattern (pinned
// Items/Addons/ParrotPerchAddon.cs and our cc-P129 PetParrot). An idea from Hammerhand's eagle and chicken lizard perches
// (third-party, reference only): no code taken; its two perches, two items and two pets are one perch, its deed and one
// perched pet here.
//
// - A carpenter makes the deed (as the parrot perch: 100 boards' worth of logs, Carpentry 50 to 85). Placed in a house, it is
//   a perch of art 0x2FB6, the client's other "parrot perch".
// - A house owner or co-owner double-clicks the perch and targets a tamed pet of their own of a kind on the list (a chicken
//   lizard, as the incubator hatches; an eagle; a small bird). The pet leaves their followers and becomes a PerchedPet:
//   blessed, unmoving, renamable by the house's co-owners, showing its age in weeks, with the pet's name, body and hue.
//   It is one way, as the parrot is.
// - The pet follows the perch when it moves, is put away when the perch is redeeded, and travels in the deed, so a
//   re-placed perch gets it back.
//
// Saved types (generator, version 0, D40): PetPerchAddon, PetPerchDeed, PerchedPet. Numbers and the kinds allowed are in
// Configuration/pet-perch.json. The craft row is added at start, so an existing world has it with no seeding.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Items;
using Server.Multis;
using Server.Network;
using Server.Targeting;

namespace Server.Mobiles;

public sealed class PetPerchSettings
{
    public const string FileName = "pet-perch.json";

    public static PetPerchSettings Current { get; set; } = new();

    public int PerchItemID { get; set; } = 0x2FB6;
    public int PetHeight { get; set; } = 12;
    public int PlaceRange { get; set; } = 3;

    public double MinSkill { get; set; } = 50.0;
    public double MaxSkill { get; set; } = 85.0;
    public int Logs { get; set; } = 100;

    /// <summary>The creature types (short names) a perch takes.</summary>
    public List<string> Kinds { get; set; } = ["ChickenLizard", "Eagle", "Bird"];

    public bool Takes(Mobile m)
    {
        var name = m?.GetType().Name;

        foreach (var kind in Kinds)
        {
            if (kind.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void Validate(PetPerchSettings s, List<string> errors)
    {
        PetSettingsFile.Range(s.PerchItemID, 1, 0xFFFF, nameof(PerchItemID), errors);
        PetSettingsFile.Range(s.PetHeight, 0, 100, nameof(PetHeight), errors);
        PetSettingsFile.Range(s.PlaceRange, 1, 18, nameof(PlaceRange), errors);
        PetSettingsFile.Range(s.MinSkill, -50, 200, nameof(MinSkill), errors);
        PetSettingsFile.Range(s.MaxSkill, s.MinSkill, 250, nameof(MaxSkill), errors);
        PetSettingsFile.Range(s.Logs, 1, 10000, nameof(Logs), errors);

        foreach (var kind in s.Kinds ?? [])
        {
            if (AssemblyHandler.FindTypeByName(kind ?? "")?.IsSubclassOf(typeof(BaseCreature)) != true)
            {
                errors.Add($"kind \"{kind}\" is not a creature.");
            }
        }
    }
}

public static class PetPerch
{
    private static bool _registered;

    public static PetPerchSettings S => PetPerchSettings.Current;

    public static void Configure()
    {
        PetPerchSettings.Current = PetSettingsFile.LoadOrWriteDefaults<PetPerchSettings>(PetPerchSettings.FileName, PetPerchSettings.Validate);
        EventSink.ServerStarted += Register;
    }

    /// <summary>Adds the perch deed to Carpentry's Furniture page, beside the parrot perch. Idempotent; the test host calls it directly.</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        var carpentry = DefCarpentry.CraftSystem;

        if (carpentry == null)
        {
            Console.WriteLine("[PetPerch] WARNING: DefCarpentry.CraftSystem is null; the pet perch is not craftable.");
            return;
        }

        _registered = true;
        carpentry.AddCraft(typeof(PetPerchDeed), 1044290, "pet perch", S.MinSkill, S.MaxSkill, typeof(Log), 1044041, S.Logs, 1044351);
    }

    /// <summary>Puts a tamed pet on a perch, or says why not. Returns the perched pet, or null.</summary>
    public static PerchedPet Place(Mobile from, PetPerchAddon perch, Mobile target)
    {
        var house = BaseHouse.FindHouseAt(perch);

        if (house?.IsCoOwner(from) != true)
        {
            from.SendMessage("Pets can only be placed on perches in houses where you are an owner or co-owner.");
            return null;
        }

        if (perch.Pet is { Deleted: false })
        {
            from.SendMessage("That perch already has a pet.");
            return null;
        }

        if (target is not BaseCreature pet || !S.Takes(pet))
        {
            from.SendMessage("That will not sit on a perch. A chicken lizard or a bird will.");
            return null;
        }

        if (!pet.Controlled || pet.ControlMaster != from || pet.Summoned)
        {
            from.SendMessage("You can only perch a tamed pet of your own.");
            return null;
        }

        if (!pet.InRange(perch.Location, S.PlaceRange) || pet.Map != perch.Map)
        {
            from.SendMessage("Bring your pet closer to the perch.");
            return null;
        }

        var perched = new PerchedPet(pet);
        perched.MoveToWorld(new Point3D(perch.X, perch.Y, perch.Z + S.PetHeight), perch.Map);
        perch.Pet = perched;

        // The pet leaves its owner's followers and the world; the perched pet takes its place.
        pet.SetControlMaster(null);
        pet.Delete();

        from.SendMessage("Your pet settles on its perch.");
        return perched;
    }
}

[SerializationGenerator(0)]
public partial class PetPerchAddon : BaseAddon
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private PerchedPet _pet;

    [Constructible]
    public PetPerchAddon() : this(null)
    {
    }

    public PetPerchAddon(PerchedPet pet)
    {
        AddComponent(new AddonComponent(PetPerch.S.PerchItemID), 0, 0, 0);
        _pet = pet;
    }

    public override BaseAddonDeed Deed => new PetPerchDeed(_pet);
    public override bool RetainDeedHue => true;

    public override void OnComponentUsed(AddonComponent c, Mobile from)
    {
        if (_pet is { Deleted: false })
        {
            from.SendMessage("That perch already has a pet.");
            return;
        }

        if (BaseHouse.FindHouseAt(this)?.IsCoOwner(from) != true)
        {
            from.SendMessage("Pets can only be placed on perches in houses where you are an owner or co-owner.");
            return;
        }

        from.SendMessage("Target the pet you wish to place on this perch.");
        from.BeginTarget(12, false, TargetFlags.None, (m, targeted) => PetPerch.Place(m, this, targeted as Mobile));
    }

    public override void OnLocationChange(Point3D oldLocation)
    {
        base.OnLocationChange(oldLocation);

        if (_pet != null)
        {
            _pet.Location = new Point3D(X, Y, Z + PetPerch.S.PetHeight);
        }
    }

    public override void OnMapChange()
    {
        base.OnMapChange();

        if (_pet != null)
        {
            _pet.Map = Map;
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        _pet?.Internalize();
    }
}

[SerializationGenerator(0)]
public partial class PetPerchDeed : BaseAddonDeed
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [InvalidateProperties]
    private PerchedPet _pet;

    [Constructible]
    public PetPerchDeed() : this(null)
    {
    }

    public PetPerchDeed(PerchedPet pet)
    {
        LootType = LootType.Blessed;
        _pet = pet;
    }

    public override BaseAddon Addon => new PetPerchAddon(_pet);

    public override string DefaultName => "a pet perch deed";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (_pet is { Deleted: false })
        {
            list.Add(1060658, $"Includes\t{_pet.Name ?? _pet.DefaultName}"); // ~1_val~: ~2_val~
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        // As the parrot perch's deed (ParrotPerchAddon-pet-parrot.patch): a pet already standing on its new perch is kept,
        // one still put away dies with the deed.
        if (_pet != null && !_pet.Deleted && _pet.Map == Map.Internal)
        {
            _pet.Delete();
        }
    }
}

/// <summary>A house pet on a perch: the look of the tamed pet it was, blessed and unmoving. One way, as the pet parrot.</summary>
[SerializationGenerator(0, false)]
public partial class PerchedPet : BaseCreature
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private DateTime _birth;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _kind;

    [Constructible]
    public PerchedPet() : base(AIType.AI_Animal, FightMode.None)
    {
        Body = 716;
        _kind = "chicken lizard";
        Setup();
    }

    public PerchedPet(BaseCreature from) : base(AIType.AI_Animal, FightMode.None)
    {
        Body = from.Body;
        Hue = from.Hue;
        BaseSoundID = from.BaseSoundID;
        _kind = KindOf(from.DefaultName);

        if (from.Name != null && from.Name != from.DefaultName)
        {
            Name = from.Name;
        }

        Setup();
    }

    /// <summary>"a chicken lizard" is "chicken lizard", "an eagle" is "eagle".</summary>
    public static string KindOf(string defaultName)
    {
        var name = defaultName ?? "pet";

        if (name.StartsWith("a ", StringComparison.Ordinal))
        {
            return name[2..];
        }

        return name.StartsWith("an ", StringComparison.Ordinal) ? name[3..] : name;
    }

    private void Setup()
    {
        SetStr(1, 5);
        SetDex(25, 30);
        SetInt(2);
        SetHits(1, Str);
        SetMana(0);

        CantWalk = true;
        Frozen = true;
        Blessed = true;
        _birth = Core.Now;
    }

    public override string DefaultName => $"a perched {_kind ?? "pet"}";

    public override bool NoHouseRestrictions => true;

    public override FoodType FavoriteFood => FoodType.FruitsAndVeggies | FoodType.GrainsAndHay;

    public int Weeks => (int)((Core.Now - _birth).TotalDays / 7);

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (Weeks == 1)
        {
            list.Add(1072626); // 1 week old
        }
        else if (Weeks > 1)
        {
            list.Add(1072627, Weeks.ToString()); // ~1_AGE~ weeks old
        }
    }

    public override void OnStatsQuery(Mobile from)
    {
        if (from.Map == Map && Utility.InUpdateRange(Location, from.Location) && from.CanSee(this))
        {
            if (from.AccessLevel == AccessLevel.Player && BaseHouse.FindHouseAt(this)?.IsCoOwner(from) == true)
            {
                from.SendMessage("As an owner of this house, you may rename this pet.");
            }

            from.NetState.SendMobileStatus(from, this);
        }
    }

    public override bool CanBeRenamedBy(Mobile from) =>
        from.AccessLevel > AccessLevel.Player || BaseHouse.FindHouseAt(this)?.IsCoOwner(from) == true;

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Frozen = true;
    }
}
