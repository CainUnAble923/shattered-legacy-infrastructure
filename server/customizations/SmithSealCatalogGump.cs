using System;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server;

/// <summary>
/// Society of Smiths — Seal Catalog
///
/// Allows guild members to spend Smithing Seals on tools, runic hammers,
/// ancient smithy hammers, and power scrolls.
///
/// Opened from: SmithGuildmasterGump (member view) and SmithGuildBook.
/// </summary>
public class SmithSealCatalogGump : Gump
{
    // ── Layout ────────────────────────────────────────────────────────────────

    private const int W       = 500;
    private const int H       = 440;
    private const int SideW   = 155; // sidebar width
    private const int RowH    = 22;
    private const int ItemsY  = 40;  // y where item rows start

    // Right panel column offsets (relative to right panel start = SideW + 10)
    private const int PanelX  = SideW + 10;
    private const int BuyX    = PanelX;
    private const int NameX   = PanelX + 26;
    private const int CostX   = W - 100;

    // ── Catalog definition ────────────────────────────────────────────────────

    public enum Cat { Tools, AncientHammers, RunicVanilla, RunicPostVal, PowerScrolls }

    private static readonly string[] CatLabels =
    {
        "Tools & Supplies",
        "Smithy Hammers",
        "Runic Hammers",
        "Post-Valorite",
        "Power Scrolls",
    };

    private record CatalogEntry(string Name, int Cost, Func<Item?> Create, bool ComingSoon = false);

    private static readonly CatalogEntry[][] Catalog =
    {
        // ── Tools & Supplies ──────────────────────────────────────────────
        new CatalogEntry[]
        {
            new("Sturdy Shovel",           50,  () => new SturdyShovel()),
            new("Sturdy Pickaxe",          50,  () => new SturdyPickaxe()),
            new("Gargoyle's Pickaxe",     100,  () => new GargoylesPickaxe()),
            new("Prospector's Tool",      200,  () => new ProspectorsTool()),
            new("Mining Gloves +3",       300,  () => new StuddedGlovesOfMining(3)),
            new("Mining Gloves +5",       600,  () => new RingmailGlovesOfMining(5)),
            new("Powder of Temperament",  150,  () => new PowderOfTemperament()),
            new("Smith Guild Salvage Bag", 250,  () => new SmithGuildSalvageBag()),
        },

        // ── Ancient Smithy Hammers ────────────────────────────────────────
        new CatalogEntry[]
        {
            new("Ancient Smithy Hammer (+10 uses)",   100, () => new AncientSmithyHammer(10)),
            new("Ancient Smithy Hammer (+15 uses)",   200, () => new AncientSmithyHammer(15)),
            new("Ancient Smithy Hammer (+30 uses)",   500, () => new AncientSmithyHammer(30)),
            new("Ancient Smithy Hammer (+60 uses)", 1_000, () => new AncientSmithyHammer(60)),
        },

        // ── Runic Hammers (Vanilla) ───────────────────────────────────────
        new CatalogEntry[]
        {
            new("Dull Copper Runic Hammer",    200, () => new RunicHammer(CraftResource.DullCopper, 50)),
            new("Shadow Iron Runic Hammer",    350, () => new RunicHammer(CraftResource.ShadowIron, 45)),
            new("Copper Runic Hammer",         550, () => new RunicHammer(CraftResource.Copper,     40)),
            new("Bronze Runic Hammer",         800, () => new RunicHammer(CraftResource.Bronze,     35)),
            new("Gold Runic Hammer",         1_200, () => new RunicHammer(CraftResource.Gold,       30)),
            new("Agapite Runic Hammer",      1_800, () => new RunicHammer(CraftResource.Agapite,    25)),
            new("Verite Runic Hammer",       3_000, () => new RunicHammer(CraftResource.Verite,     20)),
            new("Valorite Runic Hammer",     5_000, () => new RunicHammer(CraftResource.Valorite,   15)),
        },

        // ── Runic Hammers (Post-Valorite) — placeholders ──────────────────
        new CatalogEntry[]
        {
            new("Platinum Runic Hammer",     7_500, () => null, ComingSoon: true),
            new("Toxic Runic Hammer",       10_000, () => null, ComingSoon: true),
            new("Blaze Runic Hammer",       13_000, () => null, ComingSoon: true),
            new("Frost Runic Hammer",       17_000, () => null, ComingSoon: true),
            new("Obsidian Runic Hammer",    22_000, () => null, ComingSoon: true),
            new("Mythril Runic Hammer",     28_000, () => null, ComingSoon: true),
            new("Adamantium Runic Hammer",  36_000, () => null, ComingSoon: true),
            new("Celestial Runic Hammer",   50_000, () => null, ComingSoon: true),
        },

        // ── Power Scrolls ─────────────────────────────────────────────────
        // Each scroll raises the Blacksmithing skill cap by its listed amount.
        // PS 305 = cap raised from 300 → 305; buy PS 310 next for the next step, etc.
        new CatalogEntry[]
        {
            new("Blacksmithing Power Scroll +5",     750, () => new PowerScroll(SkillName.Blacksmith, 305.0)),
            new("Blacksmithing Power Scroll +10",  2_000, () => new PowerScroll(SkillName.Blacksmith, 310.0)),
            new("Blacksmithing Power Scroll +15",  4_500, () => new PowerScroll(SkillName.Blacksmith, 315.0)),
            new("Blacksmithing Power Scroll +20", 10_000, () => new PowerScroll(SkillName.Blacksmith, 320.0)),
        },
    };

    // ── Button IDs ────────────────────────────────────────────────────────────
    // 0         = close / no-op
    // 1–5       = select category (Cat enum value + 1)
    // 100–199   = buy item at row index (100 + index)

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly PlayerMobile _pm;
    private readonly Cat          _cat;

    // ── Constructor ───────────────────────────────────────────────────────────

    public SmithSealCatalogGump(PlayerMobile pm, Cat cat = Cat.Tools) : base(60, 60)
    {
        _pm  = pm;
        _cat = cat;

        Closable   = true;
        Disposable = true;

        var acct  = pm.Account as IAccount;
        var data  = acct != null ? ClusterFAccountPersistence.GetOrCreate(acct) : null;
        var seals = data?.GetCurrency("smithing") ?? 0;

        AddBackground(0, 0, W, H, 9270);
        AddAlphaRegion(8, 8, W - 16, H - 16);

        // ── Header ────────────────────────────────────────────────────────────
        AddLabel(W / 2 - 95, 12, 1153, "Society of Smiths — Seal Catalog");
        AddLabel(16, 12, 999, "Balance:");
        AddLabel(72, 12, 68,  $"{seals:N0} seals");
        AddImageTiled(10, 30, W - 20, 2, 9304);

        // ── Category sidebar ──────────────────────────────────────────────────
        var sy = ItemsY;
        for (var i = 0; i < CatLabels.Length; i++)
        {
            var selected = (int)_cat == i;
            AddButton(12, sy, selected ? 4006 : 4005, selected ? 4006 : 4007, i + 1);
            AddLabel(34, sy + 2, selected ? 1153 : 999, CatLabels[i]);
            sy += 26;
        }

        AddImageTiled(SideW, 34, 2, H - 44, 9304);

        // ── Item rows ─────────────────────────────────────────────────────────
        var entries = Catalog[(int)_cat];

        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            var y     = ItemsY + i * RowH;

            if (entry.ComingSoon)
            {
                AddLabel(BuyX,  y + 2, 0x3DE, "—");
                AddLabel(NameX, y + 2, 0x3DE, entry.Name);
                AddLabel(CostX, y + 2, 0x3DE, $"{entry.Cost:N0}  *");
            }
            else
            {
                var canAfford = seals >= entry.Cost;
                // Always show the buy button; OnResponse rejects if insufficient seals
                AddButton(BuyX, y, 4005, 4007, 100 + i);
                AddLabel(NameX, y + 2, canAfford ? 999 : 0x3DE, entry.Name);
                AddLabel(CostX, y + 2, canAfford ? 68  : 0x3DE, $"{entry.Cost:N0}");
            }
        }

        // ── Footer ────────────────────────────────────────────────────────────
        AddImageTiled(10, H - 34, W - 20, 2, 9304);
        if (_cat == Cat.RunicPostVal)
            AddLabel(PanelX, H - 28, 0x3DE, "* Not yet available — reserved for future post-valorite metals.");
        AddButton(W - 54, H - 26, 4023, 4025, 0);
        AddLabel(W - 110, H - 23, 999, "Close");
    }

    // ── Response ──────────────────────────────────────────────────────────────

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (_pm.Deleted || !_pm.Alive) return;

        var buttonID = info.ButtonID;

        // Category select
        if (buttonID is >= 1 and <= 5)
        {
            _pm.SendGump(new SmithSealCatalogGump(_pm, (Cat)(buttonID - 1)));
            return;
        }

        // Buy
        if (buttonID is >= 100 and < 200)
        {
            var idx     = buttonID - 100;
            var entries = Catalog[(int)_cat];

            if (idx < 0 || idx >= entries.Length) return;

            var entry = entries[idx];

            if (entry.ComingSoon)
            {
                _pm.SendMessage(0x22, $"{entry.Name} is not yet available.");
                _pm.SendGump(new SmithSealCatalogGump(_pm, _cat));
                return;
            }

            var acct = _pm.Account as IAccount;
            if (acct == null) return;

            var data = ClusterFAccountPersistence.GetOrCreate(acct);

            if (!data.SpendCurrency("smithing", entry.Cost))
            {
                _pm.SendMessage(0x22, $"You need {entry.Cost:N0} Smithing Seals for that (you have {data.GetCurrency("smithing"):N0}).");
                _pm.SendGump(new SmithSealCatalogGump(_pm, _cat));
                return;
            }

            var item = entry.Create();
            if (item == null)
            {
                data.AddCurrency("smithing", entry.Cost); // refund
                _pm.SendMessage(0x22, "That item is not yet available.");
                _pm.SendGump(new SmithSealCatalogGump(_pm, _cat));
                return;
            }

            _pm.AddToBackpack(item);
            _pm.SendMessage(0x44, $"Purchased: {entry.Name}  (-{entry.Cost:N0} seals).");
            _pm.PlaySound(0x57);

            // Reopen same category with refreshed balance
            _pm.SendGump(new SmithSealCatalogGump(_pm, _cat));
        }
    }
}
