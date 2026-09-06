using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
/// Outriders' Guildmaster — placed near the New Haven stables.
///
/// Handles Rangers' League guild join flow and member services
/// (standing, Trail Marks, work orders).
///
/// Location: Trammel 3524, 2574, Z=7 (New Haven stables area)
/// Seeded by: ClusterFInstitutionSeeder
/// </summary>
[SerializationGenerator(0, false)]
public partial class OutridersGuildmaster : BaseCreature
{
    [Constructible]
    public OutridersGuildmaster() : base(AIType.AI_Animal, FightMode.None, 10, 1)
    {
        Name  = "Mira Ashfen";
        Title = "the Outriders' Guildmaster";
        Body  = 0x191;    // Female humanoid
        Hue   = 0x83EA;   // Natural skin tone

        SetStr(65);
        SetDex(80);
        SetInt(55);
        SetHits(120);

        Fame  = 0;
        Karma = 2500;

        // Practical wilderness attire
        AddItem(new LeatherChest  { Movable = false, Hue = 0x8A3 }); // Forest brown
        AddItem(new LeatherLegs   { Movable = false, Hue = 0x8A3 });
        AddItem(new LeatherGloves { Movable = false, Hue = 0x8A3 });
        AddItem(new Boots         { Movable = false, Hue = 0x901 });
        AddItem(new Cloak         { Movable = false, Hue = 0x84C }); // Dark green

        // Long dark hair
        var hair = new Item(0x2046) { Movable = false, Hue = 0x455 };
        hair.Layer = Layer.Hair;
        AddItem(hair);
    }

    public override bool IsInvulnerable => true;
    public override bool ClickTitle      => true;
    public override bool ShowFameTitle   => false;

    // ── Interaction ──────────────────────────────────────────────────────────────────────

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm) return;

        if (!pm.InRange(Location, 4))
        {
            pm.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        pm.SendGump(new OutridersGuildmasterGump(pm));
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from is PlayerMobile pm && pm.InRange(Location, 4))
            list.Add(new TalkEntry(pm, this));
    }

    public override bool HandlesOnSpeech(Mobile from) =>
        from is PlayerMobile && from.InRange(Location, 4) || base.HandlesOnSpeech(from);

    public override void OnSpeech(SpeechEventArgs e)
    {
        base.OnSpeech(e);

        if (e.Mobile is not PlayerMobile pm || !pm.InRange(Location, 4))
            return;

        var s = e.Speech;
        if (s.Contains("outrider",  StringComparison.OrdinalIgnoreCase) ||
            s.Contains("ranger",    StringComparison.OrdinalIgnoreCase) ||
            s.Contains("orders",    StringComparison.OrdinalIgnoreCase) ||
            s.Contains("menu",      StringComparison.OrdinalIgnoreCase) ||
            s.Contains("work",      StringComparison.OrdinalIgnoreCase) ||
            s.Contains("taming",    StringComparison.OrdinalIgnoreCase))
        {
            pm.SendGump(new OutridersGuildmasterGump(pm));
        }
    }

    private sealed class TalkEntry : ContextMenuEntry
    {
        private readonly PlayerMobile         _pm;
        private readonly OutridersGuildmaster _npc;

        public TalkEntry(PlayerMobile pm, OutridersGuildmaster npc) : base(6146) // "Talk"
        {
            _pm  = pm;
            _npc = npc;
        }

        public override void OnClick(Mobile from, IEntity target) => _npc.OnDoubleClick(_pm);
    }
}
