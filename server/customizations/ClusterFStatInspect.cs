using System;
using System.Collections.Generic;
using Server.Commands;
using Server.Gumps;
using Server.Items;
using Server.Network;

namespace Server;

/// <summary>
/// [stats — player-accessible command that opens a full character stat summary gump.
/// Shows base stats, resistances, all aggregated AOS item bonuses, and active skill mods.
/// </summary>
public static class ClusterFStatInspect
{
    public static void Configure()
    {
        CommandSystem.Register("stats", AccessLevel.Player, OnStats);
    }

    [Usage("stats")]
    [Description("Opens a summary of all your character stats and item bonuses.")]
    private static void OnStats(CommandEventArgs e)
    {
        var m = e.Mobile;
        m.CloseGump<ClusterFStatInspectGump>();
        m.SendGump(new ClusterFStatInspectGump(m));
    }
}

public class ClusterFStatInspectGump : Gump
{
    private const int GumpW  = 480;
    private const int ColL   = 16;
    private const int ColR   = 248;
    private const int LblW   = 128;

    // Hue palette
    private const int HueHdr = 0x386; // gold — section headers
    private const int HueLbl = 0x47E; // teal — labels
    private const int HueVal = 0x44;  // grey-green — values
    private const int HuePos = 0x59;  // yellow — positive bonus
    private const int HueNeg = 0x20;  // red — negative / warning

    public ClusterFStatInspectGump(Mobile m) : base(50, 50)
    {
        var skills = CollectSkillMods(m);
        var bonusHps = CollectBonusHP(m);

        // Pre-compute all AOS sums so we can size the gump accurately.
        int strB  = Sum(m, a => a.BonusStr);
        int dexB  = Sum(m, a => a.BonusDex);
        int intB  = Sum(m, a => a.BonusInt);
        int hitsB = Sum(m, a => a.BonusHits);
        int stamB = Sum(m, a => a.BonusStam);
        int manaB = Sum(m, a => a.BonusMana);

        int lrc = Sum(m, a => a.LowerRegCost);
        int fc  = Sum(m, a => a.CastSpeed);
        int fcr = Sum(m, a => a.CastRecovery);
        int di  = Sum(m, a => a.WeaponDamage);
        int ssi = Sum(m, a => a.WeaponSpeed);
        int sdi = Sum(m, a => a.SpellDamage);
        int lmc = Sum(m, a => a.LowerManaCost);
        int hci = Sum(m, a => a.AttackChance);
        int dci = Sum(m, a => a.DefendChance);
        int rp  = Sum(m, a => a.ReflectPhysical);
        int ep  = Sum(m, a => a.EnhancePotions);
        int lk  = Sum(m, a => a.Luck);

        int hr = Sum(m, a => a.RegenHits);
        int sr = Sum(m, a => a.RegenStam);
        int mr = Sum(m, a => a.RegenMana);

        // Fixed-height blocks: title(30) + 6 sections + skill rows + close(36)
        const int SectionPad = 28; // header + gap
        const int RowH       = 16;
        int coreRows   = 4; // str/dex/int/pools
        int resistRows = 3; // phys+fire / cold+poison / energy
        int combatRows = 3; // hci+dci / di+ssi / rp+ep
        int magicRows  = 3; // lrc+lmc / fc+fcr / sdi+luck
        int regenRows  = 2; // hr+sr / mr
        int skillRows  = (skills.Count + 1) / 2;

        int gumpH = 30                                     // title
            + SectionPad + coreRows  * RowH + 8           // Core Stats
            + SectionPad + resistRows * RowH + 8           // Resistances
            + SectionPad + combatRows * RowH + 8           // Combat
            + SectionPad + magicRows  * RowH + 8           // Magic
            + SectionPad + regenRows  * RowH + 8           // Regeneration
            + (skills.Count > 0 ? SectionPad + Math.Max(1, skillRows) * RowH + 8 : 0)
            + 36;                                          // close button

        AddBackground(0, 0, GumpW, gumpH, 9200);

        int y = 10;
        CenteredLabel(y, HueHdr, $"Character Stats — {m.Name}");
        y += 24;

        // ── Core Stats ───────────────────────────────────────────────────────
        SectionHeader(ref y, "CORE STATS");

        TwoCol(ref y,
            $"Str:  {m.Str}",  strB  != 0 ? Signed(strB)  + " from items" : null,
            $"HP:   {m.Hits} / {m.HitsMax}", hitsB != 0 ? Signed(hitsB) + " bonus" : null);

        TwoCol(ref y,
            $"Dex:  {m.Dex}",  dexB  != 0 ? Signed(dexB)  + " from items" : null,
            $"Stam: {m.Stam} / {m.StamMax}", stamB != 0 ? Signed(stamB) + " bonus" : null);

        TwoCol(ref y,
            $"Int:  {m.Int}",  intB  != 0 ? Signed(intB)  + " from items" : null,
            $"Mana: {m.Mana} / {m.ManaMax}", manaB != 0 ? Signed(manaB) + " bonus" : null);

        // Mimic HP shown if equipped
        if (bonusHps.Count > 0)
        {
            foreach (var (label, cur, max) in bonusHps)
                TwoCol(ref y, $"{label}:", $"HP {cur} / {max}", null, null);
        }
        else
        {
            y += RowH; // blank row to keep spacing when no mimic
        }

        y += 8;

        // ── Resistances ──────────────────────────────────────────────────────
        SectionHeader(ref y, "RESISTANCES");

        TwoCol(ref y,
            $"Physical:  {m.PhysicalResistance}%", null,
            $"Fire:      {m.FireResistance}%",     null);

        TwoCol(ref y,
            $"Cold:      {m.ColdResistance}%",    null,
            $"Poison:    {m.PoisonResistance}%",  null);

        TwoCol(ref y,
            $"Energy:    {m.EnergyResistance}%",  null,
            null, null);

        y += 8;

        // ── Combat ───────────────────────────────────────────────────────────
        SectionHeader(ref y, "COMBAT");

        TwoCol(ref y,
            "Hit Chance Inc:",   hci > 0 ? $"+{hci}%" : "—",
            "Def Chance Inc:",   dci > 0 ? $"+{dci}%" : "—");

        TwoCol(ref y,
            "Damage Increase:",  di  > 0 ? $"+{di}%"  : "—",
            "Swing Speed Inc:",  ssi > 0 ? $"+{ssi}%" : "—");

        TwoCol(ref y,
            "Reflect Physical:", rp  > 0 ? $"{rp}%"   : "—",
            "Enhance Potions:",  ep  > 0 ? $"+{ep}%"  : "—");

        y += 8;

        // ── Magic ─────────────────────────────────────────────────────────────
        SectionHeader(ref y, "MAGIC");

        TwoCol(ref y,
            "Lower Reg Cost:",   lrc > 0 ? $"{lrc}%"  : "—",
            "Lower Mana Cost:",  lmc > 0 ? $"{lmc}%"  : "—");

        TwoCol(ref y,
            "Faster Casting:",   fc  != 0 ? $"{fc}"   : "—",
            "FC Recovery:",      fcr > 0 ? $"+{fcr}"  : "—");

        TwoCol(ref y,
            "Spell Dmg Inc:",    sdi > 0 ? $"+{sdi}%" : "—",
            "Luck:",             lk  > 0 ? $"{lk}"    : "—");

        y += 8;

        // ── Regeneration ─────────────────────────────────────────────────────
        SectionHeader(ref y, "REGENERATION");

        TwoCol(ref y,
            "HP Regen:",   hr > 0 ? $"+{hr}" : "—",
            "Stam Regen:", sr > 0 ? $"+{sr}" : "—");

        TwoCol(ref y,
            "Mana Regen:", mr > 0 ? $"+{mr}" : "—",
            null, null);

        y += 8;

        // ── Skill Bonuses ─────────────────────────────────────────────────────
        if (skills.Count > 0)
        {
            SectionHeader(ref y, "SKILL BONUSES (from items)");

            int col = 0;
            int rowStart = y;
            foreach (var (skill, bonus) in skills)
            {
                int cx = col == 0 ? ColL : ColR;
                AddLabel(cx,          rowStart, HueLbl, $"{skill}:");
                AddLabel(cx + LblW,   rowStart, HuePos, $"+{bonus:0.#}");
                col++;
                if (col >= 2) { col = 0; rowStart += RowH; }
            }
            y = rowStart + (col > 0 ? RowH : 0) + 8;
        }

        // Close
        AddButton(GumpW / 2 - 40, y, 4017, 4019, 0, GumpButtonType.Reply, 0);
        AddLabel(GumpW / 2 - 5,   y + 2, 0x455, "Close");

        return;

        // ── Local layout helpers ──────────────────────────────────────────────

        void CenteredLabel(int ly, int hue, string text)
        {
            // Approximate center: each char ≈ 8px wide
            int approxX = GumpW / 2 - text.Length * 4;
            AddLabel(approxX, ly, hue, text);
        }

        void SectionHeader(ref int ly, string title)
        {
            AddLabel(ColL, ly, HueHdr, title);
            ly += 20;
        }

        void TwoCol(ref int ly,
            string leftLabel, string leftSub,
            string rightLabel, string rightSub)
        {
            if (leftLabel  != null) AddLabel(ColL,          ly, HueLbl, leftLabel);
            if (leftSub    != null) AddLabel(ColL  + LblW,  ly, HueVal, leftSub);
            if (rightLabel != null) AddLabel(ColR,          ly, HueLbl, rightLabel);
            if (rightSub   != null) AddLabel(ColR  + LblW,  ly, HueVal, rightSub);
            ly += RowH;
        }
    }

    // ── AOS aggregation ───────────────────────────────────────────────────────

    private static int Sum(Mobile m, Func<AosAttributes, int> sel)
    {
        int total = 0;
        foreach (var item in m.Items)
        {
            AosAttributes attrs = item switch
            {
                BaseWeapon w   => w.Attributes,
                BaseArmor a    => a.Attributes,
                BaseJewel j    => j.Attributes,
                BaseClothing c => c.Attributes,
                BaseTalisman t => t.Attributes,
                PetMimic pm    => pm.Attributes,
                _              => null
            };
            if (attrs != null)
                total += sel(attrs);
        }
        return total;
    }

    // Collects active skill mods with positive values, sorted by skill name.
    private static List<(string skill, double bonus)> CollectSkillMods(Mobile m)
    {
        var dict = new Dictionary<string, double>();
        foreach (var mod in m.SkillMods)
        {
            if (mod.Value <= 0) continue;
            var key = mod.Skill.ToString();
            dict.TryGetValue(key, out var cur);
            dict[key] = cur + mod.Value;
        }

        var list = new List<(string, double)>(dict.Count);
        foreach (var kv in dict)
            list.Add((kv.Key, kv.Value));
        list.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.Ordinal));
        return list;
    }

    // Returns HP info for the mimic (and any future wearable with HP) if equipped.
    private static List<(string label, int cur, int max)> CollectBonusHP(Mobile m)
    {
        var list = new List<(string, int, int)>();
        foreach (var item in m.Items)
        {
            if (item is PetMimic pm)
                list.Add(("Pet Mimic", pm.CurrentHP, pm.MaxHP));
        }
        return list;
    }

    private static string Signed(int v) => v >= 0 ? $"+{v}" : $"{v}";
}
