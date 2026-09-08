using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/GargishKiteShield.cs (CC9 batch 3). Based off a MetalKiteShield; ServUO
    // leaves the weight commented out and so does this.
    // Derives from BaseSetShield, not BaseShield: DragonHideShield (Items/Artifacts/Equipment/Armor/DragonHideShield.cs)
    // assigns AbsorptionAttributes on it, and a carrier cannot be inserted above a type once an instance exists
    // in a save (AGENTS.md, "Give a base its final parent the first time you port it"). SetID is None, so the
    // carrier is inert here. See shard-migration/notes/cc9-artifacts.md section 13.
    [Flippable(0x4201, 0x4206)]
    [SerializationGenerator(0, false)]
    public partial class GargishKiteShield : BaseSetShield, IDyable
    {
        [Constructible]
        public GargishKiteShield() : base(0x4201)
        {
        }

        public override int RequiredRaces => Race.AllowGargoylesOnly;

        public override int BasePhysicalResistance => 0;
        public override int BaseFireResistance => 0;
        public override int BaseColdResistance => 0;
        public override int BasePoisonResistance => 0;
        public override int BaseEnergyResistance => 1;

        public override int InitMinHits => 45;
        public override int InitMaxHits => 60;

        public override int AosStrReq => 45;

        public override int ArmorBase => 16;

        public bool Dye(Mobile from, DyeTub sender)
        {
            if (Deleted)
            {
                return false;
            }

            Hue = sender.DyedHue;

            return true;
        }
    }
}
