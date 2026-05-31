using System;
using System.Collections.Generic;
using Server;
using Server.Accounting;
using Server.Engines.Craft;
using Server.Engines.Harvest;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Harvest;

/// <summary>
/// Shattered Legacy — Lumberjacking extension.
///
/// Registers extended lumber veins (Ironwood through Starwood, Tiers 308–315) into the
/// existing Lumberjacking harvest definition, and adds the matching board types to the
/// Carpentry craft sub-resource list.
///
/// Also overrides Lumberjacking.Give() to implement the wood discovery gate:
///   - First chop of an extended log → record Discovered, substitute a regular Log,
///     show a guidance message pointing the player to the Foresters' Guildmaster.
///   - After the player reports at the Guildmaster (Discovered → Reported) the real
///     extended log is delivered normally.
///
/// Startup timing mirrors ClusterFMiningExtension:
///   Configure() → EventSink.WorldLoad  — extend veins (Lumberjacking.System available)
///   Configure() → EventSink.ServerStarted — extend Carpentry sub-resources (after
///                                           DefCarpentry.Initialize())
/// </summary>
public partial class Lumberjacking
{
    // Extended log types that are gated until the player reports the discovery.
    private static readonly Dictionary<Type, string> _extendedLogGateKeys = new()
    {
        { typeof(IronwoodLog),   "Ironwood"   },
        { typeof(GhostwoodLog),  "Ghostwood"  },
        { typeof(EmberbarkLog),  "Emberbark"  },
        { typeof(FrostbarkLog),  "Frostbark"  },
        { typeof(ShadowbarkLog), "Shadowbark" },
        { typeof(RunewoodLog),   "Runewood"   },
        { typeof(VoidwoodLog),   "Voidwood"   },
        { typeof(StarwoodLog),   "Starwood"   },
    };

    // Vanilla log types tracked for the logbook (not gated, auto-Reported on first find).
    // Regular Log is now included as the baseline wood — its TotalChopped is tracked
    // for the logbook even though it requires no reporting.
    private static readonly Dictionary<Type, string> _vanillaLogDiscoveryKeys = new()
    {
        { typeof(Log),          "RegularWood" },
        { typeof(OakLog),       "OakWood"     },
        { typeof(AshLog),       "AshWood"     },
        { typeof(YewLog),       "YewWood"     },
        { typeof(HeartwoodLog), "Heartwood"   },
        { typeof(BloodwoodLog), "Bloodwood"   },
        { typeof(FrostwoodLog), "Frostwood"   },
    };

    public override bool Give(Mobile m, Item item, bool placeAtFeet)
    {
        bool isExtended      = _extendedLogGateKeys.TryGetValue(item.GetType(), out var woodKey);
        bool wasAlreadyKnown = false;
        bool shouldGate      = false;

        if (isExtended && m is PlayerMobile pmGateCheck && !Server.Items.DevTestingCrystal.IsActive(pmGateCheck))
        {
            if (pmGateCheck.Account is IAccount acctGate)
            {
                var dataGate = ClusterFAccountPersistence.GetOrCreate(acctGate);
                wasAlreadyKnown = dataGate.WoodDiscoveries.ContainsKey(woodKey!);

                shouldGate = !dataGate.WoodDiscoveries.TryGetValue(woodKey!, out var gateEntry)
                          || gateEntry.State != DiscoveryState.Reported;
            }
        }

        // Log discovery before applying gate so the entry exists when the gate fires.
        TryLogWoodDiscovery(m, item);

        if (shouldGate)
        {
            var logAmount = item.Amount;
            item.Delete();
            item = new Log(logAmount);

            if (!wasAlreadyKnown && m is PlayerMobile pmMsg)
            {
                pmMsg.SendMessage(0x44,
                    "The bark crumbles before you can properly process it — you don't " +
                    "recognise this timber. Bring your discovery notes to the Foresters' " +
                    "Guildmaster to learn how to work it.");
            }
        }

        // Attempt to route into the Foresters' Lumber Satchel first.
        if (TryRouteToSatchel(m, item))
            return true;

        // Fall back to vanilla behaviour (backpack → feet).
        return base.Give(m, item, placeAtFeet);
    }

    // ── Lumber Satchel routing ────────────────────────────────────────────────

    private static bool TryRouteToSatchel(Mobile m, Item item)
    {
        if (m.Backpack == null) return false;
        if (!Items.ForestersLumberSatchel.Accepts(item)) return false;

        var satchel = m.Backpack.FindItemByType<Items.ForestersLumberSatchel>();
        if (satchel == null) return false;

        // TryDropItem respects the satchel's CheckHold — returns false if full.
        return satchel.TryDropItem(m, item, false);
    }

    private static void TryLogWoodDiscovery(Mobile m, Item item)
    {
        if (m is not PlayerMobile pm) return;
        if (pm.Account is not IAccount acct) return;

        bool isVanilla;
        string? woodKey;

        if (_extendedLogGateKeys.TryGetValue(item.GetType(), out woodKey))
            isVanilla = false;
        else if (_vanillaLogDiscoveryKeys.TryGetValue(item.GetType(), out woodKey))
            isVanilla = true;
        else
            return; // Board types not tracked (discovery happens at the log stage)

        var data = ClusterFAccountPersistence.GetOrCreate(acct);

        if (data.WoodDiscoveries.TryGetValue(woodKey, out var existing))
        {
            existing.TotalChopped += item.Amount;
            return;
        }

        // First time finding this wood type.
        var entry = new WoodDiscoveryEntry(woodKey) { TotalChopped = item.Amount };

        // Vanilla colored woods don't require reporting — mark Reported immediately.
        if (isVanilla)
            entry.State = DiscoveryState.Reported;

        data.WoodDiscoveries[woodKey] = entry;

        if (!isVanilla)
        {
            pm.SendMessage(0x59,
                $"You've found a piece of unusual timber — {woodKey}. " +
                "Check your Forester's Logbook to report your discovery.");
        }
    }
}

// ── Static configure/extend wiring ───────────────────────────────────────────

public static class ClusterFLumberjackingExtension
{
    public static void Configure()
    {
        EventSink.WorldLoad     += OnWorldLoad;
        EventSink.ServerStarted += OnServerStarted;
    }

    private static void OnWorldLoad()
    {
        ExtendLumberjackingVeins();
    }

    private static void OnServerStarted()
    {
        ExtendCarpentrySubResources();
        ExtendFletchingSubResources();
        // NOTE: Extended log/board equivalence pairs (IronwoodLog↔IronwoodBoard etc.) are
        // registered directly in CraftItem.cs m_TypesTable because that field is initonly
        // and cannot be patched via reflection in .NET 10.  See the "Shattered Legacy"
        // comment block in CraftItem.cs.
    }

    /// <summary>
    /// Appends extended lumber HarvestResources and HarvestVeins to Lumberjacking.System.Definitions[0].
    ///
    /// All extended woods require GM Lumberjacking (100.0 skill).
    /// Vein weights: 8 (Ironwood) down to 1 (Starwood), total 36 added to base 1000 = 1036.
    /// </summary>
    private static void ExtendLumberjackingVeins()
    {
        var lj = Lumberjacking.System;
        if (lj == null)
        {
            Console.WriteLine("[ClusterFLumberjackingExtension] WARNING: Lumberjacking.System is null.");
            return;
        }

        var def = lj.Definitions.Length > 0 ? lj.Definitions[0] : null;
        if (def == null)
        {
            Console.WriteLine("[ClusterFLumberjackingExtension] WARNING: No lumber harvest definition found.");
            return;
        }

        // Reuse frostwood message cliloc (1072546) — "You chop some frostwood logs."
        // Generic enough for any rare timber; exact text doesn't matter since the gate
        // intercepts extended logs before the player sees them until reported.
        var extResources = new HarvestResource[]
        {
            new(100.0, 60.0, 140.0, 1072546, typeof(IronwoodLog)),
            new(100.0, 62.0, 142.0, 1072546, typeof(GhostwoodLog)),
            new(100.0, 64.0, 144.0, 1072546, typeof(EmberbarkLog)),
            new(100.0, 64.0, 144.0, 1072546, typeof(FrostbarkLog)),
            new(100.0, 66.0, 146.0, 1072546, typeof(ShadowbarkLog)),
            new(100.0, 66.0, 146.0, 1072546, typeof(RunewoodLog)),
            new(100.0, 68.0, 148.0, 1072546, typeof(VoidwoodLog)),
            new(100.0, 68.0, 148.0, 1072546, typeof(StarwoodLog)),
        };

        var existingRes = def.Resources;
        var combined = new HarvestResource[existingRes.Length + extResources.Length];
        Array.Copy(existingRes, combined, existingRes.Length);
        Array.Copy(extResources, 0, combined, existingRes.Length, extResources.Length);
        def.Resources = combined;

        // Regular Log vein (index 0) is the fallback for all extended woods.
        var regularLog = existingRes[0];

        var extVeins = new HarvestVein[]
        {
            new(8, 0.5, combined[existingRes.Length + 0], regularLog), // Ironwood
            new(7, 0.5, combined[existingRes.Length + 1], regularLog), // Ghostwood
            new(6, 0.5, combined[existingRes.Length + 2], regularLog), // Emberbark
            new(5, 0.5, combined[existingRes.Length + 3], regularLog), // Frostbark
            new(4, 0.5, combined[existingRes.Length + 4], regularLog), // Shadowbark
            new(3, 0.5, combined[existingRes.Length + 5], regularLog), // Runewood
            new(2, 0.5, combined[existingRes.Length + 6], regularLog), // Voidwood
            new(1, 0.5, combined[existingRes.Length + 7], regularLog), // Starwood
        };

        var existingVeins = def.Veins;
        var combinedVeins = new HarvestVein[existingVeins.Length + extVeins.Length];
        Array.Copy(existingVeins, combinedVeins, existingVeins.Length);
        Array.Copy(extVeins, 0, combinedVeins, existingVeins.Length, extVeins.Length);
        def.Veins = combinedVeins;

        Console.WriteLine($"[ClusterFLumberjackingExtension] Extended lumber veins registered: {extVeins.Length} new wood types (Ironwood through Starwood).");
    }

    /// <summary>
    /// Appends extended board types to DefCarpentry.CraftSystem sub-resources.
    /// Must run after DefCarpentry.Initialize() — called from ServerStarted.
    /// </summary>
    private static void ExtendCarpentrySubResources()
    {
        var carpentry = DefCarpentry.CraftSystem;
        if (carpentry == null)
        {
            Console.WriteLine("[ClusterFLumberjackingExtension] WARNING: DefCarpentry.CraftSystem is null — extended sub-resources not registered.");
            return;
        }

        carpentry.AddSubRes(typeof(IronwoodBoard),   "Ironwood",   100.0, 1044036, 1044268);
        carpentry.AddSubRes(typeof(GhostwoodBoard),  "Ghostwood",  100.0, 1044036, 1044268);
        carpentry.AddSubRes(typeof(EmberbarkBoard),  "Emberbark",  100.0, 1044036, 1044268);
        carpentry.AddSubRes(typeof(FrostbarkBoard),  "Frostbark",  100.0, 1044036, 1044268);
        carpentry.AddSubRes(typeof(ShadowbarkBoard), "Shadowbark", 100.0, 1044036, 1044268);
        carpentry.AddSubRes(typeof(RunewoodBoard),   "Runewood",   100.0, 1044036, 1044268);
        carpentry.AddSubRes(typeof(VoidwoodBoard),   "Voidwood",   100.0, 1044036, 1044268);
        carpentry.AddSubRes(typeof(StarwoodBoard),   "Starwood",   100.0, 1044036, 1044268);

        Console.WriteLine("[ClusterFLumberjackingExtension] Carpentry sub-resources registered: Ironwood through Starwood.");
    }

    /// <summary>
    /// Appends extended log types to DefBowFletching sub-resources.
    /// Fixes the bug where Log subclasses (IronwoodLog etc.) are counted as
    /// valid ingredients by the craft system but aren't in the sub-resource
    /// picker, which breaks the default Log entry in the fletching menu.
    /// Must run after DefBowFletching.Initialize() — called from ServerStarted.
    /// </summary>
    private static void ExtendFletchingSubResources()
    {
        var fletching = DefBowFletching.CraftSystem;
        if (fletching == null)
        {
            Console.WriteLine("[ClusterFLumberjackingExtension] WARNING: DefBowFletching.CraftSystem is null — extended fletching sub-resources not registered.");
            return;
        }

        fletching.AddSubRes(typeof(IronwoodLog),   "Ironwood",   100.0, 1044041, 1072652);
        fletching.AddSubRes(typeof(GhostwoodLog),  "Ghostwood",  100.0, 1044041, 1072652);
        fletching.AddSubRes(typeof(EmberbarkLog),  "Emberbark",  100.0, 1044041, 1072652);
        fletching.AddSubRes(typeof(FrostbarkLog),  "Frostbark",  100.0, 1044041, 1072652);
        fletching.AddSubRes(typeof(ShadowbarkLog), "Shadowbark", 100.0, 1044041, 1072652);
        fletching.AddSubRes(typeof(RunewoodLog),   "Runewood",   100.0, 1044041, 1072652);
        fletching.AddSubRes(typeof(VoidwoodLog),   "Voidwood",   100.0, 1044041, 1072652);
        fletching.AddSubRes(typeof(StarwoodLog),   "Starwood",   100.0, 1044041, 1072652);

        Console.WriteLine("[ClusterFLumberjackingExtension] Fletching sub-resources registered: Ironwood through Starwood.");
    }
}
