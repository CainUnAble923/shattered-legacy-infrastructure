using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Juggernaut/MalekisHonor.cs (CC9 batch 4).
    // ServUO derives from MetalKiteShield and gets set state from BaseArmor (through BaseShield). Here that
    // state lives on BaseSetShield (S10), so the piece derives from that and reproduces stock MetalKiteShield's
    // own members, including its IDyable.Dye.
    // SetSelfRepair is stored but inert on BaseSetShield (Q-038, S10 D-1); it keeps its value in the save.
    // Evocaricus is the other half of the Juggernaut set and is ported alongside.
    [SerializationGenerator(0, false)]
    public partial class MalekisHonor : BaseSetShield, IDyable
    {
        [Constructible]
        public MalekisHonor() : base(0x1B74)
        {
            SetHue = 0x76D;
            SetSelfRepair = 3;
            SetAttributes.DefendChance = 10;
            SetAttributes.BonusStr = 10;
            SetAttributes.WeaponSpeed = 35;
        }

        public override int LabelNumber => 1074312; // Maleki's Honor (Juggernaut Set)
        public override SetItem SetID => SetItem.Juggernaut;
        public override int Pieces => 2;

        public override int BasePhysicalResistance => 3;
        public override int BaseFireResistance => 3;
        public override int BaseColdResistance => 3;
        public override int BasePoisonResistance => 3;
        public override int BaseEnergyResistance => 3;

        // Stock MetalKiteShield members, reproduced because the parent changed.
        public override double DefaultWeight => 7.0;
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
