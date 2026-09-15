using System;
using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items
{
    // ServUO: Items/Equipment/Armor/ElegantCollarOfFortune.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class ElegantCollarOfFortune : ElegantCollar
    {
        [Constructible]
        public ElegantCollarOfFortune()
        {
            Attributes.Luck = 300;
            Attributes.RegenMana = 1;
        }

        public override int LabelNumber => 1159225;
        public override int BasePhysicalResistance => 15;
        public override int BaseFireResistance => 10;
        public override int BaseColdResistance => 10;
        public override int BasePoisonResistance => 10;
        public override int BaseEnergyResistance => 15;
        public override int InitMinHits => 255;
        public override int InitMaxHits => 255;

        // ServUO's own override, which skips BaseArmor.OnCraft's exceptional-resist distribution and hue handling:
        // quality, maker's mark, player-constructed and the resource, nothing else. ITool -> BaseTool,
        // CraftResCol.GetAt(0) -> Resources[0], Crafter is the name string on ModernUO.
        public override int OnCraft(
            int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
            CraftItem craftItem, int resHue
        )
        {
            Quality = (ArmorQuality)quality;
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
