using System;
using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/BarreraakRing.cs (CC9 batch 5). Wearing it body-mods the wearer
    // to 334 (a goblin); it refuses to equip while mounted, flying or already body-modded. ServUO re-applies the
    // body mod one tick after load if the ring is still worn; that is the AfterDeserialization here. ServUO's
    // "TODO: Get Hue" stands, so no hue.
    [TypeAlias("Server.Items.BarreraakRing")]
    [SerializationGenerator(0, false)]
    public partial class BarreraaksRing : GoldRing
    {
        [Constructible]
        public BarreraaksRing() => LootType = LootType.Blessed;

        public override int LabelNumber => 1095049; // Barreraak's Old Beat Up Ring

        public override bool CanEquip(Mobile from)
        {
            if (!base.CanEquip(from))
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

            if (parent is Mobile m)
            {
                m.BodyMod = 334;
            }
        }

        public override void OnRemoved(IEntity parent)
        {
            base.OnRemoved(parent);

            if (parent is Mobile m)
            {
                m.BodyMod = 0;
            }
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            if (Parent is Mobile m)
            {
                Timer.DelayCall(
                    TimeSpan.Zero,
                    () =>
                    {
                        if (!m.Mounted && !m.Flying && !m.IsBodyMod)
                        {
                            m.BodyMod = 334;
                        }
                    }
                );
            }
        }
    }
}
