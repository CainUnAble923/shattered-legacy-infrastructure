using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/RangersCloakOfAugmentation.cs (CC9 batch 5).
    // ServUO derives from Cloak and gets set/absorption state from BaseClothing. Here that state lives on
    // BaseSetClothing (S10), so the piece derives from that and reproduces stock Cloak's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 4 for the set-armour bucket.
    // Dropped: IsArtifact (D-1); [Alterable] (D-20: pinned ModernUO has no alter-item system, zero consumers of the name); Cloak's IArcaneEquip machinery (D-7: an artifact piece is never crafted arcane).
    [Flippable]
    [SerializationGenerator(0, false)]
    public partial class RangersCloakOfAugmentation : BaseSetClothing
    {
        [Constructible]
        public RangersCloakOfAugmentation() : base(0x1515, Layer.Cloak)
        {
            Hue = 0x54A;
            AbsorptionAttributes.EaterKinetic = 5;
            Attributes.SpellDamage = 3;
            Attributes.LowerManaCost = 1;
            Attributes.WeaponSpeed = 5;
        }

        public override int LabelNumber => 1115514;

        // Stock Cloak members, reproduced because the parent changed.
        public override double DefaultWeight => 5.0;

        // Stock Cloak.Flip, reproduced because the parent changed ([Flippable] with no ids calls it).
        public void Flip()
        {
            ItemID = ItemID switch
            {
                0x1515 => 0x1530,
                0x1530 => 0x1515,
                _      => ItemID
            };
        }
    }
}
