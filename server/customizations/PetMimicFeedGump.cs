using System;
using System.Text;
using Server.Gumps;
using Server.Items;
using Server.Network;

namespace Server.Items;

public class PetMimicFeedGump : Gump
{
    private const int GumpW = 380;
    private const int PadX  = 20;

    private readonly Mobile   _from;
    private readonly PetMimic _mimic;
    private readonly Item     _item;

    public PetMimicFeedGump(Mobile from, PetMimic mimic, Item item) : base(100, 100)
    {
        _from  = from;
        _mimic = mimic;
        _item  = item;

        var isTalisman = item is BaseTalisman;
        var category   = isTalisman ? MimicCategory.None : PetMimic.DetectCategory(item);
        var lines      = BuildStatLines(item);
        var gumpH      = 140 + lines.Count * 16 + 40;

        AddBackground(0, 0, GumpW, gumpH, 9200);

        // Title
        AddLabel(GumpW / 2 - 60, 10, 0x386, "Pet Mimic — Feed Confirmation");

        // Item name + category/type
        var itemName = item.Name ?? item.GetType().Name;
        AddLabel(PadX, 36, 0x47E, $"Item:     {itemName}");

        if (isTalisman)
        {
            AddLabel(PadX, 52, 0x47E, "Type:     Talisman");
            AddLabel(PadX, 68, 0x44,  "Absorbs talisman stats without changing category or form.");
        }
        else
        {
            var superName = SuperName(category);
            AddLabel(PadX, 52, 0x47E, $"Category: {category} ({superName})");
            if (mimic.LockedCategory == MimicCategory.None)
                AddLabel(PadX, 68, 0x44, $"This will bind your mimic to {superName} permanently.");
            else
                AddLabel(PadX, 68, 0x44, $"Bound to: {SuperName(mimic.LockedCategory)} — all {SuperName(mimic.LockedCategory).ToLower()} forms are compatible.");
        }

        // Stats header
        int y = 92;
        AddLabel(PadX, y, 0x455, "Stats that will be absorbed:");
        y += 16;

        if (lines.Count == 0)
        {
            AddLabel(PadX + 8, y, 0x3B2, "(no numeric stats found)");
            y += 16;
        }
        else
        {
            foreach (var line in lines)
            {
                AddLabel(PadX + 8, y, 0x44, line);
                y += 16;
            }
        }

        y += 8;

        // Destruction warning
        AddLabel(PadX, y, 0x020, "WARNING: The item will be permanently destroyed.");
        y += 20;

        // Buttons
        AddButton(PadX,        y, 4005, 4007, 1, GumpButtonType.Reply, 0);
        AddLabel(PadX + 35,    y + 2, 0x44, "Feed");

        AddButton(GumpW - 90,  y, 4017, 4019, 0, GumpButtonType.Reply, 0);
        AddLabel(GumpW - 55,   y + 2, 0x455, "Cancel");
    }

    private static string SuperName(MimicCategory cat) => cat switch
    {
        MimicCategory.WeaponSwordsmanship or
        MimicCategory.WeaponMaceFighting  or
        MimicCategory.WeaponFencing       or
        MimicCategory.WeaponArchery       or
        MimicCategory.WeaponWrestling     => "Weapons",
        MimicCategory.Armor or
        MimicCategory.Shield              => "Armor",
        MimicCategory.Jewelry             => "Jewelry",
        MimicCategory.Clothing            => "Clothing",
        _                                 => cat.ToString()
    };

    private static System.Collections.Generic.List<string> BuildStatLines(Item item)
    {
        var list  = new System.Collections.Generic.List<string>();
        var sb    = new StringBuilder();

        AosAttributes attrs = item switch
        {
            BaseWeapon w   => w.Attributes,
            BaseArmor a    => a.Attributes,
            BaseJewel j    => j.Attributes,
            BaseClothing c => c.Attributes,
            BaseTalisman t => t.Attributes,
            _              => null
        };

        AosSkillBonuses skills = item switch
        {
            BaseWeapon w   => w.SkillBonuses,
            BaseArmor a    => a.SkillBonuses,
            BaseJewel j    => j.SkillBonuses,
            BaseClothing c => c.SkillBonuses,
            BaseTalisman t => t.SkillBonuses,
            _              => null
        };

        if (attrs != null)
        {
            AddAttr(list, "Str Bonus",        attrs.BonusStr);
            AddAttr(list, "Dex Bonus",        attrs.BonusDex);
            AddAttr(list, "Int Bonus",        attrs.BonusInt);
            AddAttr(list, "Hit Point Inc",    attrs.BonusHits);
            AddAttr(list, "Stamina Inc",      attrs.BonusStam);
            AddAttr(list, "Mana Inc",         attrs.BonusMana);
            AddAttr(list, "Lower Reg Cost",   attrs.LowerRegCost,     "%");
            AddAttr(list, "Faster Casting",   attrs.CastSpeed);
            AddAttr(list, "FC Recovery",      attrs.CastRecovery);
            AddAttr(list, "Damage Increase",  attrs.WeaponDamage,     "%");
            AddAttr(list, "Hit Chance Inc",   attrs.AttackChance,     "%");
            AddAttr(list, "Def Chance Inc",   attrs.DefendChance,     "%");
            AddAttr(list, "Swing Speed Inc",  attrs.WeaponSpeed,      "%");
            AddAttr(list, "Spell Dmg Inc",    attrs.SpellDamage,      "%");
            AddAttr(list, "Lower Mana Cost",  attrs.LowerManaCost,    "%");
            AddAttr(list, "Reflect Physical", attrs.ReflectPhysical,  "%");
            AddAttr(list, "Enhance Potions",  attrs.EnhancePotions,   "%");
            AddAttr(list, "Luck",             attrs.Luck);
            AddAttr(list, "HP Regen",         attrs.RegenHits);
            AddAttr(list, "Stam Regen",       attrs.RegenStam);
            AddAttr(list, "Mana Regen",       attrs.RegenMana);
        }

        AddAttr(list, "Phys Resist",   item.PhysicalResistance, "%");
        AddAttr(list, "Fire Resist",   item.FireResistance,     "%");
        AddAttr(list, "Cold Resist",   item.ColdResistance,     "%");
        AddAttr(list, "Poison Resist", item.PoisonResistance,   "%");
        AddAttr(list, "Energy Resist", item.EnergyResistance,   "%");

        if (item is BaseArmor ba2)
        {
            AddAttr(list, "Self Repair", ba2.ArmorAttributes.SelfRepair);
            AddAttr(list, "Mage Armor",  ba2.ArmorAttributes.MageArmor);
        }

        if (item is BaseWeapon bw)
        {
            AddAttr(list, "Hit Dispel",        bw.WeaponAttributes.HitDispel,       "%");
            AddAttr(list, "Hit Fireball",      bw.WeaponAttributes.HitFireball,     "%");
            AddAttr(list, "Hit Harm",          bw.WeaponAttributes.HitHarm,         "%");
            AddAttr(list, "Hit Magic Arrow",   bw.WeaponAttributes.HitMagicArrow,   "%");
            AddAttr(list, "Hit Lightning",     bw.WeaponAttributes.HitLightning,    "%");
            AddAttr(list, "Hit Lower Attack",  bw.WeaponAttributes.HitLowerAttack,  "%");
            AddAttr(list, "Hit Lower Defend",  bw.WeaponAttributes.HitLowerDefend,  "%");
            AddAttr(list, "Hit Leech Hits",    bw.WeaponAttributes.HitLeechHits,    "%");
            AddAttr(list, "Hit Leech Stam",    bw.WeaponAttributes.HitLeechStam,    "%");
            AddAttr(list, "Hit Leech Mana",    bw.WeaponAttributes.HitLeechMana,    "%");
            AddAttr(list, "Hit Cold Area",     bw.WeaponAttributes.HitColdArea,     "%");
            AddAttr(list, "Hit Fire Area",     bw.WeaponAttributes.HitFireArea,     "%");
            AddAttr(list, "Hit Poison Area",   bw.WeaponAttributes.HitPoisonArea,   "%");
            AddAttr(list, "Hit Energy Area",   bw.WeaponAttributes.HitEnergyArea,   "%");
            AddAttr(list, "Hit Physical Area", bw.WeaponAttributes.HitPhysicalArea, "%");

            if (bw.Slayer != SlayerName.None)
            {
                var entry = SlayerGroup.GetEntryByName(bw.Slayer);
                list.Add($"Slayer: {(entry != null ? Capitalize(entry.SlayerText(out _)) : bw.Slayer.ToString())}");
            }
            if (bw.Slayer2 != SlayerName.None)
            {
                var entry2 = SlayerGroup.GetEntryByName(bw.Slayer2);
                list.Add($"Slayer2: {(entry2 != null ? Capitalize(entry2.SlayerText(out _)) : bw.Slayer2.ToString())}");
            }
        }

        if (skills != null)
        {
            for (var i = 0; i < 5; i++)
            {
                if (skills.GetValues(i, out var skill, out var bonus) && bonus > 0)
                {
                    list.Add($"{skill}: +{bonus:0.#}");
                }
            }
        }

        return list;
    }

    private static void AddAttr(System.Collections.Generic.List<string> list, string label, int value, string suffix = "")
    {
        if (value > 0)
            list.Add($"{label}: +{value}{suffix}");
    }

    // SlayerText returns lowercase; capitalise the first letter for display.
    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 1)
        {
            if (!_mimic.Deleted && !_item.Deleted)
            {
                _mimic.ConfirmEat(_from, _item);
            }
        }
        else
        {
            _from.SendMessage(0x59, "You decide not to feed the mimic.");
        }
    }
}
