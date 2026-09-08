using System;
using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Armor/Sets/Darkwood/DarkwoodChest.cs (CC9 batch 4).
    // ServUO derives from WoodlandChest and gets set/absorption state from BaseArmor. Here that state lives on
    // BaseSetArmor (S1), so the piece derives from that and reproduces stock WoodlandChest's own members,
    // the way S1 did for the TigerPelt pieces under Aloron's set and CC9 batch 1 for the Greymist set.
    // Dropped: IsArtifact (D-1).
    // OnCraft is ServUO's own (resource bonus on craft); stock WoodlandChest has none in pinned ModernUO.
    [Flippable(0x2B67, 0x315E)]
    [SerializationGenerator(0, false)]
    public partial class DarkwoodChest : BaseSetArmor
    {
        [Constructible]
        public DarkwoodChest() : base(0x2B67)
        {
            Hue = 0x455;
            SetHue = 0x494;
            Attributes.BonusHits = 2;
            Attributes.DefendChance = 5;
            SetAttributes.ReflectPhysical = 25;
            SetAttributes.BonusStr = 10;
            SetAttributes.NightSight = 1;
            SetSelfRepair = 3;
            SetPhysicalBonus = 2;
            SetFireBonus = 5;
            SetColdBonus = 5;
            SetPoisonBonus = 3;
            SetEnergyBonus = 5;
        }

        public override int LabelNumber => 1073482;
        public override SetItem SetID => SetItem.Darkwood;
        public override int Pieces => 6;
        public override int BasePhysicalResistance => 8;
        public override int BaseFireResistance => 5;
        public override int BaseColdResistance => 5;
        public override int BasePoisonResistance => 7;
        public override int BaseEnergyResistance => 5;

        // Stock WoodlandChest members, reproduced because the parent changed.
        public override double DefaultWeight => 8.0;
        public override int InitMinHits => 50;
        public override int InitMaxHits => 65;
        public override int AosStrReq => 95;
        public override int OldStrReq => 95;
        public override int ArmorBase => 40;
        public override ArmorMaterialType MaterialType => ArmorMaterialType.Wood;
        public override int RequiredRaces => Race.AllowElvesOnly;

        // ServUO's override, with ITool -> BaseTool and List<CraftRes> indexing (ModernUO has no CraftResCol.GetAt).
        public override int OnCraft(
            int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
            CraftItem craftItem, int resHue
        )
        {
            if (resHue > 0)
            {
                Hue = resHue;
            }

            var resourceType = typeRes ?? craftItem.Resources[0].ItemType;

            Resource = CraftResources.GetFromType(resourceType);

            switch (Resource)
            {
                case CraftResource.Bloodwood:
                    Attributes.RegenHits = 2;
                    break;
                case CraftResource.Heartwood:
                    Attributes.Luck = 40;
                    break;
                case CraftResource.YewWood:
                    Attributes.RegenHits = 1;
                    break;
            }

            return 0;
        }
    }
}
