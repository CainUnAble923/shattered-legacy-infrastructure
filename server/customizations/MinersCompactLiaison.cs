using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
/// Miners' Compact Liaison -- stationed near the south mountain mine in New Haven.
///
/// Provides players with information about the Miners' Compact guild and allows
/// them to begin the join flow, submit work orders, and request restorations.
///
/// Location: Trammel 3510, 2748, Z=0 (south mountain mine area)
/// Seeded by: ClusterFInstitutionSeeder
/// </summary>
[SerializationGenerator(0, false)]
public partial class MinersCompactLiaison : BaseCreature
{
    [Constructible]
    public MinersCompactLiaison() : base(AIType.AI_Animal, FightMode.None, 10, 1)
    {
        Name  = "Garrett Ashveil";
        Title = "the Miners' Compact Liaison";
        Body  = 0x190;    // Male humanoid
        Hue   = 0x8403;   // Natural skin tone

        SetStr(60);
        SetDex(50);
        SetInt(50);
        SetHits(100);

        Fame  = 0;
        Karma = 2000;

        // Working attire -- earthy tones, worn leather
        AddItem(new Shirt       { Movable = false, Hue = 0x8A4 }); // Earthy brown
        AddItem(new LongPants   { Movable = false, Hue = 0x8A4 });
        AddItem(new Boots       { Movable = false, Hue = 0x901 });
        AddItem(new LeatherGloves { Movable = false, Hue = 0x8A4 });

        // Short hair, brown
        var hair = new Item(0x2048) { Movable = false, Hue = 0x8A4 };
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

        pm.SendGump(new MinersCompactLiaisonGump(pm));
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from is PlayerMobile pm && pm.InRange(Location, 4))
            list.Add(new TalkEntry(pm, this));
    }

    // ── Speech trigger — say "compact", "orders", "menu", or "work" to open menu ──

    public override bool HandlesOnSpeech(Mobile from) =>
        from is PlayerMobile && from.InRange(Location, 4) || base.HandlesOnSpeech(from);

    public override void OnSpeech(SpeechEventArgs e)
    {
        base.OnSpeech(e);

        if (e.Mobile is not PlayerMobile pm || !pm.InRange(Location, 4))
            return;

        var s = e.Speech;
        if (s.Contains("compact", StringComparison.OrdinalIgnoreCase) ||
            s.Contains("orders",  StringComparison.OrdinalIgnoreCase) ||
            s.Contains("menu",    StringComparison.OrdinalIgnoreCase) ||
            s.Contains("work",    StringComparison.OrdinalIgnoreCase))
        {
            pm.SendGump(new MinersCompactLiaisonGump(pm));
        }
    }

    private sealed class TalkEntry : ContextMenuEntry
    {
        private readonly PlayerMobile         _pm;
        private readonly MinersCompactLiaison  _npc;

        public TalkEntry(PlayerMobile pm, MinersCompactLiaison npc) : base(6146) // "Talk"
        {
            _pm  = pm;
            _npc = npc;
        }

        public override void OnClick(Mobile from, IEntity target) => _npc.OnDoubleClick(_pm);
    }
}
