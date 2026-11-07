// HorseBarding.cs
//
// cc-P809 (P796 rank 3): crafted horse barding, our own build on the swamp dragon's pattern (pinned Misc/AOS.cs:175-190,
// Mobiles/Animals/Mounts/SwampDragon.cs, Items/Deeds/DragonBardingDeed.cs). An idea from ATruGod's horse barding pack
// (third-party, reference only; he credits an unnamed author): no code taken, and its dung joke stays out.
//
// - A blacksmith makes it (400 iron, Blacksmithy 60 to 110); it takes the metal's hue. Its owner uses it on their own tamed,
//   unridden horse, which takes the armored horse's look (body 284, mount item 0x3E92, the Charger of the Fallen pair) in
//   the metal's hue.
// - While ridden, it takes 5% of a monster's hit on the rider (10% if exceptional) and wears by what it takes, gone at 500
//   points (1,250 exceptional): half the dragon's, as a horse needs Taming 29.1 and the dragon 93.9 (Chase, 2026-11-07,
//   question Iron-P809-20261107-092103-6902). Players' hits pass by it, as they do the dragon's.
// - When it breaks the horse gets back the body, mount item and hue it had. Nothing else removes it, as with the dragon.
// - The horse's barding is a row in HorseBardingStore (GenericPersistence, versioned, D40), keyed by the horse's serial, so
//   stock Horse is not patched and its save is untouched; the row follows the same mobile through the stable and the
//   leash's figurine, and is dropped at save once the horse is gone.
//
// The one engine change: server/patches/AOS-horse-barding.patch calls Absorb beside the dragon's branch. Every number is in
// Configuration/horse-barding.json. The craft row is added at start, so an existing world has it with no seeding.

using System;
using System.Collections.Generic;
using System.IO;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Items;
using Server.Targeting;

namespace Server.Mobiles;

public sealed class HorseBardingSettings
{
    public const string FileName = "horse-barding.json";

    public static HorseBardingSettings Current { get; set; } = new();

    public int AbsorbPercent { get; set; } = 5;
    public int AbsorbPercentExceptional { get; set; } = 10;
    public int Durability { get; set; } = 500;
    public int DurabilityExceptional { get; set; } = 1250;

    public int IronIngots { get; set; } = 400;
    public double MinSkill { get; set; } = 60.0;
    public double MaxSkill { get; set; } = 110.0;

    public int BardedBody { get; set; } = 284;
    public int BardedMountItemID { get; set; } = 0x3E92;

    public static void Validate(HorseBardingSettings s, List<string> errors)
    {
        PetSettingsFile.Range(s.AbsorbPercent, 0, 100, nameof(AbsorbPercent), errors);
        PetSettingsFile.Range(s.AbsorbPercentExceptional, 0, 100, nameof(AbsorbPercentExceptional), errors);
        PetSettingsFile.Range(s.Durability, 1, 1000000, nameof(Durability), errors);
        PetSettingsFile.Range(s.DurabilityExceptional, 1, 1000000, nameof(DurabilityExceptional), errors);
        PetSettingsFile.Range(s.IronIngots, 1, 10000, nameof(IronIngots), errors);
        PetSettingsFile.Range(s.MinSkill, -50, 200, nameof(MinSkill), errors);
        PetSettingsFile.Range(s.MaxSkill, s.MinSkill, 250, nameof(MaxSkill), errors);
        PetSettingsFile.Range(s.BardedBody, 1, 0xFFFF, nameof(BardedBody), errors);
        PetSettingsFile.Range(s.BardedMountItemID, 1, 0xFFFF, nameof(BardedMountItemID), errors);
    }
}

/// <summary>One barded horse: the barding's state and the look to give back when it breaks.</summary>
public sealed class HorseBardingRecord
{
    public bool Exceptional;
    public string CraftedBy;
    public int HP;
    public CraftResource Resource;
    public int OldBody;
    public int OldItemID;
    public int OldHue;

    public HorseBardingRecord()
    {
    }

    public HorseBardingRecord(IGenericReader r)
    {
        Exceptional = r.ReadBool();
        CraftedBy = r.ReadString();
        HP = r.ReadEncodedInt();
        Resource = (CraftResource)r.ReadEncodedInt();
        OldBody = r.ReadEncodedInt();
        OldItemID = r.ReadEncodedInt();
        OldHue = r.ReadEncodedInt();
    }

    public void Serialize(IGenericWriter w)
    {
        w.Write(Exceptional);
        w.Write(CraftedBy);
        w.WriteEncodedInt(HP);
        w.WriteEncodedInt((int)Resource);
        w.WriteEncodedInt(OldBody);
        w.WriteEncodedInt(OldItemID);
        w.WriteEncodedInt(OldHue);
    }
}

/// <summary>Every barded horse, saved through pinned's GenericPersistence; versioned, an unknown version fails loudly (D40).</summary>
public class HorseBardingStore : GenericPersistence
{
    public const int CurrentVersion = 0;

    private static HorseBardingStore _instance;

    internal static readonly Dictionary<Serial, HorseBardingRecord> Rows = new();

    public HorseBardingStore() : base("HorseBarding", 10)
    {
    }

    public static void Configure() => _instance ??= new HorseBardingStore();

    public override void Serialize(IGenericWriter writer)
    {
        var dead = new List<Serial>();

        foreach (var serial in Rows.Keys)
        {
            if (World.FindMobile(serial) is not Horse { Deleted: false })
            {
                dead.Add(serial);
            }
        }

        foreach (var serial in dead)
        {
            Rows.Remove(serial);
        }

        writer.WriteEncodedInt(CurrentVersion);
        writer.WriteEncodedInt(Rows.Count);

        foreach (var (serial, record) in Rows)
        {
            writer.Write(serial);
            record.Serialize(writer);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"HorseBardingStore version {version} is not one this build can read.");
        }

        Rows.Clear();
        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var serial = reader.ReadSerial();
            Rows[serial] = new HorseBardingRecord(reader);
        }
    }
}

public static class HorseBarding
{
    private static bool _registered;

    private static HorseBardingSettings S => HorseBardingSettings.Current;

    public static void Configure()
    {
        HorseBardingSettings.Current = PetSettingsFile.LoadOrWriteDefaults<HorseBardingSettings>(
            HorseBardingSettings.FileName,
            HorseBardingSettings.Validate
        );

        EventSink.ServerStarted += Register;
    }

    /// <summary>Adds the barding to the blacksmith's Platemail page, beside the dragon's. Idempotent; the test host calls it directly.</summary>
    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        var smith = DefBlacksmithy.CraftSystem;

        if (smith == null)
        {
            Console.WriteLine("[HorseBarding] WARNING: DefBlacksmithy.CraftSystem is null; horse barding is not craftable.");
            return;
        }

        _registered = true;
        smith.AddCraft(typeof(CraftedHorseBarding), 1011078, "horse barding", S.MinSkill, S.MaxSkill, typeof(IronIngot), 1044036, S.IronIngots, 1044037);
    }

    /// <summary>Only a plain tamed horse: not the tutorial's private horse or any other kind built on Horse.</summary>
    public static bool IsBardable(Mobile m) => m is Horse && m.GetType() == typeof(Horse);

    public static HorseBardingRecord Get(Mobile horse) =>
        horse != null && HorseBardingStore.Rows.TryGetValue(horse.Serial, out var r) ? r : null;

    public static bool HasBarding(Mobile horse) => Get(horse) != null;

    public static int MaxHP(bool exceptional) => exceptional ? S.DurabilityExceptional : S.Durability;

    public static int Percent(bool exceptional) => exceptional ? S.AbsorbPercentExceptional : S.AbsorbPercent;

    public static void Apply(Horse horse, CraftedHorseBarding barding)
    {
        HorseBardingStore.Rows[horse.Serial] = new HorseBardingRecord
        {
            Exceptional = barding.Exceptional,
            CraftedBy = barding.CraftedBy,
            HP = MaxHP(barding.Exceptional),
            Resource = barding.Resource,
            OldBody = horse.Body,
            OldItemID = horse.ItemID,
            OldHue = horse.Hue
        };

        horse.Body = S.BardedBody;
        horse.ItemID = S.BardedMountItemID;
        horse.Hue = barding.Hue;
    }

    /// <summary>Takes the barding off and gives the horse back the look it had.</summary>
    public static void Remove(Horse horse)
    {
        if (!HorseBardingStore.Rows.Remove(horse.Serial, out var r))
        {
            return;
        }

        horse.Body = r.OldBody;
        horse.ItemID = r.OldItemID;
        horse.Hue = r.OldHue;
    }

    /// <summary>
    /// Called from AOS.Damage beside the swamp dragon's branch: a monster's hit on a rider whose horse is barded loses the
    /// barding's share, and the barding wears by it.
    /// </summary>
    public static void Absorb(Mobile from, Mobile m, ref int totalDamage)
    {
        if (from?.Player == true || m?.Player != true || m.Mount is not Horse horse || totalDamage <= 0)
        {
            return;
        }

        var r = Get(horse);

        if (r == null)
        {
            return;
        }

        var absorbed = AOS.Scale(totalDamage, Percent(r.Exceptional));

        totalDamage -= absorbed;
        r.HP -= absorbed;

        if (r.HP < 0)
        {
            Remove(horse);
            m.SendMessage(0x22, "Your horse's barding has been destroyed!");
        }
    }
}

[SerializationGenerator(0)]
public partial class CraftedHorseBarding : Item, ICraftable
{
    [InvalidateProperties]
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _craftedBy;

    [InvalidateProperties]
    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _exceptional;

    [SerializableField(2, fieldChanged: nameof(OnResourceChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [InvalidateProperties]
    private CraftResource _resource;

    [Constructible]
    public CraftedHorseBarding() : base(0x14F0)
    {
    }

    public override double DefaultWeight => 1.0;

    public override string DefaultName => _exceptional ? "exceptional horse barding" : "horse barding";

    private void OnResourceChanged(CraftResource oldValue, CraftResource newValue)
    {
        Hue = CraftResources.GetHue(newValue);
    }

    public int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        Exceptional = quality >= 2;

        if (makersMark)
        {
            CraftedBy = from?.RawName;
        }

        Resource = CraftResources.GetFromType(typeRes ?? craftItem.Resources[0].ItemType);

        if (craftSystem.GetContext(from)?.DoNotColor == true)
        {
            Hue = 0;
        }

        return quality;
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (_exceptional && _craftedBy != null)
        {
            list.Add(1050043, _craftedBy); // crafted by ~1_NAME~
        }

        list.Add(1060659, $"absorbs\t{HorseBarding.Percent(_exceptional)}% of monster damage"); // ~1_val~: ~2_val~
        list.Add(1060660, $"lasts for\t{HorseBarding.MaxHP(_exceptional)} damage"); // ~1_val~: ~2_val~
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        from.BeginTarget(6, false, TargetFlags.None, (m, targeted) => OnTarget(m, targeted));
        from.SendMessage("Select the horse you wish to place the barding on.");
    }

    public void OnTarget(Mobile from, object targeted)
    {
        if (Deleted)
        {
            return;
        }

        if (targeted is not Horse horse || !HorseBarding.IsBardable(horse) || HorseBarding.HasBarding(horse))
        {
            from.SendMessage("That is not an unarmored horse.");
        }
        else if (!horse.Controlled || horse.ControlMaster != from)
        {
            from.SendMessage("You can only put barding on a tamed horse that you own.");
        }
        else if (horse.Rider != null)
        {
            from.SendMessage("Dismount first.");
        }
        else if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1060640); // The item must be in your pack to use it.
        }
        else
        {
            HorseBarding.Apply(horse, this);
            Delete();
            from.SendMessage("You place the barding on your horse. It wears away as it turns aside the blows of monsters.");
        }
    }
}
