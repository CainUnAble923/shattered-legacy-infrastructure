// cc-P861: [VBuild statify, staff freeze a placed addon into plain statics (Chase 11-08, P854 item 2; P798 suggested it,
// P812 built the decor capture beside it). After the idea of The_Ice_Phoenyx_Rising's "Addon Converter" (A2S,
// servuo.dev 2024; reference only, D:\UO\third-party\code\addon-converter), rebuilt here in the Visual Builder's way.
//
// Target any part of an addon. Each part becomes a plain Static where it stood, keeping its art, hue, name and light,
// and the addon goes. Unlike the A2S it follows, it:
//   - is one Visual Builder step: [VBuild undo takes the statics away and puts the addon back exactly as it was. The
//     addon is not deleted at once but parked on the internal map, so undo needs no record of how to rebuild it; it is
//     deleted for good when its step falls off the undo history (100 steps) or the session is forgotten;
//   - counts the statics as the session's own, so erase, hue, nudge and the capture work on them at once;
//   - refuses what it must not freeze: an addon in a house (a player's), an addon a seeder placed and keeps (the
//     seasonal decor's tracked serials, the winter decor, and the addon types our seeders place: a frozen copy would
//     sit under the one the seeder puts back), and an addon whose parts are not all on one map in the world.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Commands;
using Server.Items;
using Server.Engines.LeagueRewards;
using Server.Multis;
using Server.Targeting;

namespace Server;

public static class AddonStatify
{
    /// <summary>The addon types our seeders place and keep (re-placed on load when missing), never frozen.</summary>
    public static readonly Type[] SeededTypes =
    [
        typeof(AlembicHallAddon), typeof(MinersCompactTentAddon), typeof(SurveyArchivistTentAddon),
        typeof(RegistrarOfficeAddon), typeof(RegistrarOfficeOutsideAddon), typeof(RegistrarQuartersAddon),
        typeof(LeagueHallAddon)
    ];

    public static void Begin(Mobile from)
    {
        from.SendMessage(0x43, "Target any part of the addon to freeze into plain statics. [VBuild undo puts the addon back.");
        from.Target = new StatifyTarget();
    }

    /// <summary>The addon a target means: the addon itself or one of its parts.</summary>
    public static BaseAddon? AddonOf(object? targeted) => targeted switch
    {
        BaseAddon a => a,
        AddonComponent c => c.Addon,
        _ => null
    };

    /// <summary>Why <paramref name="addon"/> may not be frozen, or null.</summary>
    public static string? WhyNot(BaseAddon? addon)
    {
        if (addon == null || addon.Deleted || addon.Map == null || addon.Map == Map.Internal)
        {
            return "that is not an addon in the world";
        }

        if (addon.Components.Count == 0)
        {
            return "that addon has no parts";
        }

        if (addon.Components.Any(c => c.Deleted || c.Map != addon.Map))
        {
            return "that addon's parts are not all in the world with it";
        }

        if (BaseHouse.FindHouseAt(addon) != null || addon.Components.Any(c => BaseHouse.FindHouseAt(c) != null))
        {
            return "that addon is in a house; a player's addon is theirs";
        }

        if (addon is IWinterDecorItem || ClusterFSeasonalState.IsTracked(addon.Serial) ||
            addon.Components.Any(c => ClusterFSeasonalState.IsTracked(c.Serial)))
        {
            return "a seasonal decor placed that addon and takes it down itself";
        }

        if (SeededTypes.Any(t => t.IsInstanceOfType(addon)))
        {
            return $"a seeder places and keeps the {addon.GetType().Name}; a frozen copy would sit under the one it puts back";
        }

        return null;
    }

    /// <summary>
    /// Freezes <paramref name="addon"/>: a plain Static for each part (art, hue, name, light), then the addon parked on the
    /// internal map. One Visual Builder step. Returns the statics, or null with why.
    /// </summary>
    public static List<Static>? Statify(Mobile by, BaseAddon? addon, out string? refused)
    {
        refused = WhyNot(addon);
        if (refused != null)
        {
            return null;
        }

        var s = ClusterFVisualBuilder.Get(by);
        var step = new ClusterFBuilderStep("statify");
        step.Stashed.Add(new ClusterFBuilderStash(addon!.Serial, addon.Location, addon.Map!.MapIndex));
        s.Push(step);

        var map = addon.Map;
        var made = new List<Static>();
        foreach (var c in addon.Components.ToList())
        {
            // Each static is on the step before it is placed, so undo removes what a part-way failure left.
            var piece = new Static(c.ItemID) { Hue = c.Hue, Light = c.Light };
            if (!string.IsNullOrEmpty(c.Name))
            {
                piece.Name = c.Name;
            }

            step.Placed.Add(piece.Serial);
            s.Placed.Add(piece.Serial);
            piece.MoveToWorld(c.Location, map);
            made.Add(piece);
        }

        // Parked, not deleted: the addon takes its parts to the internal map, and undo brings it back as it was.
        addon.Internalize();
        return made;
    }

    /// <summary>Deletes for good the addons a step parked (its undo is no longer possible).</summary>
    public static void Discard(ClusterFBuilderStep step)
    {
        foreach (var p in step.Stashed)
        {
            if (World.FindItem(p.Serial) is BaseAddon { Deleted: false } a && a.Map == Map.Internal)
            {
                a.Delete();
            }
        }
    }

    /// <summary>Brings back the addons a step parked, where they stood.</summary>
    public static int Restore(ClusterFBuilderStep step)
    {
        var n = 0;
        foreach (var p in step.Stashed)
        {
            var map = p.MapIndex >= 0 && p.MapIndex < Map.Maps.Length ? Map.Maps[p.MapIndex] : null;
            if (map != null && World.FindItem(p.Serial) is BaseAddon { Deleted: false } a)
            {
                a.MoveToWorld(p.From, map);
                n++;
            }
        }

        return n;
    }

    private sealed class StatifyTarget : Target
    {
        public StatifyTarget() : base(-1, false, TargetFlags.None)
        {
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            var addon = AddonOf(targeted);
            var made = Statify(from, addon, out var refused);
            if (made == null)
            {
                from.SendMessage(0x22, $"Refused: {refused}.");
                return;
            }

            CommandLogging.WriteLine(from, $"{from.AccessLevel} {CommandLogging.Format(from)} froze {addon!.GetType().Name} {addon.Serial} into {made.Count} statics");
            from.SendMessage(0x43, $"Froze the {addon.GetType().Name} into {made.Count} plain statics. [VBuild undo puts the addon back.");
        }
    }
}
