using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
/// cc-P708: the League's steward at its public headquarters in Valunstead's keep (ClusterFGuildHallPlacementData "league",
/// Chase's pick of 2026-10-27 from P704). Not a second Elara: Elara Voss (LeagueRegistrar) stays at the field office in
/// New Haven, where joining the League and the guilds, and the chapter reports, happen. The steward keeps the
/// headquarters' records and sends anyone asking to join to Elara, so the distant headquarters is never needed to join or
/// to finish the introductions. Placed with the other halls by [ClusterFGuildHalls place (test shard only until live).
/// </summary>
[SerializationGenerator(0, false)]
public partial class LeagueSteward : BaseCreature
{
    public const string WelcomeLine =
        "Welcome to the League's headquarters. The guilds' common work is recorded here, and the great hall behind me is where the League meets.";

    public const string JoinLine =
        "To join the League or a guild, see Elara Voss at the League's field office in New Haven. Nothing here is needed for that.";

    [Constructible]
    public LeagueSteward() : base(AIType.AI_Animal, FightMode.None, 10, 1)
    {
        Name = "Aldous Penhallow";
        Title = "the League Steward";
        Body = 0x190;
        Hue = 0x83EA;

        SetStr(60);
        SetDex(50);
        SetInt(70);
        SetHits(100);

        Fame = 0;
        Karma = 2000;

        AddItem(new Robe { Movable = false, Hue = 0x0455 });
        AddItem(new Shoes { Movable = false, Hue = 0x0901 });
        var hair = new Item(0x203B) { Movable = false, Hue = 0x044E, Layer = Layer.Hair };
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

        if (!pm.InRange(Location, 4))
        {
            pm.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        SayTo(pm, WelcomeLine);
        SayTo(pm, JoinLine);
    }

    public override bool HandlesOnSpeech(Mobile from) => from is PlayerMobile && from.InRange(Location, 4);

    public override void OnSpeech(SpeechEventArgs e)
    {
        base.OnSpeech(e);
        if (e.Handled || e.Mobile is not PlayerMobile pm || !pm.InRange(Location, 4))
        {
            return;
        }

        var said = e.Speech.ToLowerInvariant();
        if (said.Contains("join") || said.Contains("league") || said.Contains("guild"))
        {
            e.Handled = true;
            SayTo(pm, JoinLine);
        }
    }
}
