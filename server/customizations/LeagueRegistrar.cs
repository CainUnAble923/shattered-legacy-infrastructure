using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
/// League Registrar — the League of Extraordinary Citizens representative
/// stationed at the New Haven field office (Trammel 3459, 2601).
///
/// Behaviour:
///   - Double-click or "Talk" context menu opens LeagueRegistrarGump.
///   - Ambient speech fires every 25-45 seconds when players are within 10 tiles,
///     cycling through a set of attention-drawing lines.
///   - Proximity greeting: says a personal greeting the first time a player
///     enters range (tracked per-session via a HashSet of player serials).
/// </summary>
[SerializationGenerator(0, false)]
public partial class LeagueRegistrar : BaseCreature
{
    private static readonly string[] AmbientLines =
    {
        "Welcome to New Haven, traveller. Speak with me when you have a moment.",
        "Looking for purpose? The guilds of New Haven are always seeking new members.",
        "The League of Extraordinary Citizens — register today. It costs only your time.",
        "The Miners' Compact has a liaison near the south mine. Fine people, all of them.",
        "New to Britannia? I can help you find your footing here in New Haven.",
        "Registered citizens of the League are known throughout Sosaria. Ask me how.",
    };

    private int                _ambientIndex;
    private readonly HashSet<Serial> _greeted = new();

    [Constructible]
    public LeagueRegistrar() : base(AIType.AI_Animal, FightMode.None, 10, 1)
    {
        Name  = "Elara Voss";
        Title = "the League Registrar";
        Body  = 0x191;                  // Female humanoid
        Hue   = 0x8403;                 // Natural skin tone

        SetStr(50);
        SetDex(50);
        SetInt(75);
        SetHits(100);

        Fame  = 0;
        Karma = 5000;

        // Civic attire — deep navy robe with gold trim sash, dark sandals
        AddItem(new Robe    { Movable = false, Hue = 0x455  });  // Deep navy
        AddItem(new Sandals { Movable = false, Hue = 0x455  });
        AddItem(new BodySash { Movable = false, Hue = 0x8AB  }); // Gold sash

        // Hair
        var hair = new Item(0x203C) { Movable = false, Hue = 0x455 }; // Long hair, dark navy
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

        pm.SendGump(new LeagueRegistrarGump(pm));
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from is PlayerMobile pm && pm.InRange(Location, 4))
            list.Add(new TalkEntry(pm, this));
    }

    private sealed class TalkEntry : ContextMenuEntry
    {
        private readonly PlayerMobile     _pm;
        private readonly LeagueRegistrar  _npc;

        public TalkEntry(PlayerMobile pm, LeagueRegistrar npc) : base(6146) // "Talk"
        {
            _pm  = pm;
            _npc = npc;
        }

        public override void OnClick(Mobile from, IEntity target) => _npc.OnDoubleClick(_pm);
    }

    // ── Ambient speech ────────────────────────────────────────────────────────

    public override void OnAfterSpawn()
    {
        base.OnAfterSpawn();
        ScheduleAmbientSpeech();
    }

    private void ScheduleAmbientSpeech()
    {
        if (Deleted) return;
        var delay = TimeSpan.FromSeconds(Utility.RandomMinMax(25, 45));
        Timer.DelayCall(delay, OnAmbientTick);
    }

    private void OnAmbientTick()
    {
        if (Deleted) return;

        try
        {
            // Only speak if at least one player is nearby
            bool hasPlayer = false;
            foreach (var m in GetMobilesInRange(10))
            {
                if (m is PlayerMobile)
                {
                    hasPlayer = true;
                    break;
                }
            }

            if (hasPlayer)
            {
                Say(AmbientLines[_ambientIndex % AmbientLines.Length]);
                _ambientIndex++;
            }

            CheckProximityGreetings();
        }
        finally
        {
            ScheduleAmbientSpeech();
        }
    }

    private void CheckProximityGreetings()
    {
        if (Deleted) return;

        foreach (var m in GetMobilesInRange(6))
        {
            if (m is PlayerMobile pm && _greeted.Add(pm.Serial))
            {
                // Capture for closure — delay so greeting doesn't overlap ambient line
                var target = pm;
                Timer.DelayCall(TimeSpan.FromSeconds(2.0), () =>
                {
                    if (!Deleted && !target.Deleted && target.InRange(Location, 8))
                        Say($"Ah, {target.Name}! Welcome to New Haven. I am Elara Voss — the League Registrar. Do speak with me when you have a moment.");
                });
            }
        }
    }
}
