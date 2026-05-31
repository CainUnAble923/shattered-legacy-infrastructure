using System;
using System.Collections.Generic;
using Server;
using Server.Accounting;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Harvest;

// ── Extended ore discovery gate ───────────────────────────────────────────────
//
// Extended ores (Platinum → Celestial) require a two-step unlock before the
// player can actually extract them:
//
//   Step 1: Mine a vein — the harvest system calls Mining.Give() with the ore.
//           TryLogDiscovery() fires first, recording a Discovered entry in the
//           player's OreDiscoveries.  The ore item is then intercepted here and
//           replaced with iron ore — the vein doesn't yield the rare metal yet
//           because the player doesn't know how to work it.
//           A one-time message explains what happened.
//
//   Step 2: Visit the Survey Archivist and report the discovery (Discovered →
//           Reported). From that point on, Mining.Give() delivers the actual
//           extended ore normally, and the matching work orders appear in the
//           Guild Contract Ledger.
//
// Vanilla ores (DullCopper → Valorite) are not gated — they have always been
// part of Britannia and miners know how to extract them without instruction.
//
// Testing bypass: if the player carries an active DevTestingCrystal the gate
// is skipped and extended ore is delivered directly.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Shattered Legacy — Compact Ore Satchel auto-routing hook + Prospector's Logbook discovery logger.
///
/// Extends Mining.Give (via partial class) to:
///   1. Log ore discoveries to the player's account (ProspectorsLogbook data)
///   2. Route newly mined ore into a Compact Ore Satchel if one is in the player's pack
///
/// Routing priority:
///   1. Compact Ore Satchel (if present, not full, and item is accepted)
///   2. Standard backpack (PlaceInBackpack / feet fallback)
///
/// Discovery logging:
///   - Iron is excluded (per design — only colored ore is tracked)
///   - First discovery sends a chat message to the player
///   - Subsequent mines silently increment TotalMined on the existing entry
///   - Discovery data is account-backed regardless of whether the player carries a logbook
/// </summary>
public partial class Mining
{
    // Extended ore types that are gated until the player reports the discovery.
    // Keyed by ore item type → canonical discovery key (matches _oreDiscoveryKeys).
    private static readonly Dictionary<Type, string> _extendedOreGateKeys = new()
    {
        { typeof(PlatinumOre),   "Platinum"   },
        { typeof(ToxicOre),      "Toxic"      },
        { typeof(BlazeOre),      "Blaze"      },
        { typeof(FrostOre),      "Frost"      },
        { typeof(ObsidianOre),   "Obsidian"   },
        { typeof(MythrilOre),    "Mythril"    },
        { typeof(AdamantiumOre), "Adamantium" },
        { typeof(CelestialOre),  "Celestial"  },
    };

    public override bool Give(Mobile m, Item item, bool placeAtFeet)
    {
        // ── Progressive ore availability gate ─────────────────────────────────
        // Check before TryLogDiscovery so we can detect first-time finds
        // (wasAlreadyKnown == false when the discovery is brand new).
        bool isExtended      = _extendedOreGateKeys.TryGetValue(item.GetType(), out var extKey);
        bool wasAlreadyKnown = false;
        bool shouldGate      = false;

        if (isExtended && m is PlayerMobile pmGateCheck && !Items.DevTestingCrystal.IsActive(pmGateCheck))
        {
            if (pmGateCheck.Account is IAccount acctGateCheck)
            {
                var dataGate = ClusterFAccountPersistence.GetOrCreate(acctGateCheck);
                wasAlreadyKnown = dataGate.OreDiscoveries.ContainsKey(extKey!);

                // Gate is active if the ore has not yet been Reported to the Archivist.
                shouldGate = !dataGate.OreDiscoveries.TryGetValue(extKey!, out var gateEntry)
                          || gateEntry.State != DiscoveryState.Reported;
            }
        }

        // Log ore discovery before routing so data is captured even if routing fails.
        TryLogDiscovery(m, item);

        // ── Progressive ore availability: apply gate after logging discovery ───
        // TryLogDiscovery has now created/updated the OreDiscoveryEntry (Discovered).
        // If the player hasn't reported this ore to the Archivist yet, substitute
        // iron ore so the vein remains "locked" until they complete the report step.
        if (shouldGate)
        {
            var ironAmount = item.Amount;
            item.Delete(); // release the ungiven extended ore's serial

            item = new IronOre(ironAmount);

            // Show the guidance message only on first-ever discovery of this type.
            if (!wasAlreadyKnown && m is PlayerMobile pmMsg)
            {
                pmMsg.SendMessage(0x44,
                    "The ore crumbles before you can properly extract it — you lack " +
                    "the technique for this vein. Visit the Survey Archivist with your " +
                    "discovery notes to learn how to work it.");
            }
        }

        // ── T4/T5 Deepdelver's Advantage: +1 ore per yield in Felucca ─────────
        // Applies to T4 (Deepdelver) and T5 (Worldbreaker).
        if (m.Map == Map.Felucca && m is PlayerMobile pmFelucca)
        {
            var tool = pmFelucca.FindItemOnLayer(Layer.TwoHanded);
            if (tool is JacobsDeepdelverPickaxe || tool is JacobsWorldbreakerPickaxe)
                item.Amount += 1;
        }

        // ── T5 Worldbreaker's Edge: +1 ore per yield everywhere ───────────────
        // Stacks with Deepdelver's Advantage in Felucca (+2 total).
        if (m is PlayerMobile pmWb)
        {
            var tool = pmWb.FindItemOnLayer(Layer.TwoHanded);
            if (tool is JacobsWorldbreakerPickaxe)
                item.Amount += 1;
        }

        // ── Mining depletion broadcast ─────────────────────────────────────────
        // After consuming from the harvest bank (done by the base harvest system
        // before Give() fires), check if this 8×8 chunk is now empty.  If so,
        // send a delta packet to nearby players so the World Map haze appears
        // immediately — without waiting for the next login sync.
        BroadcastDepletionIfEmptied(m);

        // Attempt to route into the Compact Ore Satchel first.
        if (TryRouteToSatchel(m, item))
            return true;

        // Fall back to vanilla behaviour (backpack → feet).
        return base.Give(m, item, placeAtFeet);
    }

    /// <summary>
    /// Checks the HarvestBank at the player's current 8×8 chunk.
    /// If the bank's Current count just reached zero, broadcasts a depletion
    /// delta to all players within range so the World Map haze updates live.
    /// </summary>
    private static void BroadcastDepletionIfEmptied(Mobile m)
    {
        if (m.Map == null || m.Map == Map.Internal) return;

        var def = Mining.System?.OreAndStone;
        if (def?.Banks == null) return;

        if (!def.Banks.TryGetValue(m.Map, out var banks)) return;

        // Banks are keyed by chunk coordinates (tile / BankWidth, tile / BankHeight).
        // BankWidth/BankHeight default to 8, matching ChunkSize.
        var key = new Point2D(m.X / ClusterFExplorationManager.ChunkSize,
                              m.Y / ClusterFExplorationManager.ChunkSize);

        if (banks.TryGetValue(key, out var bank) && bank.Current == 0)
            ClusterFDepletionSync.BroadcastDelta(m.Map, m.X, m.Y, true);
    }

    // ── Ore Satchel routing ───────────────────────────────────────────────────

    private static bool TryRouteToSatchel(Mobile m, Item item)
    {
        if (m.Backpack == null)
            return false;

        if (!CompactOreSatchel.Accepts(item))
            return false;

        var satchel = m.Backpack.FindItemByType<CompactOreSatchel>();
        if (satchel == null)
            return false;

        // TryDropItem respects the satchel's CheckHold — returns false if full.
        return satchel.TryDropItem(m, item, false);
    }

    // ── Prospector's Logbook discovery logging ────────────────────────────────

    // Ore type → canonical discovery key. Iron is deliberately excluded.
    private static readonly Dictionary<Type, string> _oreDiscoveryKeys = new()
    {
        { typeof(DullCopperOre),  "DullCopper"  },
        { typeof(ShadowIronOre),  "ShadowIron"  },
        { typeof(CopperOre),      "Copper"      },
        { typeof(BronzeOre),      "Bronze"      },
        { typeof(GoldOre),        "Gold"        },
        { typeof(AgapiteOre),     "Agapite"     },
        { typeof(VeriteOre),      "Verite"      },
        { typeof(ValoriteOre),    "Valorite"    },
        { typeof(PlatinumOre),    "Platinum"    },
        { typeof(ToxicOre),       "Toxic"       },
        { typeof(BlazeOre),       "Blaze"       },
        { typeof(FrostOre),       "Frost"       },
        { typeof(ObsidianOre),    "Obsidian"    },
        { typeof(MythrilOre),     "Mythril"     },
        { typeof(AdamantiumOre),  "Adamantium"  },
        { typeof(CelestialOre),   "Celestial"   },
    };

    private static void TryLogDiscovery(Mobile m, Item item)
    {
        if (m is not PlayerMobile pm) return;
        if (pm.Account is not IAccount acct) return;

        if (!_oreDiscoveryKeys.TryGetValue(item.GetType(), out var oreKey))
            return; // Not a tracked ore type (Iron excluded)

        // ── Achievement: any colored ore mined, total amount, Felucca bonus ──
        ClusterFAchievementSystem.NotifyMining(pm, item.Amount, m.Map == Map.Felucca);

        var data       = ClusterFAccountPersistence.GetOrCreate(acct);
        var facetName  = m.Map?.Name ?? "Unknown";
        var regionName = m.Region?.Name;
        if (string.IsNullOrWhiteSpace(regionName)) regionName = "Wilderness";

        if (!data.OreDiscoveries.TryGetValue(oreKey, out var entry))
        {
            // ── First discovery of this ore type ─────────────────────────────
            entry = new OreDiscoveryEntry(oreKey);
            data.OreDiscoveries[oreKey] = entry;

            var loc = entry.AddLocation(facetName, regionName, m.Location);
            loc.AmountMined += item.Amount;
            entry.TotalMined += item.Amount;

            var displayName = GetOreDisplayName(oreKey);
            pm.SendMessage(0x44,
                $"Discovery recorded: {displayName} ore in {regionName}, {facetName} " +
                $"({m.Location.X}, {m.Location.Y}). Check your Prospector's Logbook.");
            pm.SendMessage(0x44,
                "Report this discovery to Velara Thorne at the south mine to unlock work orders.");

            // ── T3+ Prospector's Insight: +3 Mining Vouchers on new discovery ──
            // Applies to T3 (Prospector), T4 (Deepdelver), and T5 (Worldbreaker).
            var equippedTool = pm.FindItemOnLayer(Layer.TwoHanded);
            if (equippedTool is JacobsProspectorPickaxe
                || equippedTool is JacobsDeepdelverPickaxe
                || equippedTool is JacobsWorldbreakerPickaxe)
            {
                data.AddCurrency("mining", 3);
                pm.SendMessage(0x44, "Prospector's Insight: +3 Mining Vouchers for this discovery.");
            }

            // ── Achievement: ore discovery count ─────────────────────────────
            ClusterFAchievementSystem.NotifyOreDiscoveryCount(pm, data.OreDiscoveries.Count);
        }
        else
        {
            // ── Known ore type — check if this is a new vein location ─────────
            entry.TotalMined += item.Amount;

            var nearby = entry.FindNearbyLocation(m.Location, 12);
            if (nearby != null)
            {
                // Same vein — silently accumulate.
                nearby.AmountMined += item.Amount;
            }
            else
            {
                // New vein of a previously known ore type — log and notify.
                var loc = entry.AddLocation(facetName, regionName, m.Location);
                loc.AmountMined += item.Amount;

                var displayName = GetOreDisplayName(oreKey);
                pm.SendMessage(0x44,
                    $"New {displayName} vein logged: {regionName}, {facetName} " +
                    $"({m.Location.X}, {m.Location.Y}).");
            }
        }
    }

    private static string GetOreDisplayName(string key) => key switch
    {
        "DullCopper" => "Dull Copper",
        "ShadowIron" => "Shadow Iron",
        _            => key,
    };
}
