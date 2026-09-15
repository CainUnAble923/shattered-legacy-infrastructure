using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Clothing/ObiDiEnse.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    // ServUO chains base(0x27A0) into Obi(int hue), so the obi is briefly hue 0x27A0 and then Hue = 0; base() here.
    // The [Flippable(0x1515, 0x1530)] pair is ServUO's and names cloak graphics; it matches nothing on an obi and is inert on both emulators.
    [Flippable(0x1515, 0x1530)]
    [SerializationGenerator(0, false)]
    public partial class ObiDiEnse : Obi
    {
        [Constructible]
        public ObiDiEnse()
        {
            Hue = 0;
            Attributes.BonusInt = 5;
            Attributes.NightSight = 1;
            SkillBonuses.SetValues(0, SkillName.Focus, 5.0);
        }

        public override int LabelNumber => 1112406;
        public override int InitMinHits => 150;
        public override int InitMaxHits => 150;
        public override bool CanFortify => false;
    }
}
