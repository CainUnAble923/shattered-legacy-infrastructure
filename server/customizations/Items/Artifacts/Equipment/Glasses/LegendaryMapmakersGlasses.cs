using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Glasses/LegendaryMapmakersGlasses.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ItemQuality.Exceptional is ArmorQuality.Exceptional on an ElvenGlasses (BaseArmor).
    [SerializationGenerator(0, false)]
    public partial class LegendaryMapmakersGlasses : ElvenGlasses
    {
        [Constructible]
        public LegendaryMapmakersGlasses()
        {
            SkillBonuses.SetValues(0, SkillName.Cartography, Utility.RandomMinMax(1, 5));
            Quality = ArmorQuality.Exceptional;
        }

        public override int LabelNumber => 1159023;
    }
}
