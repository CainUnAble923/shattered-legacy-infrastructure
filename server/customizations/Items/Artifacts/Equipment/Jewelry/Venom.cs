using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/Venom.cs (CC9).
    [SerializationGenerator(0, false)]
    public partial class Venom : GoldBracelet
    {
        [Constructible]
        public Venom()
        {
            Hue = 1371;
            Attributes.CastRecovery = 1;
            Attributes.CastSpeed = 2;
            Attributes.SpellDamage = 10;
            Resistances.Poison = 20;
        }

        public override int LabelNumber => 1114783; // Venom
    }
}
