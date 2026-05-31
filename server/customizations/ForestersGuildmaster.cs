using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
/// Foresters' Guildmaster — placed near the New Haven carpenter shop.
///
/// Handles Foresters' Union guild join flow and member services
/// (standing, Timber Tokens, work orders).
///
/// Seeded by: ClusterFInstitutionSeeder
/// </summary>
[SerializationGenerator(0, false)]
public partial class ForestersGuildmaster : BaseCreature
{
    [Constructible]
    public ForestersGuildmaster() : base(AIType.AI_Animal, FightMode.None, 10, 1)
    {
        Name  = "Cedric Rowanwood";
        Title = "the Foresters' Guildmaster";
        Body  = 0x190;    // Male humanoid
        Hue   = 0x83EA;   // Natural skin tone

        SetStr(75);
        SetDex(60);
        SetInt(55);
        SetHits(130);

        Fame  = 0;
        Karma = 2500;

        // Practical woodsman attire
        AddItem(new FeatheredHat  { Movable = false, Hue = 0x84C }); // Dark green
        AddItem(new LeatherChest  { Movable = false, Hue = 0x8A3 }); // Forest brown
        AddItem(new LeatherLegs   { Movable = false, Hue = 0x8A3 });
        AddItem(new LeatherGloves { Movable = false, Hue = 0x8A3 });
        AddItem(new Boots         { Movable = false, Hue = 0x901 });
        AddItem(new Cloak         { Movable = false, Hue = 0x84C }); // Dark green

        // Short brown hair
        var hair = new Item(0x203C) { Movable = false, Hue = 0x44E };
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
            pm.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        pm.SendGump(new ForestersGuildmasterGump(pm));
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
        if (s.Contains("forester",  StringComparison.OrdinalIgnoreCase) ||
            s.Contains("woodcut",   StringComparison.OrdinalIgnoreCase) ||
            s.Contains("lumber",    StringComparison.OrdinalIgnoreCase) ||
            s.Contains("timber",    StringComparison.OrdinalIgnoreCase) ||
            s.Contains("orders",    StringComparison.OrdinalIgnoreCase) ||
            s.Contains("menu",      StringComparison.OrdinalIgnoreCase) ||
            s.Contains("contracts", StringComparison.OrdinalIgnoreCase))
        {
            pm.SendGump(new ForestersGuildmasterGump(pm));
        }
    }

    private sealed class TalkEntry : ContextMenuEntry
    {
        private readonly PlayerMobile         _pm;
        private readonly ForestersGuildmaster _npc;

        public TalkEntry(PlayerMobile pm, ForestersGuildmaster npc) : base(6146) // "Talk"
        {
            _pm  = pm;
            _npc = npc;
        }

        public override void OnClick(Mobile from, IEntity target) => _npc.OnDoubleClick(_pm);
    }
}
