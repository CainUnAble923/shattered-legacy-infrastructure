// GuildStyle.cs
//
// cc-P436 (Chase 2026-10-22: "Can we change the gumps for our guilds/league from art we have found so far?"; his pick
// 2026-10-25: look B, stone and bronze). Notes: shard-migration notes/cc-P436-guild-league-gump-look.md.
//
// One look for every guild and League gump, applied in one place. The gumps keep drawing today's stock art (frame 9270,
// cc-P169's solid panel 2624 or an alpha region inside it, rules 9304, the stock buttons); GuildStyledGump, their base,
// rewrites those entries when it is sent to a client that has our art:
//   - frame 9270 becomes our frame 23200 (nine pieces, 23200-23208: Erachiro's basalt with its braided bronze edge,
//     player-package\vendor\shattered-legacy-art\gumps\),
//   - the panel or alpha region that fills a frame's inside goes (the frame's own basalt centre is the inside, opaque),
//   - a rule 9304 becomes the frame's bronze edge: the top edge (23201) across, the left edge (23203) down, at the
//     rule's own size (AddImageTiled clips, so a 1 or 2 pixel rule is the braid's outer rows).
// Buttons and text are unchanged. Every other client gets the gump exactly as built: stock art.
//
// Who has our art: the player package ships the PNGs with our patched TazUO, and only that client acknowledges the
// ClientCommand enhanced packet (TazUOClientCommand.CanRunCommands). A stock client, Classic or a stock TazUO never
// does, so it never receives an ID it cannot draw. clusterf.guildStyle.enabled (default True) turns the look off for
// everyone, for a server that goes live before the package carrying the art is published.

using System.Collections.Generic;
using Server.Network;
using Server.TazUO;

namespace Server.Gumps;

public static class GuildStyle
{
    public const int StockFrame = 9270;
    public const int StockRule = 9304;

    /// <summary>Our frame, 23200-23208 (registry.csv rows, cc-P436).</summary>
    public const int Frame = 23200;

    public const int RuleAcross = Frame + 1; // the top edge
    public const int RuleDown = Frame + 3;   // the left edge

    /// <summary>How far inside a frame a panel or alpha region may start and still be that frame's inside.</summary>
    public const int MaxInset = 12;

    public static bool Enabled { get; set; } = true;

    public static void Configure()
    {
        Enabled = ServerConfiguration.GetOrUpdateSetting("clusterf.guildStyle.enabled", true);
    }

    /// <summary>The client shows our frame: the look is on and the client is our patched TazUO.</summary>
    public static bool UsesOurArt(NetState ns) => Enabled && TazUOClientCommand.CanRunCommands(ns);

    /// <summary>Rewrites a built gump's stock frame, inside and rules to our look. Returns how many entries changed.</summary>
    public static int Restyle(Gump gump)
    {
        var frames = new List<GumpBackground>();

        foreach (var entry in gump.Entries)
        {
            if (entry is GumpBackground { GumpID: StockFrame } bg)
            {
                frames.Add(bg);
            }
        }

        var changed = 0;
        var insides = new List<GumpEntry>();

        foreach (var entry in gump.Entries)
        {
            switch (entry)
            {
                case GumpAlphaRegion a when IsInside(frames, a.X, a.Y, a.Width, a.Height):
                    insides.Add(entry);
                    break;
                case GumpImageTiled { GumpID: RequirementStyle.PanelTile } t when IsInside(frames, t.X, t.Y, t.Width, t.Height):
                    insides.Add(entry);
                    break;
                case GumpImageTiled { GumpID: StockRule } rule:
                    rule.GumpID = rule.Width >= rule.Height ? RuleAcross : RuleDown;
                    changed++;
                    break;
            }
        }

        foreach (var bg in frames)
        {
            bg.GumpID = Frame;
            changed++;
        }

        foreach (var entry in insides)
        {
            gump.Remove(entry);
            changed++;
        }

        return changed;
    }

    // A rectangle that fills a frame, starting 0 to MaxInset pixels inside each of its four edges.
    private static bool IsInside(List<GumpBackground> frames, int x, int y, int w, int h)
    {
        foreach (var f in frames)
        {
            int left = x - f.X, top = y - f.Y, right = f.X + f.Width - (x + w), bottom = f.Y + f.Height - (y + h);

            if (left is >= 0 and <= MaxInset && top is >= 0 and <= MaxInset &&
                right is >= 0 and <= MaxInset && bottom is >= 0 and <= MaxInset)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>
///     cc-P436: the base of every guild and League gump. Built with stock art as before; restyled once, as it is sent,
///     for a client that has our art (GuildStyle).
/// </summary>
public abstract class GuildStyledGump : Gump
{
    protected GuildStyledGump(int x, int y) : base(x, y)
    {
    }

    /// <summary>The gump has been rewritten to our look (it was sent to a client with our art).</summary>
    public bool Restyled { get; private set; }

    public override void SendTo(NetState ns)
    {
        if (!Restyled && GuildStyle.UsesOurArt(ns))
        {
            GuildStyle.Restyle(this);
            Restyled = true;
        }

        base.SendTo(ns);
    }
}
