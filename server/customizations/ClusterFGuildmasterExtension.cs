using System;
using Server;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Network;

namespace Server.Mobiles;

/// <summary>
/// Extends BaseGuildmaster with ClusterF guild system hooks:
///   - "Talk" context menu entry opens the appropriate guild gump.
///   - Speech triggers: "wish to join", "guild menu", plus guild-specific phrases.
///
/// BlacksmithGuildmaster routes to SmithGuildmasterGump (member status + work orders).
/// All other guildmasters use GuildTaskDetailGump (join/status screen).
///
/// Requires the OnCustomSpeech virtual hook added to BaseGuildmaster.cs (see patches/).
/// </summary>
public abstract partial class BaseGuildmaster
{
    public override void AddCustomContextEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.AddCustomContextEntries(from, ref list);

        if (from is not PlayerMobile pm) return;
        if (pm.Account is not IAccount acct) return;

        var def = ClusterFGuildSystem.GetDefForGuildmaster(GetType());
        if (def == null) return;

        list.Add(new GuildMembershipEntry(pm, def, acct));
    }

    protected partial bool OnCustomSpeech(PlayerMobile pm, SpeechEventArgs e)
    {
        var speech = e.Speech;

        var isJoinTrigger  = speech.Contains("wish to join", StringComparison.OrdinalIgnoreCase);
        var isMenuTrigger  = speech.Contains("guild menu",   StringComparison.OrdinalIgnoreCase);

        // Smith-specific triggers — only fire for BlacksmithGuildmaster
        var isSmithTrigger = this is BlacksmithGuildmaster &&
            (speech.Contains("smith",   StringComparison.OrdinalIgnoreCase) ||
             speech.Contains("society", StringComparison.OrdinalIgnoreCase) ||
             speech.Contains("seals",   StringComparison.OrdinalIgnoreCase) ||
             speech.Contains("forge",   StringComparison.OrdinalIgnoreCase));

        // Custodian-specific triggers — only fire for SanitationWarden
        var isCustodianTrigger = this is SanitationWarden &&
            (speech.Contains("custodian", StringComparison.OrdinalIgnoreCase) ||
             speech.Contains("clean",     StringComparison.OrdinalIgnoreCase) ||
             speech.Contains("civic",     StringComparison.OrdinalIgnoreCase) ||
             speech.Contains("tokens",    StringComparison.OrdinalIgnoreCase) ||
             speech.Contains("trash",     StringComparison.OrdinalIgnoreCase));

        // Artificer-specific triggers — only fire for ArtificersGuildmaster
        var isArtificerTrigger = this is ArtificersGuildmaster &&
            (speech.Contains("imbue",     StringComparison.OrdinalIgnoreCase) ||
             speech.Contains("artificer", StringComparison.OrdinalIgnoreCase) ||
             speech.Contains("enchant",   StringComparison.OrdinalIgnoreCase) ||
             speech.Contains("essence",   StringComparison.OrdinalIgnoreCase) ||
             speech.Contains("shard",     StringComparison.OrdinalIgnoreCase));

        if (!isJoinTrigger && !isMenuTrigger && !isSmithTrigger && !isCustodianTrigger && !isArtificerTrigger)
            return false;

        var def = ClusterFGuildSystem.GetDefForGuildmaster(GetType());
        if (def == null) return false;
        if (pm.Account is not IAccount acct) return false;

        OpenGuildGump(pm, def, acct);
        return true;
    }

    /// <summary>
    /// Opens the appropriate gump for this guildmaster type.
    /// Smith guildmaster gets the full Society of Smiths gump;
    /// all others use the standard task/join gump.
    /// </summary>
    private void OpenGuildGump(PlayerMobile pm, GuildDef def, IAccount acct)
    {
        if (this is BlacksmithGuildmaster smithNpc)
            pm.SendGump(new SmithGuildmasterGump(pm, def, acct, smithNpc));
        else if (this is SanitationWarden)
            pm.SendGump(new SanitationWardenGump(pm, def, acct));
        else if (this is ArtificersGuildmaster)
            pm.SendGump(new ArtificersGuildmasterGump(pm));
        else
            pm.SendGump(new GuildTaskDetailGump(pm, def, acct));
    }
}
