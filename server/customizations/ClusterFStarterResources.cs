using ModernUO.Serialization;
using Server.Network;

namespace Server.Items;

// cc-P36 Part D: the New Haven dungeon's four starter resources (shard-migration notes/cc-P36-dungeon-build-1.md;
// Chase's decisions 2026-10-01, items 8, 9 and 12). For characters under about 50 skill: harvest 45 / 5 / 85, craft gate
// 45, bonuses about half of the lowest stock tier (ResourceInfo.cs, CraftAttributeInfo). Every number is a deviation.
//
// Each type is new, so SerializationGenerator(0, false) carries no migration (as ClusterFExtendedOres.cs). Names come
// from CraftResources.GetName (CraftResourceInfo number 0, as the extended ores and woods). None of the four is in any BOD,
// commission, work order or random resource pick; tests/Items/StarterResourcesVerification.cs asserts that against the
// tables.

// ---------------------------------------------------------------- Dawnstone (metal, CraftResource 18)

[SerializationGenerator(0, false)]
public partial class DawnstoneOre : BaseOre
{
    [Constructible]
    public DawnstoneOre(int amount = 1) : base(CraftResource.Dawnstone, amount)
    {
    }

    public override BaseIngot GetIngot() => new DawnstoneIngot();
}

[SerializationGenerator(0, false)]
public partial class DawnstoneIngot : BaseIngot
{
    [Constructible]
    public DawnstoneIngot(int amount = 1) : base(CraftResource.Dawnstone, amount)
    {
    }
}

// Dawnstone's second harvest type (cc-P36 Part A, F1): a GM stone miner gets a granite 10% of the time
// (patches/Mining.cs GetResourceType, Types[1]), as with every stock colored ore.
[SerializationGenerator(0, false)]
public partial class DawnstoneGranite : BaseGranite
{
    [Constructible]
    public DawnstoneGranite() : base(CraftResource.Dawnstone)
    {
    }
}

// ---------------------------------------------------------------- Burrowhide (leather, CraftResource 105)

// Carved later from the dungeon's creatures through OnCarve, not a new HideType (pinned enum; P24 3.4).
[SerializationGenerator(0, false)]
public partial class BurrowhideHides : BaseHides, IScissorable
{
    [Constructible]
    public BurrowhideHides(int amount = 1) : base(CraftResource.BurrowhideLeather, amount)
    {
    }

    // As pinned BarbedHides.Scissor (Items/Resources/Tailor/Hides.cs).
    public bool Scissor(Mobile from, Scissors scissors)
    {
        if (Deleted || !from.CanSee(this))
        {
            return false;
        }

        if (Core.AOS && !IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(502437); // Items you wish to cut must be in your backpack
            return false;
        }

        ScissorHelper(from, new BurrowhideLeather(), 1);

        return true;
    }
}

[SerializationGenerator(0, false)]
public partial class BurrowhideLeather : BaseLeather
{
    [Constructible]
    public BurrowhideLeather(int amount = 1) : base(CraftResource.BurrowhideLeather, amount)
    {
    }
}

// ---------------------------------------------------------------- Rootwood (wood, CraftResource 316)

[SerializationGenerator(0, false)]
public partial class RootwoodLog : Log
{
    [Constructible]
    public RootwoodLog(int amount = 1) : base(CraftResource.Rootwood, amount)
    {
    }

    // The stock pattern: a log cuts to boards at its own harvest requirement (pinned OakLog 65, Log.cs:138).
    public override bool Axe(Mobile from, BaseAxe axe) => TryCreateBoards(from, 45, new RootwoodBoard());
}

[SerializationGenerator(0, false)]
public partial class RootwoodBoard : Board
{
    [Constructible]
    public RootwoodBoard(int amount = 1) : base(CraftResource.Rootwood, amount)
    {
    }
}

// ---------------------------------------------------------------- Gloomsilk (cloth, cosmetic)

// cc-P24 3.6 option A, decided (item 8): a Cloth with a fixed hue and no bonus. Every cloth recipe consumes it, because
// consumption matches subclasses (Container.ConsumeTotal), and its hue carries into the crafted piece. It cannot be
// picked in the craft menu (cloth has no sub-resource list). It drops later from the dungeon's spiders.
// No bolt: BoltOfCloth.Scissor is not virtual and always makes plain Cloth, so a Gloomsilk bolt would cut into ordinary
// cloth unless it re-implemented IScissorable. Not worth it for a drop that is cloth already.
[SerializationGenerator(0, false)]
public partial class GloomsilkCloth : Cloth
{
    /// <summary>The first cc-P36 hue candidate (dusky violet-grey); Chase picks in a client.</summary>
    public const int GloomsilkHue = 0x02C9;

    [Constructible]
    public GloomsilkCloth(int amount = 1) : base(amount) => Hue = GloomsilkHue;

    public override string DefaultName => "gloomsilk cloth";

    public override void AddNameProperty(IPropertyList list)
    {
        if (Amount > 1)
        {
            list.Add(1050039, $"{Amount}\t{DefaultName}"); // ~1_NUMBER~ ~2_ITEMNAME~
        }
        else
        {
            list.Add(DefaultName);
        }
    }

    public override void OnSingleClick(Mobile from)
    {
        from.NetState.SendMessage(Serial, ItemID, MessageType.Regular, 0x3B2, 3, false, "ENU", "",
            Amount > 1 ? $"{Amount} {DefaultName}" : DefaultName);
    }
}
