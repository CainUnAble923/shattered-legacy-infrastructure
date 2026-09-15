using System;
using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Spellbooks/ScrappersCompendium.cs (CC9 batch 5).
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class ScrappersCompendium : Spellbook
    {
        [Constructible]
        public ScrappersCompendium()
        {
            Hue = 0x494;
            Attributes.SpellDamage = 25;
            Attributes.LowerManaCost = 10;
            Attributes.CastSpeed = 1;
            Attributes.CastRecovery = 1;
        }

        public override int LabelNumber => 1072940;

        // ServUO's own override. ITool -> BaseTool; Crafter is the name string on ModernUO.
        public override int OnCraft(
            int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
            CraftItem craftItem, int resHue
        )
        {
            var magery = from.Skills.Magery.Value - 100;

            if (magery < 0)
            {
                magery = 0;
            }

            var count = (int)Math.Round(magery * Utility.RandomDouble() / 5);

            if (count > 2)
            {
                count = 2;
            }

            if (Utility.RandomDouble() < 0.5)
            {
                count = 0;
            }
            else
            {
                BaseRunicTool.ApplyAttributesTo(this, true, 0, count, 70, 80);
            }

            Attributes.SpellDamage = 25;
            Attributes.LowerManaCost = 10;
            Attributes.CastSpeed = 1;
            Attributes.CastRecovery = 1;

            if (makersMark)
            {
                Crafter = from.RawName;
            }

            return quality;
        }
    }
}
