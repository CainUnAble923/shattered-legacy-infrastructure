// [TCFill: restock the caller's bank with pinned's Test Center fill and the Shattered Legacy Test Kit.
//
// FillBankbox only runs at character creation, so characters made before the Test Center was switched on get nothing.
// This gives them the same bank. Registered only when TestCenter.Enabled, so the command does not exist on live.
// Once per character per ten minutes; the cooldown is held in memory and resets when the server restarts.
//
// Registered in Initialize, not Configure: TestCenter.Enabled is set in TestCenter.Configure at the default priority,
// so from another Configure it would depend on sort order.

using System;
using System.Collections.Generic;

namespace Server.Misc;

public static class TestCenterFillCommand
{
    public const string Command = "TCFill";

    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(10.0);

    private static readonly Dictionary<Serial, DateTime> _lastUse = new();

    public static void Initialize()
    {
        if (TestCenter.Enabled)
        {
            CommandSystem.Register(Command, AccessLevel.Player, TCFill_OnCommand);
        }
    }

    [Usage("TCFill")]
    [Description("Test Center only. Restocks your bank box with the Test Center supplies and the Shattered Legacy Test Kit. Once every ten minutes.")]
    [ShardCommand(CommandCategory.Player)]
    public static void TCFill_OnCommand(CommandEventArgs e)
    {
        var from = e.Mobile;

        if (!TestCenter.Enabled)
        {
            from.SendMessage("The Test Center is not enabled on this shard.");
            return;
        }

        var now = Core.Now;

        if (_lastUse.TryGetValue(from.Serial, out var last) && now < last + Cooldown)
        {
            var minutes = (int)Math.Ceiling((last + Cooldown - now).TotalMinutes);
            from.SendMessage($"You can restock your bank again in {minutes} minute{(minutes == 1 ? "" : "s")}.");
            return;
        }

        _lastUse[from.Serial] = now;

        TestCenterKit.RefillBank(from);

        from.SendMessage("Your bank box has been restocked.");
    }
}
