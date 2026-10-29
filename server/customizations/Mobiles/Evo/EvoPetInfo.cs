// cc-P726 item 2 (Chase 10-28 12:22, "How do players check on their evo dog/cat? There is no info on them"): Pet Info, a
// window for the owner of an evo pet, in plain words: its name and kind, its stage (of how many), its experience toward
// the next stage as a bar and "X of Y", what it waits for besides experience (the owner's loops, the Bond Trial, a body
// to pick), its loyalty, and what makes it grow. Everything is read from the pet itself (EvoPet's line, stage, XP and
// gates, EvoPetSettings.XpToEnter; pinned's Loyalty), as the staff command's EvoStatus is. Pets on this shard have no
// hunger to show: feeding raises loyalty (pinned BaseCreature), and the window says so.
// Opened from the pet's context menu ("Pet Info", our cliloc ShardClilocs.PetInfo) and by [PetInfo (target the pet); both
// only for the pet's owner. Notes: shard-migration notes/cc-P726-isle-pet-reward.md.

#nullable enable

using System;
using Server.Collections;
using Server.Commands;
using Server.ContextMenus;
using Server.Gumps;
using Server.Network;
using Server.Targeting;
using Server.Tutorial;

namespace Server.Mobiles
{
    public partial class EvoPet
    {
        public override void AddCustomContextEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
        {
            base.AddCustomContextEntries(from, ref list);
            if (EvoPetInfo.IsOwner(from, this))
            {
                list.Add(new EvoPetInfo.PetInfoEntry());
            }
        }
    }

    public static class EvoPetInfo
    {
        public const string NotYoursLine = "Pet Info shows your own evo pet: click your pet.";

        public static void Configure() =>
            CommandSystem.Register("PetInfo", AccessLevel.Player, OnCommand);

        /// <summary>The pet's owner (its control master, the pet controlled), or staff.</summary>
        public static bool IsOwner(Mobile from, EvoPet pet) =>
            !pet.Deleted && (pet.Controlled && pet.ControlMaster == from || from.AccessLevel >= AccessLevel.GameMaster);

        /// <summary>Opens the window when <paramref name="from"/> owns <paramref name="target"/>. True when it did.</summary>
        public static bool Show(Mobile from, object? target)
        {
            if (target is not EvoPet pet || !IsOwner(from, pet))
            {
                from.SendMessage(0x3B2, NotYoursLine);
                return false;
            }

            from.CloseGump<EvoPetInfoGump>();
            from.SendGump(new EvoPetInfoGump(pet));
            return true;
        }

        [Usage("PetInfo")]
        [Description("Shows your evo pet's stage, growth and loyalty: target your pet.")]
        [ShardCommand(CommandCategory.Player)]
        private static void OnCommand(CommandEventArgs e)
        {
            e.Mobile.SendMessage("Click your pet to see how it is doing.");
            e.Mobile.Target = new PetTarget();
        }

        private sealed class PetTarget : Target
        {
            public PetTarget() : base(18, false, TargetFlags.None) { }

            protected override void OnTarget(Mobile from, object targeted) => Show(from, targeted);
        }

        public sealed class PetInfoEntry : ContextMenuEntry
        {
            public PetInfoEntry() : base(ShardClilocs.PetInfo, 18) { }

            public override void OnClick(Mobile from, IEntity target) => Show(from, target);
        }

        /// <summary>The window's lines, in order (the gump draws them; tests read them).</summary>
        public static string[] Lines(EvoPet pet)
        {
            var line = pet.Line;
            var stage = pet.CurrentStage;
            var top = line?.Stages.Length ?? 0;
            var next = line?.NextEnabled(pet.Stage);
            var kind = line == null ? "evo pet" : $"{line.Name} ({line.Role})";
            var growth = next == null
                ? "Growth: at its top stage for now."
                : $"Growth: {pet.Xp:N0} of {EvoPetSettings.XpToEnter(next.Number):N0} experience to become a {next.Name}.";
            var waits = next != null && pet.BlockFor(next) is { } block ? $"It also waits for: {block}." : null;
            return
            [
                $"Kind: {kind}",
                $"Stage: {stage?.Name ?? "unknown"} (stage {pet.Stage} of {top})",
                growth,
                waits ?? "",
                $"Loyalty: {pet.Loyalty}% (feed it now and then to keep it high; pets here never go hungry otherwise)",
                "How it grows: it earns experience whenever it helps defeat a creature, more for stronger foes. Keep it beside you when you fight."
            ];
        }

        /// <summary>How full the growth bar is, 0 to 1 (1 at its top stage).</summary>
        public static double Progress(EvoPet pet)
        {
            var next = pet.Line?.NextEnabled(pet.Stage);
            if (next == null)
            {
                return 1.0;
            }

            var from = EvoPetSettings.XpToEnter(pet.Stage);
            var to = EvoPetSettings.XpToEnter(next.Number);
            return to <= from ? 1.0 : Math.Clamp((pet.Xp - from) / (double)(to - from), 0.0, 1.0);
        }
    }

    public sealed class EvoPetInfoGump : Gump
    {
        private const int Width = 460;
        private const int BarWidth = 300;

        public EvoPet Pet { get; }

        public EvoPetInfoGump(EvoPet pet) : base(100, 100)
        {
            Pet = pet;
            var lines = EvoPetInfo.Lines(pet);
            TutorialStyle.Panel(this, Width, 300);
            AddLabel(24, 18, TutorialStyle.HeadingHue, $"Pet Info: {pet.Name}");
            var y = 50;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0)
                {
                    continue;
                }

                var height = lines[i].Length > 60 ? 36 : 20;
                AddHtml(24, y, Width - 48, height, TutorialStyle.Html(lines[i]), false, false);
                y += height + 2;
                if (i == 2)
                {
                    // the growth bar under its line
                    AddImageTiled(24, y, BarWidth, 11, 0x0805);
                    AddImageTiled(24, y, (int)(BarWidth * EvoPetInfo.Progress(pet)), 11, 0x0806);
                    y += 18;
                }
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info) { }
    }
}
