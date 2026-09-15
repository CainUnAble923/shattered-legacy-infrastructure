using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Initiation/InitiationBag.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class InitiationSuitBag : Bag
    {
        [Constructible]
        public InitiationSuitBag()
        {
            Hue = 0x30;
            DropItem(new InitiationArms());
            DropItem(new InitiationCap());
            DropItem(new InitiationChest());
            DropItem(new InitiationGloves());
            DropItem(new InitiationGorget());
            DropItem(new InitiationLegs());
        }

        public override string DefaultName => "Initiation Suit Bag";
    }
}
