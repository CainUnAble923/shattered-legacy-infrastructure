using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/CreepingVine.cs (CC9).
    [SerializationGenerator(0, false)]
    public partial class CreepingVine : Item
    {
        [Constructible]
        public CreepingVine() : base(Utility.Random(18322, 4))
        {
        }

        public override int LabelNumber => 1112401;
    }
}
