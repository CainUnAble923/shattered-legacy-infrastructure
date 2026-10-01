// ThievesDenLookout.cs
//
// cc-P22, the F-9 follow-up (Chase, 2026-09-30): the Thieves' Den guildmaster stays in the inn's back room
// (GuildLocations, "thieves"), and a lower-rank member sits on a stool by the fighting pit in the building west of
// the Healer's Hall and sends players to the guildmaster. Ours.
//
// Not a vendor and not a trainer (a BaseCreature, not a BaseVendor or BaseGuildmaster: ThiefGuildmaster is a
// vendor with a guild join, and the lookout must offer neither). Invulnerable, never walks, faces east over the pit.
// Double-click, or say "guild", "thief", "thieves" or "join" near him: two lines, then the same quest arrow the
// Guild Directory's "Show me the way" uses (ClusterFGuildStarter.ShowTheWay), so a missing guildmaster is reported
// in that rule's words, never silently skipped.
//
// Placed by ClusterFGuildHallSeeder (GuildLocations.Lookouts), not by world load: see that file.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ThievesDenLookout : BaseCreature
{
    public const string GuildKey = "thieves";

    private const int TalkRange = 4;

    private static readonly string[] Keywords = ["guild", "thief", "thieves", "join"];

    [Constructible]
    public ThievesDenLookout() : base(AIType.AI_Animal, FightMode.None, 10, 1)
    {
        Name = "Wren";
        Title = "the Thieves' Den lookout";
        Body = 0x190;
        Hue = Race.Human.RandomSkinHue();
        CantWalk = true;
        Direction = Direction.East;

        SetStr(50);
        SetDex(75);
        SetInt(50);
        SetHits(100);

        Fame = 0;
        Karma = 0;

        AddItem(new Cloak { Movable = false, Hue = 0x455 });
        AddItem(new Shirt { Movable = false, Hue = 0x497 });
        AddItem(new LongPants { Movable = false, Hue = 0x455 });
        AddItem(new Boots { Movable = false, Hue = 0x497 });
        AddItem(new Bandana { Movable = false, Hue = 0x455 });
        AddItem(new Dagger { Movable = false });

        var hair = new Item(0x203B) { Movable = false, Hue = 0x44E, Layer = Layer.Hair }; // short hair
        AddItem(hair);
    }

    public override bool IsInvulnerable => true;
    public override bool ClickTitle => true;
    public override bool ShowFameTitle => false;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        if (!pm.InRange(Location, TalkRange))
        {
            pm.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        SendToGuildmaster(pm);
    }

    public override bool HandlesOnSpeech(Mobile from) => from is PlayerMobile && from.InRange(Location, TalkRange);

    public override void OnSpeech(SpeechEventArgs e)
    {
        if (!e.Handled && e.Mobile is PlayerMobile pm && pm.InRange(Location, TalkRange) && IsAboutTheGuild(e.Speech))
        {
            e.Handled = true;
            SendToGuildmaster(pm);
            return;
        }

        base.OnSpeech(e);
    }

    public static bool IsAboutTheGuild(string speech)
    {
        if (string.IsNullOrEmpty(speech))
        {
            return false;
        }

        foreach (var word in Keywords)
        {
            if (speech.Contains(word, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Two lines from the lookout, then the directory's arrow to the guildmaster, or its words when the guildmaster
    /// is missing. Returns what the player was told by the arrow (for tests).
    /// </summary>
    public string SendToGuildmaster(PlayerMobile pm)
    {
        var present = false;
        foreach (var loc in GuildLocations.For(GuildKey))
        {
            if (loc.Map != null && loc.Find() != null)
            {
                present = true;
                break;
            }
        }

        if (present)
        {
            SayTo(pm, "Not so loud. The Den does not do its business out here by the pit.");
            SayTo(pm, "The guildmaster keeps the back room of the Bountiful Harvest Inn. Follow the mark, and keep your hands to yourself.");
        }
        else
        {
            SayTo(pm, "The guildmaster should be in the back room of the Bountiful Harvest Inn, but I have not seen them today.");
        }

        var told = ClusterFGuildStarter.ShowTheWay(pm, GuildKey);
        pm.SendMessage(present ? 0x44 : 0x22, told);
        return told;
    }
}
