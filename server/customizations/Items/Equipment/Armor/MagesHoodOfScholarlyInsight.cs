using System;
using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/MagesHoodOfScholarlyInsight.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class MagesHoodOfScholarlyInsight : MagesHood
    {
        [Constructible]
        public MagesHoodOfScholarlyInsight(int hue = 0) : base(hue)
        {
            Attributes.BonusMana = 15;
            Attributes.RegenMana = 2;
            Attributes.SpellDamage = 15;
            Attributes.CastSpeed = 1;
            Attributes.LowerManaCost = 10;
        }

        public override int LabelNumber => 1159229;
        public override int BasePhysicalResistance => 15;
        public override int BaseFireResistance => 15;
        public override int BaseColdResistance => 15;
        public override int BasePoisonResistance => 15;
        public override int BaseEnergyResistance => 15;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // ServUO's own override, which skips BaseClothing.OnCraft's hue handling:
        // quality, maker's mark, player-constructed and the resource, nothing else. ITool -> BaseTool,
        // CraftResCol.GetAt(0) -> Resources[0], Crafter is the name string on ModernUO.
        public override int OnCraft(
            int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
            CraftItem craftItem, int resHue
        )
        {
            Quality = (ClothingQuality)quality;
            PlayerConstructed = true;

            if (makersMark)
            {
                Crafter = from.RawName;
            }

            if (!craftItem.ForceNonExceptional)
            {
                typeRes ??= craftItem.Resources[0].ItemType;

                Resource = CraftResources.GetFromType(typeRes);
            }

            return quality;
        }
    }
}
