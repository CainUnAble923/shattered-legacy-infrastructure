using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/CoffinPiece.cs (CC9).
    [SerializationGenerator(0, false)]
    public partial class CoffinPiece : Item
    {
        [Constructible]
        public CoffinPiece() : base(Utility.RandomList(7481, 7480, 7479, 7452, 7451, 7450))
        {
        }

        public override int LabelNumber => 1116783;
    }
}
