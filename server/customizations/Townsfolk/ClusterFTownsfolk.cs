// cc-P631 (P566 design, stage 1): the New Haven residents who answer townsfolk topics, and their topic menu. Notes:
// shard-migration notes/cc-P631-build-townsfolk-rest.md.
//
// ClusterFTownsfolk  a stationary, invulnerable person with a fixed identity and outfit chosen by a saved tag ("plaza":
//                    Mara, the town steward; "trades": Ellis, the workshop runner). No loot, no teaching, no vendor
//                    stock. Speech, double-click and the stock Talk entry go to ClusterFTownsfolkService; anything it does
//                    not own goes on to the ordinary creature speech. Stage 1 places none: staff [add one on the test
//                    shard; the seeder is stage 3.
// TownsfolkTalkGump  the topic buttons, for players who do not know the words. Every button rechecks the player, the
//                    NPC, range, sight and region, and shares the speech cooldowns.

#nullable enable

using System;
using System.Linq;
using ModernUO.Serialization;
using Server.ContextMenus;
using Server.Collections;
using Server.Engines.Townsfolk;
using Server.Gumps;
using Server.Items;
using Server.Network;
using Server.Tutorial;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ClusterFTownsfolk : BaseCreature, ITalkResident
{
    public const string PlazaTag = "plaza";
    public const string TradesTag = "trades";

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private string _tag = PlazaTag;

    [Constructible(AccessLevel.Administrator)]
    public ClusterFTownsfolk(string tag) : base(AIType.AI_Animal, FightMode.None, 10, 1)
    {
        _tag = tag == TradesTag ? TradesTag : PlazaTag;
        CantWalk = true;
        Blessed = true;

        if (_tag == TradesTag)
        {
            Name = "Ellis";
            Title = "the workshop runner";
            Body = 0x190;
            Hue = 0x83EA;
            HairItemID = 0x203B;
            HairHue = 0x44E;
            AddItem(new Shirt(0x47E) { Movable = false });
            AddItem(new LongPants(0x455) { Movable = false });
            AddItem(new HalfApron(0x2E5) { Movable = false });
            AddItem(new Boots(0x6D2) { Movable = false });
        }
        else
        {
            Name = "Mara";
            Title = "the town steward";
            Body = 0x191;
            Female = true;
            Hue = 0x83F4;
            HairItemID = 0x203C;
            HairHue = 0x455;
            AddItem(new FancyShirt(0x5A6) { Movable = false });
            AddItem(new LongPants(0x1BB) { Movable = false });
            AddItem(new BodySash(0x59B) { Movable = false });
            AddItem(new Shoes(0x6D2) { Movable = false });
        }
    }

    public string TalkProfile => _tag;

    public override bool IsInvulnerable => true;
    public override bool ClickTitle => true;
    public override bool CanTeach => false;
    public override bool CanAutoStable => false;

    public override void GenerateLoot()
    {
    }

    public override void GenerateLoot(bool spawning)
    {
    }

    public override bool HandlesOnSpeech(Mobile from) =>
        ClusterFTownsfolkService.Enabled && from.Player && from.InRange(Location, ClusterFTownsfolkService.TalkRange) ||
        base.HandlesOnSpeech(from);

    public override void OnSpeech(SpeechEventArgs e)
    {
        if (!ClusterFTownsfolkService.TryHandleSpeech(this, e))
        {
            base.OnSpeech(e);
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!ClusterFTownsfolkService.OpenMenu(this, from))
        {
            base.OnDoubleClick(from);
        }
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);
        if (ClusterFTownsfolkService.Enabled && from.Player)
        {
            list.Add(new TownsfolkTalkEntry());
        }
    }

    public override bool OnDragDrop(Mobile from, Item dropped) => false;

    private class TownsfolkTalkEntry : ContextMenuEntry
    {
        // 6146: the stock "Talk" (cliloc 3006146), as GuildMembershipEntry uses.
        public TownsfolkTalkEntry() : base(6146, 3)
        {
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (target is Mobile npc)
            {
                ClusterFTownsfolkService.OpenMenu(npc, from);
            }
        }
    }
}

public class TownsfolkTalkGump : Gump
{
    private const int Width = 300;

    private readonly PlayerMobile _player;
    private readonly Mobile _npc;
    private readonly string[] _topics;

    public TownsfolkTalkGump(PlayerMobile player, Mobile npc) : base(120, 100)
    {
        _player = player;
        _npc = npc;
        var profile = npc is ITalkResident r ? ClusterFTalkRules.Current?.Profile(r.TalkProfile) : null;
        _topics = profile == null ? [] : profile.Topics.Select(t => t.Id).ToArray();

        var height = 70 + _topics.Length * 26;
        TutorialStyle.Panel(this, Width, height);
        AddHtml(20, 18, Width - 40, 20, TutorialStyle.HeadingHtml(Escape(npc.Name ?? "")));
        for (var i = 0; i < _topics.Length; i++)
        {
            var y = 46 + i * 26;
            AddButton(20, y, 4005, 4007, i + 1);
            AddHtml(55, y + 2, Width - 75, 20, TutorialStyle.Html(Label(_topics[i])));
        }
    }

    public static string Label(string topic) => topic switch
    {
        "hello" => "Say hello",
        "name" => "Ask their name",
        "job" => "Ask about their work",
        "help" => "Ask for help",
        "inn" => "Ask about the inn",
        "rumors" => "Ask for rumors",
        "goodbye" => "Say goodbye",
        _ => topic
    };

    public static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var index = info.ButtonID - 1;
        if (sender.Mobile != _player || index < 0 || index >= _topics.Length)
        {
            return;
        }

        // The table may have been reloaded since: Answer looks the topic up again and says nothing for a removed one.
        ClusterFTownsfolkService.Answer(_npc, _player, _topics[index]);
        if (ClusterFTownsfolkService.CanTalk(_npc, _player))
        {
            ClusterFTownsfolkService.OpenMenu(_npc, _player);
        }
    }
}
