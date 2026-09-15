using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Dardens Set/DardensSleeves.cs (CC9 batch 5).
    // DragonTurtleHide* (batch 3) is on BaseSetArmor, so the set members and AbsorptionAttributes resolve.
    // SetSelfRepair = 3 is live on armour (S1 BaseArmor-set-self-repair.patch).
    [SerializationGenerator(0, false)]
    public partial class DardensSleeves : DragonTurtleHideArms
    {
        [Constructible]
        public DardensSleeves()
        {
            AbsorptionAttributes.EaterKinetic = 2;
            Attributes.BonusStr = 4;
            Attributes.BonusHits = 4;
            Attributes.LowerRegCost = 15;
            SetAttributes.BonusMana = 15;
            SetAttributes.LowerManaCost = 20;
            SetSelfRepair = 3;
            SetPhysicalBonus = 9;
            SetFireBonus = 8;
            SetColdBonus = 8;
            SetPoisonBonus = 8;
            SetEnergyBonus = 8;
        }

        public override int LabelNumber => 1156242;
        public override SetItem SetID => SetItem.Darden;
        public override int Pieces => 4;
        public override int BasePhysicalResistance => 6;
        public override int BaseFireResistance => 7;
        public override int BaseColdResistance => 7;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 7;

        // ServUO's override adds list.Add(1156346) ("Myrmidex Slayer") here: the set slayer that
        // SetHelper.GetSetSlayer returns for a completed Darden set. That method is not present in our
        // SetItem.cs and ModernUO's SlayerName has no Myrmidex member (S1, D-2, the Aloron precedent), so the
        // line is not emitted either: a tooltip promising a slayer the shard does not apply is worse than none.
    }
}
