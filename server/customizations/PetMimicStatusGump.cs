using System;
using System.Collections.Generic;
using Server.Gumps;
using Server.Items;
using Server.Network;

namespace Server.Items;

public class PetMimicStatusGump : Gump
{
    private const int GumpW = 380;
    private const int PadX  = 20;
    private const int Col2  = 120;
    private const int BarW  = 200;
    private const int BarH  = 14;

    private readonly Mobile   _from;
    private readonly PetMimic _mimic;

    public PetMimicStatusGump(Mobile from, PetMimic mimic) : base(150, 100)
    {
        _from  = from;
        _mimic = mimic;

        // ── Pre-compute which sections will render (needed for gump height) ──

        bool hasStats     = mimic.AccStrBonus > 0 || mimic.AccDexBonus > 0 || mimic.AccIntBonus > 0;
        bool hasBonusPool = mimic.AccBonusHits > 0 || mimic.AccBonusStam > 0 || mimic.AccBonusMana > 0;
        bool hasMagic     = mimic.AccLRC > 0 || mimic.AccFC > 0 || mimic.AccFCR > 0 || mimic.AccDI > 0;
        bool hasCombat    = mimic.AccHCI > 0 || mimic.AccDCI > 0 || mimic.AccSSI > 0 || mimic.AccSDI > 0;
        bool hasUtility   = mimic.AccLMC > 0 || mimic.AccRP > 0 || mimic.AccEP > 0 || mimic.AccLuck > 0;
        bool hasRegen     = mimic.AccRegenHits > 0 || mimic.AccRegenStam > 0 || mimic.AccRegenMana > 0;
        bool hasResist    = mimic.AccPhysResist > 0 || mimic.AccFireResist > 0 || mimic.AccColdResist > 0
                         || mimic.AccPoisonResist > 0 || mimic.AccEnergyResist > 0;
        bool hasArmor     = mimic.AccSelfRepair > 0 || mimic.AccMageArmor > 0;

        // Slayers — up to 2 rows
        int slayerRows = (mimic.AccSlayer  != SlayerName.None ? 1 : 0)
                       + (mimic.AccSlayer2 != SlayerName.None ? 1 : 0);

        // Hit procs — build list of non-zero entries first
        var hitProcLines = BuildHitProcLines(mimic);

        // Skills
        int skillRows = mimic.AccSkillBonusIds?.Count ?? 0;

        bool hasAnything = hasStats || hasBonusPool || hasMagic || hasCombat || hasUtility ||
                           hasRegen || hasResist || hasArmor || slayerRows > 0 ||
                           hitProcLines.Count > 0 || skillRows > 0;

        // Total dynamic content rows (each 16px)
        int dynRows = (hasStats     ? 1 : 0)
                    + (hasBonusPool ? 1 : 0)
                    + (hasMagic     ? 1 : 0)
                    + (hasCombat    ? 1 : 0)
                    + (hasUtility   ? 1 : 0)
                    + (hasRegen     ? 1 : 0)
                    + (hasResist    ? 1 : 0)
                    + (hasArmor     ? 1 : 0)
                    + slayerRows
                    + hitProcLines.Count
                    + (!hasAnything ? 1 : 0)       // "None" placeholder
                    + (skillRows > 0 ? 1 + skillRows : 0); // skills header + rows

        // Fixed vertical budget: header (48) + HP (22) + State (22) + Category/Forms (44) +
        // separator (14) + "Accumulated Stats" header (20) + content-start padding (2) = 172
        // Footer: gap (8) + separator (10) + tip (20) + buttons (30) = 68
        int gumpH = 172 + dynRows * 16 + 68;

        AddBackground(0, 0, GumpW, gumpH, 9200);

        // ── Title ──────────────────────────────────────────────────────────
        AddLabel(GumpW / 2 - 44, 10, 0x386, "Pet Mimic");
        AddLabel(GumpW / 2 - 40, 26, 0x47E, "Status Report");

        // ── HP bar ─────────────────────────────────────────────────────────
        var hp    = mimic.CurrentHP;
        var maxHp = mimic.MaxHP;
        var pct   = maxHp > 0 ? (double)hp / maxHp : 0.0;

        AddLabel(PadX, 54, 0x455, "Health");
        AddImageTiled(Col2, 56, BarW, BarH, 9354);

        if (hp > 0)
        {
            var fillW  = Math.Max(1, (int)(BarW * pct));
            var barHue = pct > 0.60 ? 0x044 : pct > 0.30 ? 0x035 : 0x020;
            AddImageTiled(Col2, 56, fillW, BarH, 9353);
            AddLabel(Col2 + BarW + 4, 54, barHue, $"{hp}/{maxHp}");
        }
        else
        {
            AddLabel(Col2 + BarW + 4, 54, 0x020, $"0/{maxHp}");
        }

        // ── State ──────────────────────────────────────────────────────────
        AddLabel(PadX, 76, 0x455, "State");

        string stateText;
        int    stateHue;
        if      (hp <= 0)                                        { stateText = "Exhausted — restore health first"; stateHue = 0x020; }
        else if (mimic.LockedCategory == MimicCategory.None)     { stateText = "Dormant";                          stateHue = 0x47E; }
        else                                                     { stateText = $"Awakened ({mimic.LockedCategory})"; stateHue = 0x044; }
        AddLabel(Col2, 76, stateHue, stateText);

        // ── Category / forms ───────────────────────────────────────────────
        AddLabel(PadX, 94, 0x455, "Category");
        AddLabel(Col2, 94, 0x47E,
            mimic.LockedCategory == MimicCategory.None
                ? "None — feed it an item to awaken"
                : mimic.LockedCategory.ToString());

        AddLabel(PadX, 110, 0x455, "Forms");
        AddLabel(Col2, 110, 0x47E,
            mimic.KnownFormItemIDs.Count == 0
                ? "None"
                : $"{mimic.KnownFormItemIDs.Count} known  (active: #{mimic.ActiveFormItemID:X4})");

        // ── Separator ──────────────────────────────────────────────────────
        AddImageTiled(PadX, 130, GumpW - PadX * 2, 2, 9354);

        AddLabel(PadX, 136, 0x386, "Accumulated Stats");

        int y = 154;

        // ── Str / Dex / Int ────────────────────────────────────────────────
        if (hasStats)
        {
            AddLabel(PadX, y, 0x455, "Stats");
            var line = "";
            if (mimic.AccStrBonus > 0) line += $"Str+{mimic.AccStrBonus}  ";
            if (mimic.AccDexBonus > 0) line += $"Dex+{mimic.AccDexBonus}  ";
            if (mimic.AccIntBonus > 0) line += $"Int+{mimic.AccIntBonus}";
            AddLabel(Col2, y, 0x44, line.TrimEnd());
            y += 16;
        }

        // ── Hit Point / Stamina / Mana Increase ───────────────────────────
        if (hasBonusPool)
        {
            AddLabel(PadX, y, 0x455, "Bonus Pool");
            var line = "";
            if (mimic.AccBonusHits > 0) line += $"HP+{mimic.AccBonusHits}  ";
            if (mimic.AccBonusStam > 0) line += $"Stam+{mimic.AccBonusStam}  ";
            if (mimic.AccBonusMana > 0) line += $"Mana+{mimic.AccBonusMana}";
            AddLabel(Col2, y, 0x44, line.TrimEnd());
            y += 16;
        }

        // ── LRC / FC / FCR / DI ────────────────────────────────────────────
        if (hasMagic)
        {
            AddLabel(PadX, y, 0x455, "Magic");
            var line = "";
            if (mimic.AccLRC > 0) line += $"LRC:{mimic.AccLRC}%  ";
            if (mimic.AccFC  > 0) line += $"FC:{mimic.AccFC}  ";
            if (mimic.AccFCR > 0) line += $"FCR:{mimic.AccFCR}  ";
            if (mimic.AccDI  > 0) line += $"DI:{mimic.AccDI}%";
            AddLabel(Col2, y, 0x44, line.TrimEnd());
            y += 16;
        }

        // ── HCI / DCI / SSI / SDI ──────────────────────────────────────────
        if (hasCombat)
        {
            AddLabel(PadX, y, 0x455, "Combat");
            var line = "";
            if (mimic.AccHCI > 0) line += $"HCI:{mimic.AccHCI}%  ";
            if (mimic.AccDCI > 0) line += $"DCI:{mimic.AccDCI}%  ";
            if (mimic.AccSSI > 0) line += $"SSI:{mimic.AccSSI}%  ";
            if (mimic.AccSDI > 0) line += $"SDI:{mimic.AccSDI}%";
            AddLabel(Col2, y, 0x44, line.TrimEnd());
            y += 16;
        }

        // ── LMC / RP / EP / Luck ───────────────────────────────────────────
        if (hasUtility)
        {
            AddLabel(PadX, y, 0x455, "Utility");
            var line = "";
            if (mimic.AccLMC  > 0) line += $"LMC:{mimic.AccLMC}%  ";
            if (mimic.AccRP   > 0) line += $"RP:{mimic.AccRP}%  ";
            if (mimic.AccEP   > 0) line += $"EP:{mimic.AccEP}%  ";
            if (mimic.AccLuck > 0) line += $"Luck:{mimic.AccLuck}";
            AddLabel(Col2, y, 0x44, line.TrimEnd());
            y += 16;
        }

        // ── HP / Stam / Mana Regen ─────────────────────────────────────────
        if (hasRegen)
        {
            AddLabel(PadX, y, 0x455, "Regen");
            var line = "";
            if (mimic.AccRegenHits > 0) line += $"HP:{mimic.AccRegenHits}  ";
            if (mimic.AccRegenStam > 0) line += $"Stam:{mimic.AccRegenStam}  ";
            if (mimic.AccRegenMana > 0) line += $"Mana:{mimic.AccRegenMana}";
            AddLabel(Col2, y, 0x44, line.TrimEnd());
            y += 16;
        }

        // ── Resistances ────────────────────────────────────────────────────
        if (hasResist)
        {
            AddLabel(PadX, y, 0x455, "Resists");
            var line = "";
            if (mimic.AccPhysResist   > 0) line += $"P:{mimic.AccPhysResist}  ";
            if (mimic.AccFireResist   > 0) line += $"F:{mimic.AccFireResist}  ";
            if (mimic.AccColdResist   > 0) line += $"C:{mimic.AccColdResist}  ";
            if (mimic.AccPoisonResist > 0) line += $"Po:{mimic.AccPoisonResist}  ";
            if (mimic.AccEnergyResist > 0) line += $"E:{mimic.AccEnergyResist}";
            AddLabel(Col2, y, 0x44, line.TrimEnd());
            y += 16;
        }

        // ── Armor attributes ───────────────────────────────────────────────
        if (hasArmor)
        {
            AddLabel(PadX, y, 0x455, "Armor");
            var line = "";
            if (mimic.AccSelfRepair > 0) line += $"SelfRepair:{mimic.AccSelfRepair}  ";
            if (mimic.AccMageArmor  > 0) line += "MageArmor";
            AddLabel(Col2, y, 0x44, line.TrimEnd());
            y += 16;
        }

        // ── Slayers ────────────────────────────────────────────────────────
        if (mimic.AccSlayer != SlayerName.None)
        {
            AddLabel(PadX, y, 0x455, "Slayer");
            AddLabel(Col2, y, 0x44, SlayerDisplayName(mimic.AccSlayer));
            y += 16;
        }
        if (mimic.AccSlayer2 != SlayerName.None)
        {
            AddLabel(PadX, y, 0x455, "Slayer 2");
            AddLabel(Col2, y, 0x44, SlayerDisplayName(mimic.AccSlayer2));
            y += 16;
        }

        // ── Weapon hit procs ───────────────────────────────────────────────
        foreach (var procLine in hitProcLines)
        {
            AddLabel(PadX + 8, y, 0x44, procLine);
            y += 16;
        }

        // ── Nothing message ────────────────────────────────────────────────
        if (!hasAnything)
        {
            AddLabel(PadX, y, 0x3B2, "None — feed it equippable items to accumulate stats.");
            y += 16;
        }

        // ── Skill bonuses ──────────────────────────────────────────────────
        if (skillRows > 0)
        {
            AddLabel(PadX, y, 0x455, "Skills");
            y += 16;
            var ids    = mimic.AccSkillBonusIds;
            var values = mimic.AccSkillBonusValues;
            for (var i = 0; i < ids.Count; i++)
            {
                AddLabel(PadX + 8, y, 0x44, $"{(SkillName)ids[i]}: +{values[i]:0.#}");
                y += 16;
            }
        }

        y += 8;

        // ── Footer ─────────────────────────────────────────────────────────
        AddImageTiled(PadX, y, GumpW - PadX * 2, 2, 9354);
        y += 8;
        AddLabel(PadX, y, 0x3B2, "Gems grow max HP. Potions/bandages restore HP.");
        y += 20;

        if (mimic.KnownFormItemIDs.Count > 0)
        {
            AddButton(PadX, y, 4005, 4007, 2, GumpButtonType.Reply, 0);
            AddLabel(PadX + 35, y + 2, 0x44, $"Forms ({mimic.KnownFormItemIDs.Count})");
        }

        AddButton(GumpW / 2 - 20, y, 4017, 4019, 1, GumpButtonType.Reply, 0);
        AddLabel(GumpW / 2 + 15, y + 2, 0x455, "Close");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static string SlayerDisplayName(SlayerName name)
    {
        var entry = SlayerGroup.GetEntryByName(name);
        if (entry != null)
        {
            var text = entry.SlayerText(out _);
            return string.IsNullOrEmpty(text) ? name.ToString()
                   : char.ToUpper(text[0]) + text.Substring(1);
        }
        return name.ToString();
    }

    // Build compact display lines for non-zero weapon hit procs (≤3 per line).
    private static List<string> BuildHitProcLines(PetMimic m)
    {
        var procs = new List<string>();
        if (m.AccHitDispel      > 0) procs.Add($"Dispel:{m.AccHitDispel}%");
        if (m.AccHitFireball    > 0) procs.Add($"Fireball:{m.AccHitFireball}%");
        if (m.AccHitHarm        > 0) procs.Add($"Harm:{m.AccHitHarm}%");
        if (m.AccHitMagicArrow  > 0) procs.Add($"MagicArrow:{m.AccHitMagicArrow}%");
        if (m.AccHitLightning   > 0) procs.Add($"Lightning:{m.AccHitLightning}%");
        if (m.AccHitLowerAttack > 0) procs.Add($"LowerAtk:{m.AccHitLowerAttack}%");
        if (m.AccHitLowerDefend > 0) procs.Add($"LowerDef:{m.AccHitLowerDefend}%");
        if (m.AccHitLeechHits   > 0) procs.Add($"LeechHP:{m.AccHitLeechHits}%");
        if (m.AccHitLeechStam   > 0) procs.Add($"LeechStam:{m.AccHitLeechStam}%");
        if (m.AccHitLeechMana   > 0) procs.Add($"LeechMana:{m.AccHitLeechMana}%");
        if (m.AccHitColdArea    > 0) procs.Add($"ColdArea:{m.AccHitColdArea}%");
        if (m.AccHitFireArea    > 0) procs.Add($"FireArea:{m.AccHitFireArea}%");
        if (m.AccHitPoisonArea  > 0) procs.Add($"PoisArea:{m.AccHitPoisonArea}%");
        if (m.AccHitEnergyArea  > 0) procs.Add($"EnrgArea:{m.AccHitEnergyArea}%");
        if (m.AccHitPhysicalArea > 0) procs.Add($"PhysArea:{m.AccHitPhysicalArea}%");

        if (procs.Count == 0) return procs;

        // Pack 3 procs per display line
        var lines = new List<string>();
        for (int i = 0; i < procs.Count; i += 3)
        {
            var line = procs[i];
            if (i + 1 < procs.Count) line += "  " + procs[i + 1];
            if (i + 2 < procs.Count) line += "  " + procs[i + 2];
            lines.Add(line);
        }
        return lines;
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 2 && !_mimic.Deleted)
        {
            _from.SendGump(new PetMimicFormJournalGump(_from, _mimic));
        }
    }
}
