using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Jewelry/GargishNecklace.cs (CC9), three types as in ServUO.
    // The two dropped overrides are the same inert pair as GargishEarrings: see the comment there.
    // S10 (2026-09-08): derives from BaseSetArmor, not BaseArmor. ServUO carries set and absorption
    // state on every BaseArmor, so for a ported armour-class base the carrier is the faithful parent;
    // with SetID None it is inert. Decided before any instance reached a live world, because the
    // parent is part of the save layout and cannot change afterwards without a wipe of the type.
    // See shard-migration/notes/s10-carriers.md section 4.
    [SerializationGenerator(0, false)]
    public partial class GargishNecklace : BaseSetArmor
    {
        [Constructible]
        public GargishNecklace() : this(0x4210)
        {
        }

        public GargishNecklace(int itemID) : base(itemID) => Layer = Layer.Neck;

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Chainmail;
        public override ArmorMeditationAllowance DefMedAllowance => ArmorMeditationAllowance.All;

        public override int BasePhysicalResistance => 1;
        public override int BaseFireResistance => 2;
        public override int BaseColdResistance => 2;
        public override int BasePoisonResistance => 2;
        public override int BaseEnergyResistance => 3;

        public override int InitMinHits => 30;
        public override int InitMaxHits => 40;
    }

    [SerializationGenerator(0, false)]
    public partial class GargishAmulet : GargishNecklace
    {
        [Constructible]
        public GargishAmulet() : base(0x4D0B)
        {
        }
    }

    [SerializationGenerator(0, false)]
    public partial class GargishStoneAmulet : GargishNecklace
    {
        [Constructible]
        public GargishStoneAmulet() : base(0x4D0A) => Hue = 2500;

        public override ArmorMaterialType MaterialType => ArmorMaterialType.Stone;

        public override int AosStrReq => 40;
        public override int OldStrReq => 20;
    }
}
