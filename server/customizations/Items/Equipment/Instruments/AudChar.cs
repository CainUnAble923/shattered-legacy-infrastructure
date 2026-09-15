using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Instruments/AudChar.cs (CC9 batch 5).
    [SerializationGenerator(0, false)]
    public partial class AudChar : BaseInstrument
    {
        [Constructible]
        public AudChar() : base(0x403B, 0x392, 0x44)
        {
        }

        public override double DefaultWeight => 10.0;
    }
}
