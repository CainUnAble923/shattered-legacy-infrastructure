// =============================================================================
// RewardMountStatuette.cs
// cc-P365: what each reward route hands over: a statuette that calls its mount (RewardMounts.SummonFor), bonded to the
// player who uses it, and is used up. Drawn with the mount item's own icon (its ridden look's stand frame, 44 px), at a
// statuette's weight. The four achievement mounts have a class each, because an achievement's reward is a type
// (AchievementDef.RewardItems, made with Activator.CreateInstance).
// =============================================================================

#nullable enable

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class RewardMountStatuette : Item
{
    [SerializableField(0)]
    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _mountKey;

    [Constructible(AccessLevel.GameMaster)]
    public RewardMountStatuette(string mountKey) : base(RewardMounts.Find(mountKey)?.MountItem ?? RewardMounts.FirstMountItem)
    {
        _mountKey = mountKey;
        Weight = 1.0;
        LootType = LootType.Blessed;
    }

    public RewardMountDef? Def => RewardMounts.Find(_mountKey);

    // The art is the mount item's, whose tiledata is on the Mount layer: a statuette is never worn.
    public override bool CanEquip(Mobile m) => false;

    public override string DefaultName => $"a statuette of {Def?.Name ?? "a mount"}";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Double-click to call your mount. It comes bonded to you, once.");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm || Def is not { } def)
        {
            return;
        }

        if (!IsChildOf(pm.Backpack))
        {
            pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        if (RewardMounts.SummonFor(pm, def) != null)
        {
            Delete();
        }
    }
}

[SerializationGenerator(0, false)]
public partial class MoonlitUnicornStatuette : RewardMountStatuette
{
    [Constructible]
    public MoonlitUnicornStatuette() : base("moonlit-unicorn") { }
}

[SerializationGenerator(0, false)]
public partial class BonewalkerSteedStatuette : RewardMountStatuette
{
    [Constructible]
    public BonewalkerSteedStatuette() : base("bonewalker-steed") { }
}

[SerializationGenerator(0, false)]
public partial class EmberhoofStatuette : RewardMountStatuette
{
    [Constructible]
    public EmberhoofStatuette() : base("emberhoof") { }
}

[SerializationGenerator(0, false)]
public partial class WarWolfStatuette : RewardMountStatuette
{
    [Constructible]
    public WarWolfStatuette() : base("war-wolf") { }
}
