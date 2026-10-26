// cc-P70, F-33: the mule's Options window (every module action in one place), the Trader's Ledger's sell window, and
// the guilds' schematic shop. Notes: shard-migration notes/cc-P70-mule-modules.md.
//
// One context entry on the mule, stock cliloc 3000127 "Options", opens the window: a new cliloc would need a player
// package update, which this task does not ship.

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Accounting;
using Server.ContextMenus;
using Server.Engines.Points;
using Server.Gumps;
using Server.Items;
using Server.Network;
using Server.Targeting;

namespace Server.Mobiles
{
    public sealed class MuleOptionsEntry : ContextMenuEntry
    {
        public const int Cliloc = 3000127; // "Options"

        private readonly PackMule _mule;

        public MuleOptionsEntry(PackMule mule) : base(Cliloc, 3) => _mule = mule;

        public override void OnClick(Mobile from, IEntity target)
        {
            if (from is PlayerMobile pm && pm == _mule.ControlMaster && !_mule.Deleted)
            {
                pm.SendGump(new MuleOptionsGump(pm, _mule));
            }
        }
    }

    public class MuleOptionsGump : Gump
    {
        private const int RemoveBase = 100;
        private const int TrackBase = 200;
        private const int FilterBase = 300;
        private const int BtnDig = 10;
        private const int BtnTrash = 11;
        private const int BtnSalvage = 12;
        private const int BtnSell = 13;
        private const int BtnBank = 14;

        private readonly PlayerMobile _pm;
        private readonly PackMule _mule;
        private readonly List<MuleModule> _modules;
        private readonly List<Item> _maps;

        public MuleOptionsGump(PlayerMobile pm, PackMule mule) : base(80, 80)
        {
            _pm = pm;
            _mule = mule;
            _modules = MuleModules.Installed(mule).ToList();
            _maps = MuleModules.TrackableMaps(mule).Take(6).ToList();

            AddPage(0);
            AddBackground(0, 0, 460, 520, 9270);
            AddAlphaRegion(6, 6, 448, 508);
            AddLabel(20, 14, 1154, $"{mule.Name}: modules {_modules.Count} / {mule.ModuleSlots}");

            var y = 40;

            for (var i = 0; i < _modules.Count; i++)
            {
                AddButton(20, y, 4017, 4019, RemoveBase + i);
                AddLabel(55, y + 2, 999, $"Remove {_modules[i].Name}");
                y += 24;
            }

            if (_modules.Count == 0)
            {
                AddLabel(20, y, 0x3B2, "Drop a module on the mule to fit it.");
                y += 24;
            }

            AddImageTiled(10, y + 4, 440, 2, 9304);
            y += 14;

            var survey = MuleModules.TierOf(mule, MuleModuleId.SurveySaddleRoll);

            if (survey >= 2)
            {
                AddLabel(20, y, 1154, "Track map (nearest by default):");
                y += 22;

                for (var i = 0; i < _maps.Count; i++)
                {
                    AddButton(20, y, 4005, 4007, TrackBase + i);
                    AddLabel(55, y + 2, 999, _maps[i].Name ?? _maps[i].GetType().Name);
                    y += 22;
                }

                if (survey >= 3)
                {
                    AddButton(20, y, 4005, 4007, BtnDig);
                    AddLabel(55, y + 2, 999, "Dig here for the tracked map");
                    y += 24;
                }
            }

            var bin = _modules.FirstOrDefault(m => m.ModuleId == MuleModuleId.SalvageBin);

            if (bin != null)
            {
                AddButton(20, y, 4005, 4007, BtnTrash);
                AddLabel(55, y + 2, 999, "Trash an item (Clean Up Britannia)");
                y += 24;

                if (bin.Tier >= 2)
                {
                    AddLabel(20, y, 1154, "Pick up near you:");
                    y += 20;

                    for (var i = 0; i < MuleModules.PickupCategories.Length; i++)
                    {
                        var c = MuleModules.PickupCategories[i];
                        AddButton(20 + i % 2 * 210, y, bin.Filter.Contains(c) ? 4018 : 4005, 4007, FilterBase + i);
                        AddLabel(55 + i % 2 * 210, y + 2, bin.Filter.Contains(c) ? 0x44 : 999, c);

                        if (i % 2 == 1)
                        {
                            y += 22;
                        }
                    }

                    y += 26;
                }

                if (bin.Tier >= 3)
                {
                    AddButton(20, y, 4005, 4007, BtnSalvage);
                    AddLabel(55, y + 2, 999, "Salvage a salvage bag in the mule's pack");
                    y += 24;
                }
            }

            var ledger = MuleModules.TierOf(mule, MuleModuleId.TradersLedger);

            if (ledger >= 1)
            {
                AddButton(20, y, 4005, 4007, BtnSell);
                AddLabel(55, y + 2, 999, "Sell cargo to a vendor");
                y += 24;
            }

            if (ledger >= 3)
            {
                AddButton(20, y, 4005, 4007, BtnBank);
                AddLabel(55, y + 2, 999, "Open your bank box");
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (_pm != _mule.ControlMaster || _mule.Deleted)
            {
                return;
            }

            var id = info.ButtonID;

            if (id >= RemoveBase && id < RemoveBase + _modules.Count)
            {
                MuleModules.Detach(_pm, _mule, _modules[id - RemoveBase]);
            }
            else if (id >= TrackBase && id < TrackBase + _maps.Count)
            {
                if (MuleModules.Track(_mule, _pm, _maps[id - TrackBase]) == null)
                {
                    _pm.SendMessage(0x22, "That map cannot be tracked.");
                }
            }
            else if (id >= FilterBase && id < FilterBase + MuleModules.PickupCategories.Length)
            {
                _modules.FirstOrDefault(m => m.ModuleId == MuleModuleId.SalvageBin)?.ToggleFilter(MuleModules.PickupCategories[id - FilterBase]);
            }
            else
            {
                switch (id)
                {
                    case BtnDig:
                        if (MuleModules.Dig(_mule, _pm) == null)
                        {
                            _pm.SendMessage(0x22, "Track a decoded treasure map first, and stand within 6 tiles of its spot.");
                        }

                        break;
                    case BtnTrash:
                        _pm.SendMessage("Target an item to trash.");
                        _pm.Target = new TrashTarget(_mule);
                        return;
                    case BtnSalvage:
                        Salvage();
                        break;
                    case BtnSell:
                        _pm.SendMessage("Target the vendor to sell your mule's cargo to.");
                        _pm.Target = new SellTarget(_mule);
                        return;
                    case BtnBank:
                        if (MuleModules.RefuseBank(_pm, _mule) is { } refusal)
                        {
                            _pm.SendMessage(0x22, refusal);
                        }
                        else
                        {
                            _pm.BankBox.Open();
                        }

                        return;
                    default:
                        return;
                }
            }

            _pm.SendGump(new MuleOptionsGump(_pm, _mule));
        }

        // Salvage Bin III: the salvage bags in the mule's pack are salvaged as the stock salvage bag does (ingots, cloth,
        // leather to the owner's pack; a forge near the owner as usual), and each item eligible for Clean Up Britannia
        // earns its points too.
        private void Salvage()
        {
            var bags = new List<SalvageBag>();

            if (_mule.Backpack != null)
            {
                foreach (var b in _mule.Backpack.FindItemsByType<SalvageBag>())
                {
                    bags.Add(b);
                }
            }

            if (bags.Count == 0)
            {
                _pm.SendMessage(0x22, "Put a salvage bag with something in it into the mule's pack first.");
                return;
            }

            var points = 0.0;

            foreach (var bag in bags)
            {
                foreach (var item in new List<Item>(bag.Items))
                {
                    if (ClusterFCustodianSystem.IsEligible(item, false))
                    {
                        points += ClusterFCustodianSystem.ComputePoints(item);
                    }
                }

                bag.SalvageAll(_pm);
            }

            if (points > 0)
            {
                ClusterFCustodianSystem.AwardPoints(_pm, points);
            }
        }

        private sealed class TrashTarget : Target
        {
            private readonly PackMule _mule;

            public TrashTarget(PackMule mule) : base(12, false, TargetFlags.None) => _mule = mule;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (from is PlayerMobile pm && targeted is Item item && !MuleModules.Trash(pm, _mule, item))
                {
                    pm.SendMessage(0x22, "That cannot be trashed (it must be in your pack or the mule's, and count for Clean Up Britannia).");
                }
            }
        }

        private sealed class SellTarget : Target
        {
            private readonly PackMule _mule;

            public SellTarget(PackMule mule) : base(10, false, TargetFlags.None) => _mule = mule;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (from is PlayerMobile pm && targeted is BaseVendor vendor)
                {
                    pm.SendGump(new MuleSellGump(pm, _mule, vendor));
                }
                else
                {
                    from.SendMessage(0x22, "That is not a vendor.");
                }
            }
        }
    }

    /// <summary>
    /// Trader's Ledger I-II: the mule's cargo sold to a vendor without a pinned change (P47 5.4, option A). Pinned's sell
    /// list reads only the seller's own backpack and its reply refuses items whose RootParent is not the seller
    /// (BaseVendor.cs:1039-1065, :435), so this window does the sale itself with the vendor's own sell info: the same
    /// filters, the same prices (II: 10% better), resellable items to its buy pack, a cap of lines per trip.
    /// </summary>
    public static class MuleSell
    {
        public static readonly int[] LinesPerTrip = { 100, 250, 250 };

        public static List<(Item Item, int Price, bool Resell)> Sellable(PackMule mule, BaseVendor vendor)
        {
            var list = new List<(Item, int, bool)>();
            var infos = vendor.GetSellInfo();

            if (mule.Backpack == null || infos == null)
            {
                return list;
            }

            foreach (var item in mule.Backpack.FindItems())
            {
                if (item is Container { Items.Count: > 0 } || !item.IsStandardLoot() || !item.Movable)
                {
                    continue;
                }

                foreach (var ssi in infos)
                {
                    if (item.InTypeList(ssi.Types) && ssi.IsSellable(item))
                    {
                        list.Add((item, ssi.GetSellPriceFor(item), ssi.IsResellable(item)));
                        break;
                    }
                }
            }

            return list;
        }

        public static int Bonus(PackMule mule) =>
            MuleModules.TierOf(mule, MuleModuleId.TradersLedger) >= 2 ? MuleModuleData.ValueAt(MuleModuleId.TradersLedger, 2) : 0;

        /// <summary>Sells the chosen lines; returns the gold paid to the owner's pack (or bank).</summary>
        public static int Sell(PlayerMobile pm, PackMule mule, BaseVendor vendor, IEnumerable<Item> chosen)
        {
            var tier = MuleModules.TierOf(mule, MuleModuleId.TradersLedger);

            if (tier == 0 || vendor.Deleted || !vendor.IsActiveBuyer || !pm.InRange(vendor, 10) ||
                !(mule.InRange(pm, 3) || pm.Mount == mule) || !vendor.CheckVendorAccess(pm))
            {
                pm.SendMessage(0x22, "Bring your mule and stand by the vendor.");
                return 0;
            }

            var sellable = new Dictionary<Item, (int Price, bool Resell)>();

            foreach (var (item, price, resell) in Sellable(mule, vendor))
            {
                sellable[item] = (price, resell);
            }

            var cap = LinesPerTrip[Math.Min(tier, LinesPerTrip.Length) - 1];
            var bonus = Bonus(mule);
            var gold = 0;
            var lines = 0;

            foreach (var item in chosen)
            {
                if (lines >= cap || !sellable.TryGetValue(item, out var row) || item.Deleted || !item.IsChildOf(mule.Backpack))
                {
                    continue;
                }

                gold += row.Price * item.Amount * (100 + bonus) / 100;
                lines++;

                if (row.Resell)
                {
                    vendor.BuyPack.DropItem(item);
                    item.SetLastMoved();
                }
                else
                {
                    item.Delete();
                }
            }

            while (gold > 0)
            {
                var pile = Math.Min(60000, gold);
                var coins = new Gold(pile);

                if (pm.Backpack?.TryDropItem(pm, coins, false) != true)
                {
                    pm.BankBox.DropItem(coins);
                }

                gold -= pile;
            }

            if (lines > 0)
            {
                vendor.SayTo(pm, true, "A pleasure doing business with you and your mule.");
            }

            return lines;
        }
    }

    public class MuleSellGump : Gump
    {
        private readonly PlayerMobile _pm;
        private readonly PackMule _mule;
        private readonly BaseVendor _vendor;
        private readonly List<(Item Item, int Price, bool Resell)> _rows;

        public MuleSellGump(PlayerMobile pm, PackMule mule, BaseVendor vendor) : base(80, 80)
        {
            _pm = pm;
            _mule = mule;
            _vendor = vendor;
            _rows = MuleSell.Sellable(mule, vendor).Take(20).ToList();

            AddPage(0);
            AddBackground(0, 0, 460, 120 + _rows.Count * 22, 9270);
            AddAlphaRegion(6, 6, 448, 108 + _rows.Count * 22);
            AddLabel(20, 14, 1154, $"Sell your mule's cargo to {vendor.Name}");

            var bonus = MuleSell.Bonus(mule);
            var y = 40;

            for (var i = 0; i < _rows.Count; i++)
            {
                var (item, price, _) = _rows[i];
                AddCheck(20, y, 210, 211, true, i);
                AddLabel(50, y, 999, $"{item.Amount} x {item.Name ?? item.GetType().Name}: {price * item.Amount * (100 + bonus) / 100} gold");
                y += 22;
            }

            if (_rows.Count == 0)
            {
                AddLabel(20, y, 0x22, "This vendor buys nothing your mule carries.");
            }

            AddButton(20, y + 20, 4005, 4007, 1);
            AddLabel(55, y + 22, 0x44, "Sell the checked items");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (info.ButtonID != 1)
            {
                return;
            }

            var chosen = new List<Item>();

            for (var i = 0; i < _rows.Count; i++)
            {
                if (info.IsSwitched(i))
                {
                    chosen.Add(_rows[i].Item);
                }
            }

            MuleSell.Sell(_pm, _mule, _vendor, chosen);
        }
    }

    // ------------------------------------------------------------------------------------------------------------------
    // Schematics (Part B): bought from the building guild's hall, they teach the recipe once (pinned's recipe store).
    // ------------------------------------------------------------------------------------------------------------------

    public static class MuleSchematics
    {
        // Guild rank index needed per module tier (ClusterFGuildSystem.GuildRankIndex). cc-P78 Part G (F-43, F-33): tier I at
        // 1, II at 2, III at 4 (Chase, the recipe chat 2026-10-11); IV and V, if module tiers ever go to 5, at 4 and 5.
        public static readonly int[] RankFor = { 1, 2, 4, 4, 5 };

        public static readonly int[] ScripCost = { 20, 50, 120, 250, 500 };
        public static readonly int[] VoucherCost = { 5, 10, 25, 50, 100 };
        public static readonly int[] CleanUpCost = { 2_000, 6_000, 15_000, 30_000, 60_000 }; // the Custodians have no scrip

        public static string RefuseBuy(PlayerMobile pm, CharacterGuildData guild, MuleModuleId id, int tier)
        {
            var info = MuleModuleData.Info(id);

            if (info == null || tier < 1 || tier > MuleModuleData.MaxTier)
            {
                return "There is no such schematic.";
            }

            // cc-P78 Part G: a schematic is learned once, by the account (ClusterFRecipeStore), like an ML recipe.
            if (ClusterFRecipeStore.Knows(pm, ClusterFRecipeStore.RecipeKey(MuleModuleCrafts.RecipeId(id, tier))))
            {
                return "Your account already knows that schematic.";
            }

            if (!guild.JoinedGuilds.Contains(info.GuildKey))
            {
                return $"Only members of the {info.GuildName} may buy its schematics.";
            }

            if (ClusterFGuildSystem.GuildRankIndex(info.GuildKey, guild) < RankFor[tier - 1])
            {
                return $"Tier {MuleData.GradeName(tier)} needs a higher rank in the {info.GuildName}.";
            }

            guild.GuildCurrency.TryGetValue("mining", out var vouchers);

            if (vouchers < VoucherCost[tier - 1])
            {
                return $"You need {VoucherCost[tier - 1]} Mining Vouchers (you have {vouchers}).";
            }

            if (info.GuildKey == "custodians")
            {
                var points = CleanUpBritanniaData.Instance.GetPoints(pm);
                return points < CleanUpCost[tier - 1] ? $"You need {CleanUpCost[tier - 1]:N0} Clean Up points (you have {points:N0})." : null;
            }

            if (info.GuildKey != "mining")
            {
                guild.GuildCurrency.TryGetValue(info.GuildKey, out var scrip);

                if (scrip < ScripCost[tier - 1])
                {
                    return $"You need {ScripCost[tier - 1]} {MuleUpgrades.CurrencyName(info.GuildKey)} (you have {scrip}).";
                }
            }
            else if (vouchers < VoucherCost[tier - 1] + ScripCost[tier - 1])
            {
                return $"You need {VoucherCost[tier - 1] + ScripCost[tier - 1]} Mining Vouchers (you have {vouchers}).";
            }

            return null;
        }

        /// <summary>Pays (guild currency or Clean Up points, plus Mining Vouchers) and teaches the recipe.</summary>
        public static bool Buy(PlayerMobile pm, CharacterGuildData guild, MuleModuleId id, int tier)
        {
            if (RefuseBuy(pm, guild, id, tier) is { } refusal)
            {
                pm.SendMessage(0x22, refusal);
                return false;
            }

            var info = MuleModuleData.Info(id);

            if (info.GuildKey == "custodians")
            {
                if (!CleanUpBritanniaData.Instance.DeductPoints(pm, CleanUpCost[tier - 1]))
                {
                    return false;
                }
            }
            else
            {
                guild.GuildCurrency[info.GuildKey] = guild.GuildCurrency[info.GuildKey] - ScripCost[tier - 1];
            }

            guild.GuildCurrency["mining"] = guild.GuildCurrency["mining"] - VoucherCost[tier - 1];
            ClusterFRecipeStore.Learn(pm, ClusterFRecipeStore.RecipeKey(MuleModuleCrafts.RecipeId(id, tier)));
            pm.SendMessage(0x44, $"You learn the {info.Name} {MuleData.GradeName(tier)} schematic ({MuleModuleCrafts.Row(id)?.SystemName}).");
            return true;
        }
    }

    public class GuildSchematicsGump : GuildStyledGump
    {
        private readonly PlayerMobile _pm;
        private readonly string _guildKey;
        private readonly Mobile _guildmaster;
        private readonly List<(MuleModuleId Id, int Tier)> _rows = new();

        public GuildSchematicsGump(PlayerMobile pm, string guildKey, Mobile guildmaster) : base(100, 80)
        {
            _pm = pm;
            _guildKey = guildKey;
            _guildmaster = guildmaster;

            var guild = Guild(pm);

            AddPage(0);
            AddBackground(0, 0, 480, 300, 9270);
            AddAlphaRegion(6, 6, 468, 288);
            AddLabel(20, 14, 1154, "Mule module schematics");

            var y = 44;

            foreach (var info in MuleModuleData.Modules.Where(m => m.GuildKey == guildKey))
            {
                for (var tier = 1; tier <= MuleModuleData.MaxTier; tier++)
                {
                    _rows.Add((info.Id, tier));
                    var refusal = MuleSchematics.RefuseBuy(pm, guild, info.Id, tier);
                    var cost = guildKey == "custodians"
                        ? $"{MuleSchematics.CleanUpCost[tier - 1]:N0} Clean Up points"
                        : $"{MuleSchematics.ScripCost[tier - 1]} {MuleUpgrades.CurrencyName(guildKey)}";

                    AddButton(20, y, 4005, 4007, _rows.Count);
                    AddLabel(55, y + 2, refusal == null ? 0x44 : 999, $"{info.Name} {MuleData.GradeName(tier)}: {cost} + {MuleSchematics.VoucherCost[tier - 1]} Mining Vouchers");
                    AddLabel(75, y + 22, 0x3B2, refusal ?? $"Teaches the recipe ({MuleModuleCrafts.Row(info.Id)?.SystemName}).");
                    y += 48;
                }
            }
        }

        private static CharacterGuildData Guild(PlayerMobile pm) =>
            pm.Account is IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct).GetOrCreateGuildData(pm.Serial) : new CharacterGuildData();

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var i = info.ButtonID - 1;

            if (i < 0 || i >= _rows.Count)
            {
                return;
            }

            if (!ClusterFGuildSystem.IsNearGuildmaster(_pm, _guildmaster))
            {
                _pm.SendMessage(0x22, "You must be at the guildmaster to buy a schematic.");
                return;
            }

            MuleSchematics.Buy(_pm, Guild(_pm), _rows[i].Id, _rows[i].Tier);
            _pm.SendGump(new GuildSchematicsGump(_pm, _guildKey, _guildmaster));
        }
    }
}
