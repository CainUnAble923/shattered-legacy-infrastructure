// cc-P142 (decision 4): components that can only be stolen from monsters, each feeding a Thieves' Den recipe
// (ThievesDenCraftRegistrations; the proposals and the built ones are in shard-migration
// notes/cc-P142-monster-stealing-shard-rules.md). Stackable, on stock graphics with a hue each, placeholders for
// Chase's eye as the Burrows parts are (BurrowsCommon.cs). Which one a steal yields is by the creature's fame level
// (StealLootTables.Component): the thread and the ash from any creature, the leaf from level 10, the wire from 20, the
// fang from 30 (ServUO's fame-to-level line, StealingHandler.FameLevel).

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public abstract partial class StolenComponent : Item
{
    protected StolenComponent(int itemId, int hue, string name, int amount) : base(itemId)
    {
        Stackable = true;
        Amount = amount;
        Hue = hue;
        Name = name;
    }

    public override double DefaultWeight => 0.1;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060847, "stolen from monsters\tA Thieves' Den recipe calls for it"); // ~1_val~ ~2_val~
    }
}

/// <summary>Any creature. Feeds the Shadowsilk Cloak and the Pickpocket's Charm.</summary>
[SerializationGenerator(0, false)]
public partial class ShadowsilkThread : StolenComponent
{
    [Constructible]
    public ShadowsilkThread(int amount = 1) : base(0x0FA0, 0x455, "shadowsilk thread", amount)
    {
    }
}

/// <summary>Any creature. Feeds the Thief's Smoke Bomb.</summary>
[SerializationGenerator(0, false)]
public partial class EmberAsh : StolenComponent
{
    [Constructible]
    public EmberAsh(int amount = 1) : base(0x0F8C, 0x4E7, "ember ash", amount)
    {
    }
}

/// <summary>Fame level 10 and up. Feeds the Thief's Smoke Bomb (and the proposed Shadow Cloak IV).</summary>
[SerializationGenerator(0, false)]
public partial class Whisperleaf : StolenComponent
{
    [Constructible]
    public Whisperleaf(int amount = 1) : base(0x0F88, 0x48F, "whisperleaf", amount)
    {
    }
}

/// <summary>Fame level 20 and up. Feeds the proposed Gilded Lockpick.</summary>
[SerializationGenerator(0, false)]
public partial class ThiefsWire : StolenComponent
{
    [Constructible]
    public ThiefsWire(int amount = 1) : base(0x1876, 0x8A5, "thief's wire", amount)
    {
    }
}

/// <summary>Fame level 30 and up (the famous ones). Feeds the Pickpocket's Charm.</summary>
[SerializationGenerator(0, false)]
public partial class LuckyFang : StolenComponent
{
    [Constructible]
    public LuckyFang(int amount = 1) : base(0x10E8, 0x8A5, "lucky fang", amount)
    {
    }
}
