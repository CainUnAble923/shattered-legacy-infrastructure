// cc-P261: the Smith's Seal (placeholder name), the process that makes a crafted item levelable (Chase's rule 2). It
// works only on crafted weapons, armour, shields and jewellery, never on found gear or an artifact
// (ClusterFLevelGear.WhyNot), and is used up only when it works. Where players get it (a Society of Smiths quest or a
// vendor) is Chase's to pick; until then nothing drops, sells or rewards it, so staff [add it.

#nullable enable

using ModernUO.Serialization;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class SmithsSealDeed : Item
{
    [Constructible]
    public SmithsSealDeed() : base(0x14F0)
    {
        Hue = 0x8A5;
        LootType = LootType.Blessed;
    }

    public override string DefaultName => "a Smith's Seal";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1070722, "Makes a crafted weapon, armor piece, shield or jewel levelable"); // ~1_NOTHING~
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        from.SendMessage("Target the crafted item to seal.");
        from.Target = new SealTarget(this);
    }

    private sealed class SealTarget : Target
    {
        private readonly SmithsSealDeed _seal;

        public SealTarget(SmithsSealDeed seal) : base(2, false, TargetFlags.None) => _seal = seal;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_seal.Deleted || !_seal.IsChildOf(from.Backpack))
            {
                return;
            }

            if (targeted is not Item item || !ClusterFLevelGear.Holds(from, item))
            {
                from.SendMessage("You must carry or wear the item.");
                return;
            }

            if (ClusterFLevelGear.MakeLevelable(item, from) is { } why)
            {
                from.SendMessage(0x22, why);
                return;
            }

            from.SendMessage(0x59, "The seal sinks into the metal. This item will now grow with you. ([ItemLevel)");
            _seal.Delete();
        }
    }
}
