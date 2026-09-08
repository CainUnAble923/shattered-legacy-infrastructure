using System;
using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Instruments/DreadFlute.cs (CC9).
    [Flippable(0x315C, 0x315D)]
    [SerializationGenerator(0, false)]
    public partial class DreadFlute : BaseInstrument
    {
        [Constructible]
        public DreadFlute() : base(0x315C, 0x58B, 0x58C) // ServUO: TODO check sounds
        {
            ReplenishesCharges = true;
            Hue = 0x4F2;
        }

        public override double DefaultWeight => 1.0;

        public override int LabelNumber => 1075089; // Dread Flute

        public override int InitMinUses => 700;
        public override int InitMaxUses => 700;

        public override TimeSpan ChargeReplenishRate => TimeSpan.FromMinutes(15.0);
    }
}
