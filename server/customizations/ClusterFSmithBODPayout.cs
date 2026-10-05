using System;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Engines.BulkOrders;
using Server.Gumps;
using Server.Items;
using Server.Misc;
using Server.Network;
using Server.Systems.FeatureFlags;
using System.Text.RegularExpressions;

namespace Server
{
    /// <summary>cc-P55 Part H: how a Society member's completed smith deed is paid.</summary>
    public enum SmithTurnInMode : byte
    {
        Bank    = 0, // the default: the deed's Seals go to the member's guild currency
        CashOut = 1, // gold at the counter and a chance at OSI's item reward
        Ask     = 2  // a two-button choice at each turn-in
    }

    public enum SmithTurnInResult
    {
        Paid,
        Asked,
        Refused
    }
}

namespace Server.Mobiles
{
    // -----------------------------------------------------------------------------
    // ClusterFSmithBODPayout - cc-P55 Part H (bug-list D76 / PT-11, F-25 items 3 and 6; Chase 2026-10-05).
    //
    // A Society of Smiths member's completed smith deed, turned in at the guildmaster, at any regular smith, or from the
    // Smithing Guild Book, is paid one of two ways, chosen by a per-character setting (CharacterGuildData.SmithTurnIn):
    //
    //   Bank (default): the guild payout as it stands (BlacksmithGuildmaster.ComputeGuildReward): Smithing Seals.
    //   Cash out: what a regular smith pays a non-member, at reduced rates so banking is the better deal: the deed's OSI
    //     gold (its own GetRewards) times CashOutGoldShare, its OSI fame, and OSI's item reward with CashOutItemChance.
    //   Ask: a two-button choice at each turn-in; the deed stays in the pack until a button is pressed.
    //
    // Either way the member earns the same standing and the same Blacksmithy skill checks. A non-member at a regular smith
    // is paid by pinned's own BaseVendor.OnDragDrop (OSI's rewards), unchanged. Every path deletes the deed it pays for,
    // and the payout re-checks the deed (still there, still the member's, complete, the large-order skill) at the moment
    // it pays, so a deed cannot be paid twice or slip a limit through the choice gump.
    //
    // Numbers (bank against cash out for three sample deeds) and the reasoning: shard-migration
    // notes/cc-P55-bug-batch-5.md, Part H.
    // -----------------------------------------------------------------------------

    public static class ClusterFSmithBODPayout
    {
        /// <summary>Cash out pays this share of the deed's OSI gold.</summary>
        public const double CashOutGoldShare = 0.5;

        /// <summary>Cash out gives the deed's OSI item reward with this chance (1 in 40).</summary>
        public const double CashOutItemChance = 0.025;

        /// <summary>The farthest a member may walk from the smith before answering the choice and still be paid there.</summary>
        public const int CounterRange = 12;

        public static SmithTurnInMode GetMode(Mobile m) =>
            ClusterFAccountPersistence.GetGuild(m)?.SmithTurnIn ?? SmithTurnInMode.Bank;

        public static void SetMode(Mobile m, SmithTurnInMode mode)
        {
            if (m.Account == null)
            {
                return;
            }

            ClusterFAccountPersistence.GetOrCreateGuild(m).SmithTurnIn = mode;
        }

        public static string ModeLabel(SmithTurnInMode mode) => mode switch
        {
            SmithTurnInMode.CashOut => "Always cash out",
            SmithTurnInMode.Ask     => "Ask each time",
            _                       => "Always bank"
        };

        public const string ToggleLabel = "Completed orders:";

        public const string ToggleHint = "Bank: Seals. Cash out: gold, a chance at an item.";

        /// <summary>Moves the setting to the next of bank, cash out, ask, and says what it is now. Both gumps use this.</summary>
        public static void CycleMode(PlayerMobile pm)
        {
            var next = GetMode(pm) switch
            {
                SmithTurnInMode.Bank    => SmithTurnInMode.CashOut,
                SmithTurnInMode.CashOut => SmithTurnInMode.Ask,
                _                       => SmithTurnInMode.Bank
            };

            SetMode(pm, next);
            pm.SendMessage(0x59, $"Completed Society orders: {ModeLabel(next).ToLowerInvariant()}.");
        }

        /// <summary>
        /// Starts a member's turn-in by the setting: pays at once (bank or cash out), or shows the choice and leaves the deed
        /// where it is. The caller has checked that the deed is a complete smith deed and the player a member. inHand: the deed was
        /// just dropped on the smith, so it is in the player's hand (a lifted item sits on the internal map, Mobile.cs:5173), not
        /// in the pack; with "ask" it bounces back to the pack and the choice checks the pack.
        /// </summary>
        public static SmithTurnInResult Begin(PlayerMobile pm, Item deed, Mobile? counter, bool inHand = false)
        {
            var mode = GetMode(pm);
            if (mode == SmithTurnInMode.Ask)
            {
                pm.CloseGump<SmithTurnInChoiceGump>();
                pm.SendGump(new SmithTurnInChoiceGump(pm, deed, counter));
                return SmithTurnInResult.Asked;
            }

            return Pay(pm, deed, mode == SmithTurnInMode.CashOut, counter, inHand) ? SmithTurnInResult.Paid : SmithTurnInResult.Refused;
        }

        /// <summary>The deed can still be paid: not deleted, in the member's backpack, complete, the large-order gate met, and the smith (if any) still near.</summary>
        public static bool CanPay(PlayerMobile pm, Item deed, Mobile? counter, out string? why, bool inHand = false)
        {
            why = null;

            if (deed == null || deed.Deleted)
            {
                why = "That order no longer exists.";
            }
            else if (!inHand && (pm.Backpack == null || !deed.IsChildOf(pm.Backpack)))
            {
                why = "That order must be in your backpack."; // a guild book in the pack counts; the bank box does not
            }
            else if (deed is not (SmallSmithBOD or LargeSmithBOD))
            {
                why = "That is not a smith's bulk order.";
            }
            else if (!(deed is SmallBOD { Complete: true } || deed is LargeBOD { Complete: true }))
            {
                why = "That order is not yet complete.";
            }
            else if (pm.Account is not IAccount || !ClusterFGuildSystem.IsJoined(pm, "smithing"))
            {
                why = "Only members of the Society of Smiths are paid this way.";
            }
            else if (deed is LargeSmithBOD && !BlacksmithGuildmaster.CanTurnInLarge(pm))
            {
                why = BlacksmithGuildmaster.LargeSkillRefusal;
            }
            else if (counter != null && (counter.Deleted || counter.Map != pm.Map || !pm.InRange(counter, CounterRange)))
            {
                why = "You must be at the smith to turn in that order.";
            }

            return why == null;
        }

        /// <summary>Pays the deed (bank or cash out), gives the standing and skill checks, and deletes it.</summary>
        public static bool Pay(PlayerMobile pm, Item deed, bool cashOut, Mobile? counter, bool inHand = false)
        {
            if (!CanPay(pm, deed, counter, out var why, inHand))
            {
                pm.SendMessage(0x22, why);
                return false;
            }

            var guild = ClusterFAccountPersistence.GetOrCreateGuild(pm);
            var (seals, standing, skillChecks) = BlacksmithGuildmaster.ComputeGuildReward(deed);
            guild.AddReputation("smithing", standing);

            if (cashOut)
            {
                var (gold, reward) = CashOut(pm, deed);
                pm.SendMessage(0x44,
                    $"Society of Smiths: +{standing} standing. Cashed out: {gold:N0} gold" +
                    (reward != null ? $" and {DescribeReward(reward)}." : "."));
            }
            else
            {
                guild.AddCurrency("smithing", seals);
                pm.SendMessage(0x44,
                    $"Society of Smiths: +{standing} standing, +{seals} Smithing Seal{(seals == 1 ? "" : "s")} banked.");
            }

            BlacksmithGuildmaster.RollTurnInSkillChecks(pm, deed, skillChecks);

            // At a regular smith, the stock turn-in's own effects: the order timer reset (SE) and the ML inspection pause.
            if (counter is BaseVendor vendor && ClusterFRegularSmithBODs.IsRegularSmith(vendor))
            {
                vendor.OnSuccessfulBulkOrderReceive(pm);
                if (Core.ML)
                {
                    pm.NextBODTurnInTime = Core.Now + TimeSpan.FromSeconds(10.0);
                }
            }

            pm.PlaySound(0x3D);
            deed.Delete();
            return true;
        }

        /// <summary>
        /// Cash out: the deed's own OSI rewards (BaseBOD.GetRewards: gold, fame and its reward group's item), the gold at
        /// CashOutGoldShare and the item kept with CashOutItemChance. Gold above 1,000 comes as a bank check, as the stock
        /// turn-in pays it (BaseVendor.cs:1135-1142).
        /// </summary>
        private static (int gold, Item? reward) CashOut(PlayerMobile pm, Item deed)
        {
            ((BaseBOD)deed).GetRewards(out var reward, out var osiGold, out var fame);

            var gold = (int)(osiGold * CashOutGoldShare);
            if (gold > 1000)
            {
                pm.AddToBackpack(new BankCheck(gold));
            }
            else if (gold > 0)
            {
                pm.AddToBackpack(new Gold(gold));
            }

            Titles.AwardFame(pm, fame, true);

            if (reward != null && Utility.RandomDouble() < CashOutItemChance)
            {
                pm.AddToBackpack(reward);
                return (gold, reward);
            }

            reward?.Delete();
            return (gold, null);
        }

        private static string DescribeReward(Item reward) =>
            reward.Name ?? "a " + Regex.Replace(reward.GetType().Name, "(?<=[a-z])(?=[A-Z])", " ").ToLowerInvariant();
    }

    // -----------------------------------------------------------------------------
    // SmithTurnInChoiceGump - "Ask each time": bank or cash out this one deed.
    // -----------------------------------------------------------------------------

    public class SmithTurnInChoiceGump : Gump
    {
        public const int BtnBank = 1;
        public const int BtnCashOut = 2;

        private const int W = 360;
        private const int H = 190;

        private readonly PlayerMobile _pm;
        private readonly Item _deed;
        private readonly Mobile? _counter;

        public SmithTurnInChoiceGump(PlayerMobile pm, Item deed, Mobile? counter) : base(140, 110)
        {
            _pm = pm;
            _deed = deed;
            _counter = counter;

            Closable   = true;
            Disposable = true;
            Resizable  = false;

            var (seals, standing, _) = BlacksmithGuildmaster.ComputeGuildReward(deed);

            AddPage(0);
            AddBackground(0, 0, W, H, 9270);
            AddAlphaRegion(8, 8, W - 16, H - 16);

            AddLabel(18, 16, 1153, "Society of Smiths - Completed Order");
            AddImageTiled(10, 38, W - 20, 2, 9304);
            AddLabel(18, 48, 999, $"Standing either way: +{standing}");

            AddButton(18, 74, 4005, 4007, BtnBank);
            AddLabel(54, 76, 1154, $"Bank: about {seals:N0} Smithing Seals");

            AddButton(18, 104, 4005, 4007, BtnCashOut);
            AddLabel(54, 106, 1154, "Cash out: gold and a chance at an item");

            AddLabel(18, 136, 0x3B2, "Closing keeps the order in your pack.");

            AddButton(W - 50, H - 34, 4017, 4019, 0);
            AddLabel(W - 90, H - 32, 999, "Close");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile is not PlayerMobile pm || pm != _pm)
            {
                return;
            }

            if (info.ButtonID is BtnBank or BtnCashOut)
            {
                ClusterFSmithBODPayout.Pay(pm, _deed, info.ButtonID == BtnCashOut, _counter);
            }
        }
    }

    // -----------------------------------------------------------------------------
    // Regular smiths - cc-P55 Part H item 1 (Chase 2026-10-05).
    //
    // Every stock vendor that issues or takes smith BODs: pinned Blacksmith (Mobiles/Vendors/NPC/Blacksmith.cs:76-127) and
    // Weaponsmith (Weaponsmith.cs:48-100), and through them pinned GeorgeHephaestus (Engines/ML Quests/Definitions/
    // NewHavenSkillTraining.cs:1821, a Blacksmith) and our GargoyleWeaponsmith (ClusterFGargoyleVendors.cs:280, a
    // Weaponsmith). The Society's BlacksmithGuildmaster is not a regular smith (ClusterFSmithBODSystem.cs).
    //
    // Asking one for an order ("Bulk Order Info", the stock entry's place and cliloc) opens a Small/Large choice instead of
    // the stock random roll. The stock timer still decides when an order is available, and choosing either starts it: the
    // choice calls the vendor's own CreateBulkOrder (which sets the timer, 1, 2 or 6 hours by skill, and rolls small or
    // large), keeps that deed when it is the kind chosen, and otherwise swaps it for a stock deed of the chosen kind. Opening
    // or closing the menu without choosing starts nothing. Large needs the stock skill, 70.1 Blacksmithy (Base): pinned
    // Blacksmith.cs:96 and Weaponsmith.cs:68 roll a large deed only at `theirSkill >= 70.1`. The deeds are stock deeds
    // (SmallSmithBOD.CreateRandomFor(m), new LargeSmithBOD()): the "Orders that still teach me" setting is the Society's only.
    // The 20% offer after buying from the smith (BaseVendor.cs:554-566) is unchanged.
    // -----------------------------------------------------------------------------

    public static class ClusterFRegularSmithBODs
    {
        /// <summary>The stock large-order skill: pinned Blacksmith.cs:96 and Weaponsmith.cs:68, against Base.</summary>
        public const double StockLargeSkill = 70.1;

        public static bool IsRegularSmith(Mobile m) => m is Blacksmith or Weaponsmith;

        public static bool MeetsLargeSkill(Mobile m) => m.Skills.Blacksmith.Base >= StockLargeSkill;

        /// <summary>Swaps the stock "Bulk Order Info" entry (cliloc 3006152) for the one that opens the choice.</summary>
        public static void ReplaceBulkOrderEntry(ref PooledRefList<ContextMenuEntry> list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Number == RegularSmithBulkOrderEntry.Cliloc && list[i] is not RegularSmithBulkOrderEntry)
                {
                    list[i] = new RegularSmithBulkOrderEntry();
                }
            }
        }

        /// <summary>
        /// The deed for the chosen kind, by the stock rules: the vendor's own CreateBulkOrder starts the timer; a deed of the
        /// other kind is swapped for a stock deed of the chosen kind. Null when nothing is available (timer running, no
        /// order fits, or Large below 70.1).
        /// </summary>
        public static Item? CreateChosen(BaseVendor vendor, PlayerMobile pm, bool large)
        {
            if (!vendor.SupportsBulkOrders(pm))
            {
                return null;
            }

            if (large && !MeetsLargeSkill(pm))
            {
                pm.SendMessage(0x22, LargeSkillMessage);
                return null;
            }

            if (vendor.GetNextBulkOrder(pm).TotalSeconds >= 1)
            {
                return null;
            }

            // Stock: the timer starts here, then a large roll or a small deed (null when no small order fits).
            var stock = vendor.CreateBulkOrder(pm, true);
            if (stock != null && stock is LargeSmithBOD == large)
            {
                return stock;
            }

            stock?.Delete();

            if (large)
            {
                return new LargeSmithBOD();
            }

            // A small choice: stock rolled large, so a stock small deed; or stock found no small order, so none.
            return stock == null ? null : SmallSmithBOD.CreateRandomFor(pm);
        }

        public const string LargeSkillMessage = "You need at least 70.1 Blacksmithy for a large order.";

        /// <summary>Creates the chosen deed and shows pinned's own accept gump, as the stock entry does.</summary>
        public static Item? Offer(BaseVendor vendor, PlayerMobile pm, bool large)
        {
            var bod = CreateChosen(vendor, pm, large);

            if (bod is LargeSmithBOD largeBod)
            {
                pm.SendGump(new LargeBODAcceptGump(largeBod));
            }
            else if (bod is SmallSmithBOD smallBod)
            {
                pm.SendGump(new SmallBODAcceptGump(smallBod));
            }
            else if (!large || MeetsLargeSkill(pm))
            {
                pm.SendMessage(0x22, "There is no order for you right now.");
            }

            return bod;
        }

        /// <summary>
        /// A smith deed handed to a regular smith by a Society member: paid the guild's way (bank, cash out or ask). Returns
        /// false (not handled) for anything else, so the stock turn-in pays it: non-members, other items, and a large deed
        /// from a member below 70.1 (the guild cannot take it; "any blacksmith will take this one").
        /// </summary>
        public static bool TryMemberTurnIn(BaseVendor vendor, Mobile from, Item dropped, out bool accepted)
        {
            accepted = false;

            if (dropped is not (SmallSmithBOD or LargeSmithBOD) || from is not PlayerMobile pm
                || pm.Account is not IAccount || !ClusterFGuildSystem.IsJoined(pm, "smithing")
                || dropped is LargeSmithBOD && !BlacksmithGuildmaster.CanTurnInLarge(pm))
            {
                return false;
            }

            if (Core.ML && pm.NextBODTurnInTime > Core.Now)
            {
                vendor.SayTo(from, 1079976); // You'll have to wait a few seconds while I inspect the last order.
                return true;
            }

            if (dropped is SmallBOD { Complete: false } or LargeBOD { Complete: false })
            {
                vendor.SayTo(from, 1045131); // You have not completed the order yet.
                return true;
            }

            switch (ClusterFSmithBODPayout.Begin(pm, dropped, vendor, inHand: true))
            {
                case SmithTurnInResult.Paid:
                    vendor.SayTo(from, 1045132); // Thank you so much!  Here is a reward for your effort.
                    accepted = true;
                    break;
                case SmithTurnInResult.Asked:
                    // The deed goes back to the pack until a button is pressed.
                    break;
            }

            return true;
        }
    }

    public class RegularSmithBulkOrderEntry : ContextMenuEntry
    {
        /// <summary>The stock entry's number, "Bulk Order Info" (pinned BaseVendor.cs:1367, 6152 sent as 3006152).</summary>
        public const int Cliloc = 3006152;

        public RegularSmithBulkOrderEntry() : base(Cliloc)
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (!ContentFeatureFlags.BulkOrders || target is not BaseVendor vendor || from is not PlayerMobile pm
                || !vendor.SupportsBulkOrders(from))
            {
                return;
            }

            var totalSeconds = vendor.GetNextBulkOrder(from).TotalSeconds;

            // The stock entry's timing and messages (BaseVendor.cs:1378-1418); the choice replaces its random roll.
            if (totalSeconds < 1)
            {
                from.SendLocalizedMessage(1049038); // You can get an order now.

                if (Core.AOS)
                {
                    pm.CloseGump<RegularSmithBulkOrderGump>();
                    pm.SendGump(new RegularSmithBulkOrderGump(pm, vendor));
                }

                return;
            }

            var oldSpeechHue = vendor.SpeechHue;
            vendor.SpeechHue = 0x3B2;

            if (Core.SE)
            {
                vendor.SayTo(from, 1072058, $"{Math.Ceiling(totalSeconds / 60):F0}"); // ~1_minutes~ minutes
            }
            else
            {
                vendor.SayTo(from, 1049039, $"{Math.Ceiling(totalSeconds / 3600):F0}"); // ~1_hours~ hours
            }

            vendor.SpeechHue = oldSpeechHue;
        }
    }

    public class RegularSmithBulkOrderGump : Gump
    {
        public const int BtnSmall = 1;
        public const int BtnLarge = 2;

        private const int W = 340;
        private const int H = 200;

        private readonly PlayerMobile _pm;
        private readonly BaseVendor _vendor;

        public RegularSmithBulkOrderGump(PlayerMobile pm, BaseVendor vendor) : base(120, 100)
        {
            _pm = pm;
            _vendor = vendor;

            Closable   = true;
            Disposable = true;
            Resizable  = false;

            AddPage(0);
            AddBackground(0, 0, W, H, 9270);
            AddAlphaRegion(8, 8, W - 16, H - 16);

            AddLabel(18, 16, 1153, "Bulk Order");
            AddImageTiled(10, 38, W - 20, 2, 9304);

            AddButton(18, 52, 4005, 4007, BtnSmall);
            AddLabel(54, 54, 1154, "Small bulk order");

            if (ClusterFRegularSmithBODs.MeetsLargeSkill(pm))
            {
                AddButton(18, 82, 4005, 4007, BtnLarge);
                AddLabel(54, 84, 1154, "Large bulk order");
            }
            else
            {
                AddLabel(18, 84, 0x3B2, "Large bulk order: needs 70.1 Blacksmithy");
                AddLabel(18, 104, 0x3B2, $"(yours is {pm.Skills.Blacksmith.Base:F1})");
            }

            AddLabel(18, 130, 0x3B2, "Either choice starts your wait for the next order.");

            AddButton(W - 50, H - 34, 4017, 4019, 0);
            AddLabel(W - 90, H - 32, 999, "Close");
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile is not PlayerMobile pm || pm != _pm || info.ButtonID is not (BtnSmall or BtnLarge))
            {
                return;
            }

            if (_vendor.Deleted || _vendor.Map != pm.Map || !pm.InRange(_vendor, ClusterFSmithBODPayout.CounterRange))
            {
                pm.SendMessage(0x22, "You are too far from the smith.");
                return;
            }

            // The Large button is not drawn below the skill; a client can still send it, so the rule is checked again.
            ClusterFRegularSmithBODs.Offer(_vendor, pm, info.ButtonID == BtnLarge);
        }
    }

    // The two stock smith vendors: the choice entry and the members' turn-in. Neither pinned class overrides these two
    // members, so partial declarations add them without a patch (as ClusterFSmithBODSystem.cs does for the guildmaster).

    public partial class Blacksmith
    {
        public override void AddCustomContextEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.AddCustomContextEntries(from, ref list);
            ClusterFRegularSmithBODs.ReplaceBulkOrderEntry(ref list);
        }

        public override bool OnDragDrop(Mobile from, Item dropped) =>
            ClusterFRegularSmithBODs.TryMemberTurnIn(this, from, dropped, out var accepted)
                ? accepted
                : base.OnDragDrop(from, dropped);
    }

    public partial class Weaponsmith
    {
        public override void AddCustomContextEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.AddCustomContextEntries(from, ref list);
            ClusterFRegularSmithBODs.ReplaceBulkOrderEntry(ref list);
        }

        public override bool OnDragDrop(Mobile from, Item dropped) =>
            ClusterFRegularSmithBODs.TryMemberTurnIn(this, from, dropped, out var accepted)
                ? accepted
                : base.OnDragDrop(from, dropped);
    }
}
