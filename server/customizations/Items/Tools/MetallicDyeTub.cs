// cc-P199: ServUO pub57 Scripts/Items/Tools/MetallicDyeTub.cs. The metallic dye tub (1150067), a blessed 5th-year
// veteran reward: it dyes ringmail, chainmail and plate armor, and nothing else, in the metallic hues. Its tub hue is set
// with dyes through the metallic hue picker.
//
// Two pinned differences, both carried in this file:
//   - ServUO picks the metallic hues with CustomHuePicker.MetallicDyeTub (MetallicDyeTub.cs:25). Pinned has no such
//     picker; it has the MetallicHues virtual instead, which makes Dyes open pinned's MetallicHuePicker on the tub
//     (Items/Skill Items/Tailor Items/Misc/Dyes.cs:43), the way pinned's MetallicClothDyetub does. Same hue range.
//   - ServUO's tub dyes metal through an AllowMetal virtual read by ServUO's DyeTub target (ServUO
//     Items/Tools/DyeTub.cs:40,246-264). Pinned's DyeTub has no AllowMetal and its target is private, so this tub's
//     double-click sends its own target: ServUO's metal branch (too far 500446, locked down 1042419, worn 500861,
//     otherwise hue the armor and play 0x23E) and FailMessage for anything else. With AllowDyables false and no other
//     Allow* set, that is the whole of ServUO's target for this tub. Pinned's target also refuses quest items
//     (1151836); ServUO's has no such line, so neither does this one.
// The reward check and its property line are ServUO's (MetallicDyeTub.cs:36,46-47), through pinned's RewardSystem.

using ModernUO.Serialization;
using Server.Engines.VeteranRewards;
using Server.Targeting;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class MetallicDyeTub : DyeTub, IRewardItem
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private bool _isRewardItem;

        [Constructible]
        public MetallicDyeTub() => LootType = LootType.Blessed;

        public override bool AllowDyables => false;
        public override int TargetMessage => 1080393; // Select the metal item to dye.
        public override int FailMessage => 1080394;   // You can only dye metal with this tub.
        public override int LabelNumber => 1150067;   // Metallic Dye Tub
        public override bool MetallicHues => true;

        public override void OnDoubleClick(Mobile from)
        {
            if (_isRewardItem && !RewardSystem.CheckIsUsableBy(from, this))
            {
                return;
            }

            if (from.InRange(GetWorldLocation(), 1))
            {
                from.SendLocalizedMessage(TargetMessage);
                from.Target = new InternalTarget(this);
            }
            else
            {
                from.SendLocalizedMessage(500446); // That is too far away.
            }
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            if (Core.ML && _isRewardItem)
            {
                list.Add(1076221); // 5th Year Veteran Reward
            }
        }

        private class InternalTarget : Target
        {
            private readonly MetallicDyeTub _tub;

            public InternalTarget(MetallicDyeTub tub) : base(1, false, TargetFlags.None) => _tub = tub;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (targeted is BaseArmor
                    {
                        MaterialType: ArmorMaterialType.Chainmail or ArmorMaterialType.Ringmail or ArmorMaterialType.Plate
                    } item)
                {
                    if (!from.InRange(_tub.GetWorldLocation(), 1) || !from.InRange(item.GetWorldLocation(), 1))
                    {
                        from.SendLocalizedMessage(500446); // That is too far away.
                    }
                    else if (!item.Movable)
                    {
                        from.SendLocalizedMessage(1042419); // You may not dye leather items which are locked down.
                    }
                    else if (item.Parent is Mobile)
                    {
                        from.SendLocalizedMessage(500861); // Can't Dye clothing that is being worn.
                    }
                    else
                    {
                        item.Hue = _tub.DyedHue;
                        from.PlaySound(0x23E);
                    }
                }
                else
                {
                    from.SendLocalizedMessage(_tub.FailMessage);
                }
            }
        }
    }
}
