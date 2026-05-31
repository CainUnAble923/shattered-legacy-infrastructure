using System;
using Server.Accounting;
using Server.Commands;
using Server.Items;
using Server.Mobiles;
using Server.Targeting;

namespace Server;

/// <summary>
/// Shattered Legacy — Miners' Compact admin and testing commands.
///
/// Commands (all require GameMaster+):
///   [CompactStanding [amount]        -- inspect or set mining standing on targeted player
///   [CompactVouchers [amount]        -- inspect or set mining vouchers on targeted player
///   [CompactRank                     -- show rank name for targeted player's standing
///   [CompactUnlockPickaxe            -- grant legacy.jacobs_pickaxe registry entry to target
///   [CompactGivePickaxe              -- give Jacob's Pickaxe (fresh) to target
///   [CompactGiveReinforcedPickaxe    -- give Jacob's Reinforced Pickaxe to target
///   [CompactExhaust                  -- exhaust the Jacob's Pickaxe held by target (for testing)
///   [CompactWipeLogbook              -- clear all ore discoveries from targeted player's logbook
///   [CompactSeedDiscoveries [state]  -- seed all 16 ore types into targeted player's logbook (Discovered or Reported)
///   [CompactOreInfo                  -- print extended ore CraftResource info to console
///</summary>
public static class ClusterFCompactAdminTools
{
    public static void Configure()
    {
        CommandSystem.Register("CompactStanding",              AccessLevel.GameMaster, CompactStanding_OnCommand);
        CommandSystem.Register("CompactVouchers",              AccessLevel.GameMaster, CompactVouchers_OnCommand);
        CommandSystem.Register("CompactRank",                  AccessLevel.GameMaster, CompactRank_OnCommand);
        CommandSystem.Register("CompactUnlockPickaxe",         AccessLevel.GameMaster, CompactUnlockPickaxe_OnCommand);
        CommandSystem.Register("CompactGivePickaxe",           AccessLevel.GameMaster, CompactGivePickaxe_OnCommand);
        CommandSystem.Register("CompactGiveReinforcedPickaxe", AccessLevel.GameMaster, CompactGiveReinforced_OnCommand);
        CommandSystem.Register("CompactExhaust",               AccessLevel.GameMaster, CompactExhaust_OnCommand);
        CommandSystem.Register("CompactClearActive",           AccessLevel.GameMaster, CompactClearActive_OnCommand);
        CommandSystem.Register("CompactOreInfo",               AccessLevel.GameMaster, CompactOreInfo_OnCommand);
        CommandSystem.Register("CompactGiveSatchel",           AccessLevel.GameMaster, CompactGiveSatchel_OnCommand);
        CommandSystem.Register("CompactGiveLogbook",           AccessLevel.GameMaster, CompactGiveLogbook_OnCommand);
        CommandSystem.Register("CompactWipeLogbook",           AccessLevel.GameMaster, CompactWipeLogbook_OnCommand);
        CommandSystem.Register("CompactSeedDiscoveries",       AccessLevel.GameMaster, CompactSeedDiscoveries_OnCommand);
        CommandSystem.Register("TestingReset",                 AccessLevel.GameMaster, TestingReset_OnCommand);
        CommandSystem.Register("TestingZeroSkills",            AccessLevel.GameMaster, TestingZeroSkills_OnCommand);
    }

    // ── [CompactStanding ──────────────────────────────────────────────────────

    [Usage("[CompactStanding [amount]")]
    [Description("Inspect or set Compact Standing (mining GuildReputation) on a targeted player.")]
    private static void CompactStanding_OnCommand(CommandEventArgs e)
    {
        int? setAmount = null;
        if (e.Length >= 1 && int.TryParse(e.GetString(0), out var amt))
            setAmount = amt;

        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Account is not IAccount acct) return;
            var data = ClusterFAccountPersistence.GetOrCreate(acct);

            if (setAmount.HasValue)
            {
                data.GuildReputation["mining"] = setAmount.Value;
                from.SendMessage($"Set {pm.Name}'s Compact Standing to {setAmount.Value:N0}.");
            }
            else
            {
                data.GuildReputation.TryGetValue("mining", out var standing);
                var rank     = MinersCompactLiaisonGump.GetRankName(standing);
                from.SendMessage($"{pm.Name}: Compact Standing={standing:N0}, Rank={rank}");
            }
        });
    }

    // ── [CompactVouchers ──────────────────────────────────────────────────────

    [Usage("[CompactVouchers [amount]")]
    [Description("Inspect or set Mining Vouchers (mining GuildCurrency) on a targeted player.")]
    private static void CompactVouchers_OnCommand(CommandEventArgs e)
    {
        int? setAmount = null;
        if (e.Length >= 1 && int.TryParse(e.GetString(0), out var amt))
            setAmount = amt;

        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Account is not IAccount acct) return;
            var data = ClusterFAccountPersistence.GetOrCreate(acct);

            if (setAmount.HasValue)
            {
                data.GuildCurrency["mining"] = setAmount.Value;
                from.SendMessage($"Set {pm.Name}'s Mining Vouchers to {setAmount.Value}.");
            }
            else
            {
                data.GuildCurrency.TryGetValue("mining", out var vouchers);
                from.SendMessage($"{pm.Name}: Mining Vouchers={vouchers}");
            }
        });
    }

    // ── [CompactRank ──────────────────────────────────────────────────────────

    [Usage("[CompactRank")]
    [Description("Show Compact rank for the targeted player's current standing.")]
    private static void CompactRank_OnCommand(CommandEventArgs e)
    {
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Account is not IAccount acct) return;
            var data     = ClusterFAccountPersistence.GetOrCreate(acct);
            data.GuildReputation.TryGetValue("mining", out var standing);
            var rank     = MinersCompactLiaisonGump.GetRankName(standing);
            from.SendMessage($"{pm.Name}: Standing={standing:N0}, Rank={rank}");
        });
    }

    // ── [CompactUnlockPickaxe ─────────────────────────────────────────────────

    [Usage("[CompactUnlockPickaxe")]
    [Description("Grant legacy.jacobs_pickaxe registry unlock to the targeted player.")]
    private static void CompactUnlockPickaxe_OnCommand(CommandEventArgs e)
    {
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Account is not IAccount acct) return;

            var result = ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_pickaxe", "admin");
            from.SendMessage(result
                ? $"Unlocked legacy.jacobs_pickaxe for {pm.Name}."
                : $"{pm.Name} already has legacy.jacobs_pickaxe unlocked.");
        });
    }

    // ── [CompactGivePickaxe ───────────────────────────────────────────────────

    [Usage("[CompactGivePickaxe")]
    [Description("Give Jacob's Pickaxe (fresh) to the targeted player.")]
    private static void CompactGivePickaxe_OnCommand(CommandEventArgs e)
    {
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Backpack == null) { from.SendMessage($"{pm.Name} has no backpack."); return; }

            var pickaxe = new JacobsPickaxe();
            pm.Backpack.DropItem(pickaxe);
            from.SendMessage($"Gave Jacob's Pickaxe to {pm.Name}.");
            pm.SendMessage(0x44, "An admin has given you Jacob's Pickaxe.");
        });
    }

    // ── [CompactGiveReinforcedPickaxe ─────────────────────────────────────────

    [Usage("[CompactGiveReinforcedPickaxe")]
    [Description("Give Jacob's Reinforced Pickaxe to the targeted player.")]
    private static void CompactGiveReinforced_OnCommand(CommandEventArgs e)
    {
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Backpack == null) { from.SendMessage($"{pm.Name} has no backpack."); return; }

            var pickaxe = new JacobsReinforcedPickaxe();
            pm.Backpack.DropItem(pickaxe);
            from.SendMessage($"Gave Jacob's Reinforced Pickaxe to {pm.Name}.");
            pm.SendMessage(0x44, "An admin has given you Jacob's Reinforced Pickaxe.");
        });
    }

    // ── [CompactExhaust ───────────────────────────────────────────────────────

    [Usage("[CompactExhaust")]
    [Description("Exhaust the Jacob's Pickaxe in the targeted player's pack (for testing durability behaviour).")]
    private static void CompactExhaust_OnCommand(CommandEventArgs e)
    {
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Backpack == null) { from.SendMessage($"{pm.Name} has no backpack."); return; }

            JacobsPickaxe? found = null;
            foreach (var item in pm.Backpack.Items)
            {
                if (item is JacobsPickaxe p && !p.Exhausted)
                {
                    found = p;
                    break;
                }
            }

            if (found == null)
            {
                from.SendMessage($"No non-exhausted Jacob's Pickaxe found in {pm.Name}'s pack.");
                return;
            }

            // Force-exhaust via public property (generated by SerializableField)
            found.Exhausted = true;
            found.Hue = 0x0415;

            if (pm.Account is IAccount acct)
                ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_pickaxe");

            from.SendMessage($"Exhausted {pm.Name}'s Jacob's Pickaxe (serial {found.Serial}).");
            pm.SendMessage(0x22, "Your Jacob's Pickaxe has been exhausted by an admin (testing).");
        });
    }

    // ── [CompactClearActive ───────────────────────────────────────────────────

    [Usage("[CompactClearActive")]
    [Description("Clears the active-copy flag for legacy.jacobs_pickaxe on a targeted player, allowing restoration.")]
    private static void CompactClearActive_OnCommand(CommandEventArgs e)
    {
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Account is not IAccount acct) return;

            ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_pickaxe");
            ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_reinforced_pickaxe");
            from.SendMessage($"Cleared active-copy flags for {pm.Name} (T1 and T2 pickaxe).");
        });
    }

    // ── [TestingReset ─────────────────────────────────────────────────────────

    [Usage("[TestingReset")]
    [Description("Wipes all guild, league, and Compact data for a targeted player — full testing reset.")]
    private static void TestingReset_OnCommand(CommandEventArgs e)
    {
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Account is not IAccount acct) return;

            var data = ClusterFAccountPersistence.GetOrCreate(acct);

            // Clear guild membership, reputation, and currency
            data.JoinedGuilds.Clear();
            data.GuildReputation.Clear();
            data.GuildCurrency.Clear();

            // Clear all flags and flag values (league.*, mining.*, etc.)
            data.Flags.Clear();
            data.FlagValues.Clear();

            // Clear active and completed work orders
            data.ActiveWorkOrders.Clear();
            data.CompletedWorkOrders.Clear();

            // Clear ore discovery records
            data.OreDiscoveries.Clear();

            // Clear restoration registry entries
            ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_pickaxe");
            ClusterFRestorationRegistry.Revoke(acct,          "legacy.jacobs_pickaxe");
            ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_reinforced_pickaxe");
            ClusterFRestorationRegistry.Revoke(acct,          "legacy.jacobs_reinforced_pickaxe");

            // Remove any pickaxes from the player's pack
            if (pm.Backpack != null)
            {
                var toDelete = new System.Collections.Generic.List<Item>();
                foreach (var item in pm.Backpack.Items)
                {
                    if (item is JacobsPickaxe || item is JacobsReinforcedPickaxe)
                        toDelete.Add(item);
                }
                foreach (var item in toDelete)
                    item.Delete();
            }

            from.SendMessage($"[TestingReset] {pm.Name}: guilds, reputation, currency, flags, registry entries, pickaxes, work orders, and ore discoveries cleared.");
            pm.SendMessage(0x22, "Your guild and League progress has been reset by an admin (testing).");
        });
    }

    // ── [TestingZeroSkills ────────────────────────────────────────────────────

    [Usage("[TestingZeroSkills")]
    [Description("Sets all skills to 0.0 on a targeted player for clean testing.")]
    private static void TestingZeroSkills_OnCommand(CommandEventArgs e)
    {
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }

            for (var i = 0; i < pm.Skills.Length; i++)
                pm.Skills[i].Base = 0.0;

            from.SendMessage($"[TestingZeroSkills] All {pm.Skills.Length} skills set to 0.0 on {pm.Name}.");
            pm.SendMessage(0x22, "Your skills have been reset to 0 by an admin (testing).");
        });
    }

    // ── [CompactGiveSatchel ───────────────────────────────────────────────────

    [Usage("[CompactGiveSatchel")]
    [Description("Give a Compact Ore Satchel to the targeted player.")]
    private static void CompactGiveSatchel_OnCommand(CommandEventArgs e)
    {
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Backpack == null) { from.SendMessage($"{pm.Name} has no backpack."); return; }

            pm.Backpack.DropItem(new Items.CompactOreSatchel());
            from.SendMessage($"Gave Compact Ore Satchel to {pm.Name}.");
            pm.SendMessage(0x44, "An admin has given you a Compact Ore Satchel.");
        });
    }

    // ── [CompactGiveLogbook ───────────────────────────────────────────────────

    [Usage("[CompactGiveLogbook")]
    [Description("Give a Prospector's Logbook to the targeted player.")]
    private static void CompactGiveLogbook_OnCommand(CommandEventArgs e)
    {
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Backpack == null) { from.SendMessage($"{pm.Name} has no backpack."); return; }

            pm.Backpack.DropItem(new Items.ProspectorsLogbook());
            from.SendMessage($"Gave Prospector's Logbook to {pm.Name}.");
            pm.SendMessage(0x44, "An admin has given you a Prospector's Logbook.");
        });
    }

    // ── [CompactWipeLogbook ───────────────────────────────────────────────────

    [Usage("[CompactWipeLogbook")]
    [Description("Clears all ore discovery records from a targeted player's logbook (for testing).")]
    private static void CompactWipeLogbook_OnCommand(CommandEventArgs e)
    {
        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Account is not IAccount acct) return;

            var data  = ClusterFAccountPersistence.GetOrCreate(acct);
            var count = data.OreDiscoveries.Count;
            data.OreDiscoveries.Clear();

            from.SendMessage($"[CompactWipeLogbook] Cleared {count} ore discovery record(s) from {pm.Name}'s logbook.");
            pm.SendMessage(0x22, "Your Prospector's Logbook has been wiped by an admin (testing).");
        });
    }

    // ── [CompactSeedDiscoveries ───────────────────────────────────────────────

    [Usage("[CompactSeedDiscoveries [Discovered|Reported]")]
    [Description("Seeds all 16 ore types into a targeted player's logbook. State defaults to Discovered (unreported). Use 'Reported' to skip straight to reported state.")]
    private static void CompactSeedDiscoveries_OnCommand(CommandEventArgs e)
    {
        var stateArg = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "discovered";
        var state    = stateArg == "reported" ? DiscoveryState.Reported : DiscoveryState.Discovered;

        e.Mobile.BeginTarget(-1, false, TargetFlags.None, (from, targeted) =>
        {
            if (targeted is not PlayerMobile pm) { from.SendMessage("Target a player."); return; }
            if (pm.Account is not IAccount acct) return;

            var data = ClusterFAccountPersistence.GetOrCreate(acct);

            // All 16 tracked ore keys in tier order
            var oreKeys = new[]
            {
                "DullCopper", "ShadowIron", "Copper", "Bronze", "Gold",
                "Agapite", "Verite", "Valorite",
                "Platinum", "Toxic", "Blaze", "Frost",
                "Obsidian", "Mythril", "Adamantium", "Celestial",
            };

            var seeded  = 0;
            var skipped = 0;

            foreach (var key in oreKeys)
            {
                if (data.OreDiscoveries.ContainsKey(key))
                {
                    skipped++;
                    continue;
                }

                var entry = new OreDiscoveryEntry(key)
                {
                    TotalMined = 1,
                    State      = state,
                };
                var seedLoc = entry.AddLocation("Testing", "Admin Seed", pm.Location);
                seedLoc.AmountMined = 1;

                data.OreDiscoveries[key] = entry;
                seeded++;
            }

            from.SendMessage($"[CompactSeedDiscoveries] Seeded {seeded} discoveries ({state}) on {pm.Name}. Skipped {skipped} already present.");
            pm.SendMessage(0x44, $"An admin has seeded {seeded} ore discoveries into your Prospector's Logbook ({state}).");
        });
    }

    // ── [CompactOreInfo ───────────────────────────────────────────────────────

    [Usage("[CompactOreInfo")]
    [Description("Print extended ore CraftResource enum values and hues to the console.")]
    private static void CompactOreInfo_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Extended ore info printed to server console.");

        var ores = new[]
        {
            CraftResource.Platinum, CraftResource.Toxic, CraftResource.Blaze,
            CraftResource.Frost,    CraftResource.Obsidian, CraftResource.Mythril,
            CraftResource.Adamantium, CraftResource.Celestial
        };

        Console.WriteLine("[CompactOreInfo] Extended ore resource table:");
        Console.WriteLine($"  {"Resource",-16} {"Value",5}  {"Hue",6}  {"Name",-16}  AttributeInfo");

        foreach (var ore in ores)
        {
            var info = CraftResources.GetInfo(ore);
            var ai   = info?.AttributeInfo;
            Console.WriteLine($"  {ore,-16} {(int)ore,5}  0x{info?.Hue ?? 0:X4}  {info?.Name ?? "(null)",-16}  " +
                              $"PhysRes={ai?.ArmorPhysicalResist} FireRes={ai?.ArmorFireResist} " +
                              $"ColdRes={ai?.ArmorColdResist} PoisRes={ai?.ArmorPoisonResist} " +
                              $"EngyRes={ai?.ArmorEnergyResist} Dur={ai?.ArmorDurability}");
        }
    }
}
