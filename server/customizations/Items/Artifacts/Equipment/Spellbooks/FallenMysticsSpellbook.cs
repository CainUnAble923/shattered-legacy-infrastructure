using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Spellbooks/FallenMysticsSpellbook.cs (CC9 batch 5).
    // ServUO derives from Spellbook and restates the mystic book's three overrides (SpellbookType.Mystic,
    // BookOffset 677, BookCount 16) and 0x2D9D. Pinned ModernUO already ships that book as MysticSpellbook
    // (Items/Skill Items/Magical/MysticSpellbook.cs, same values), so this derives from it, the way
    // PetrifiedSnake derives from ModernUO's SerpentstoneStaff. ServUO's own MysticBook.cs is that same class
    // and is not ported.
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class FallenMysticsSpellbook : MysticSpellbook
    {
        [Constructible]
        public FallenMysticsSpellbook(ulong content = 0) : base(content)
        {
            Hue = 687;
            SkillBonuses.SetValues(0, SkillName.Mysticism, 10.0);
            Attributes.LowerManaCost = 5;
            Attributes.RegenMana = 1;
            Attributes.LowerRegCost = 10;
            Attributes.CastRecovery = 1;
            Attributes.CastSpeed = 1;
            Attributes.SpellDamage = 10;
            Slayer = SlayerName.Fey;
        }

        public override int LabelNumber => 1113867; // Fallen Mystic's Spellbook
    }
}
