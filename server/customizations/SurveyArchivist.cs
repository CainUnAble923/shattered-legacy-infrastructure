using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
/// Miners' Compact Survey Archivist — accepts ore discovery reports from players
/// who carry a Prospector's Logbook and grants Compact Standing and Mining Vouchers.
///
/// Location: Trammel 3516, 2747, Z=1 (temporary — near south mountain mine entrance;
///           to be relocated when the mine encampment is built)
/// Seeded by: ClusterFInstitutionSeeder
/// </summary>
[SerializationGenerator(0, false)]
public partial class SurveyArchivist : BaseCreature
{
    [Constructible]
    public SurveyArchivist() : base(AIType.AI_Animal, FightMode.None, 10, 1)
    {
        Name  = "Velara Thorne";
        Title = "the Survey Archivist";
        Body  = 0x191;    // Female humanoid
        Hue   = 0x83EA;   // Natural skin tone

        SetStr(50);
        SetDex(40);
        SetInt(80);
        SetHits(100);

        Fame  = 0;
        Karma = 2000;

        // Scholar's attire — ink-stained robes, field boots
        AddItem(new Robe         { Movable = false, Hue = 0x0455 }); // Deep slate blue
        AddItem(new Boots        { Movable = false, Hue = 0x0901 });

        // Long dark hair
        var hair = new Item(0x203C) { Movable = false, Hue = 0x0455 };
        hair.Layer = Layer.Hair;
        AddItem(hair);
    }

    public override bool IsInvulnerable => true;
    public override bool ClickTitle      => true;
    public override bool ShowFameTitle   => false;

    // ── Interaction ───────────────────────────────────────────────────────────

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm) return;

        if (!pm.InRange(Location, 4))
        {
            pm.SendMessage("You are too far away to speak with the Survey Archivist.");
            return;
        }

        pm.CloseGump<SurveyArchivistGump>();
        pm.SendGump(new SurveyArchivistGump(pm));
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);
        if (from is PlayerMobile && from.InRange(Location, 4))
            list.Add(new TalkEntry(this));
    }

    private sealed class TalkEntry : ContextMenuEntry
    {
        private readonly SurveyArchivist _npc;
        public TalkEntry(SurveyArchivist npc) : base(6146) => _npc = npc; // "Talk"

        public override void OnClick(Mobile from, IEntity target)
        {
            if (from is PlayerMobile pm && !_npc.Deleted)
                _npc.OnDoubleClick(pm);
        }
    }

    // ── Ambient speech ────────────────────────────────────────────────────────

    public override bool OnBeforeDeath() => false; // Invulnerable — never dies

    private void Deserialize(IGenericReader reader, int version) { }
}
