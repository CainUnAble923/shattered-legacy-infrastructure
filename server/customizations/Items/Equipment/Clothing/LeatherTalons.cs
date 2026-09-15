using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Clothing/LeatherTalons.cs (CC9 batch 5).
    // ServUO derives from BaseShoes and gets set/absorption state from BaseClothing. Here that state lives on
    // BaseSetClothing (S10), so the piece derives from that instead; nothing else changes.
    // Dropped: CanBeWornByGargoyles (D-2).
    // Derives from BaseSetClothing, not BaseShoes: UnicornManeWovenTalons (Items/Artifacts/DespiseArtifacts.cs) assigns
    // SAAbsorptionAttributes, and a set carrier cannot be inserted above a type once an instance exists in a save
    // (AGENTS.md, "Give a base its final parent the first time you port it"). SetID is None, so the carrier is inert here.
    [Flippable(0x41D8, 0x41D9)]
    [SerializationGenerator(0, false)]
    public partial class LeatherTalons : BaseSetClothing
    {
        [Constructible]
        public LeatherTalons(int hue = 0) : base(0x41D8, Layer.Shoes, hue)
        {
        }

        public override double DefaultWeight => 3.0;
        public override int RequiredRaces => Race.AllowGargoylesOnly;
        public override CraftResource DefaultResource => CraftResource.RegularLeather;

        // Stock BaseShoes.Scissor, reproduced because BaseSetClothing sits on BaseClothing directly.
        public override bool Scissor(Mobile from, Scissors scissors)
        {
            if (DefaultResource == CraftResource.None)
            {
                return base.Scissor(from, scissors);
            }

            from.SendLocalizedMessage(502440); // Scissors can not be used on that to produce anything.
            return false;
        }
    }
}
