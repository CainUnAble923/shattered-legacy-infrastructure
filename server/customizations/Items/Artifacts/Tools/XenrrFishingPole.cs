using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Tools/XenrrFishingPole.cs (CC9). Equipping it changes the wearer's
    // body to 723 while it is held; the body mod is re-applied after a world load.
    //
    // DEVIATION (CC9 D-4): ServUO's FishingPole carries AosAttributes and Xenrr's sets
    // SpellChanneling = 1 and CastSpeed = -1. ModernUO's FishingPole is a plain Item with no
    // attribute block, and AosAttributes.GetValue aggregates only the seven equipment base
    // classes, so an attribute block added here would never be read without a patch to
    // AOS.cs. Both lines are omitted. Cost to restore is in notes/cc9-artifacts.md.
    [SerializationGenerator(0, false)]
    public partial class XenrrFishingPole : FishingPole
    {
        [Constructible]
        public XenrrFishingPole() => LootType = LootType.Blessed;

        public override int LabelNumber => 1095066;

        public override bool OnEquip(Mobile from)
        {
            if (!base.OnEquip(from))
            {
                return false;
            }

            if (from.Mounted)
            {
                from.SendLocalizedMessage(1010097); // You cannot use this while mounted.
                return false;
            }

            if (from.Flying)
            {
                from.SendLocalizedMessage(1113414); // You can't use this while flying!
                return false;
            }

            if (from.IsBodyMod)
            {
                from.SendLocalizedMessage(1111896); // You may only change forms while in your original body.
                return false;
            }

            return true;
        }

        public override void OnAdded(IEntity parent)
        {
            base.OnAdded(parent);

            if (parent is Mobile from)
            {
                from.FixedParticles(0x3728, 1, 13, 5042, EffectLayer.Waist);

                from.BodyMod = 723;
                from.HueMod = 0;
            }
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m && !Deleted)
            {
                m.BodyMod = 0;
                m.HueMod = -1;
                m.FixedParticles(0x3728, 1, 13, 5042, EffectLayer.Waist);
            }
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            if (Parent is not Mobile m)
            {
                return;
            }

            Timer.DelayCall(
                () =>
                {
                    if (!m.Mounted && !m.Flying && !m.IsBodyMod)
                    {
                        m.BodyMod = 723;
                        m.HueMod = 0;
                    }
                }
            );
        }
    }
}
