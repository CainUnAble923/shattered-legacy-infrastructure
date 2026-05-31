using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Mobiles;

// ─────────────────────────────────────────────────────────────────────────────
// SanitationWarden — The Custodians guildmaster NPC
//
// GM-placeable civic NPC that handles join and status for The Custodians.
// Not a vendor — BaseGuildmaster already sets IsActiveVendor = false.
//
// Speech triggers (via ClusterFGuildmasterExtension):
//   "wish to join", "guild menu", "custodian", "clean", "civic", "tokens", "trash"
//
// Context menu "Talk" → SanitationWardenGump (member status or join view).
// On join: player receives a TrashBag via OnCustodiansJoined().
// ─────────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class SanitationWarden : BaseGuildmaster
{
    // ── Ambient speech ────────────────────────────────────────────────────────

    private static readonly string[] AmbientLines =
    {
        "Britannia's streets won't clean themselves!",
        "A tidy town is a safe town.",
        "Every piece of refuse removed is a service to the realm.",
        "The Custodians keep the roads clear and the citizenry healthy.",
        "You there — seen any litter about? We pay good Civic Tokens for clean streets.",
        "Cleanliness is next to civic virtue.",
        "Join The Custodians and earn your keep keeping the peace — of the streets.",
    };

    private DateTime _nextSpeech;

    // ── Construction ──────────────────────────────────────────────────────────

    [Constructible]
    public SanitationWarden() : base("Custodian")
    {
        Name = "Sanitation Warden";

        SetSkill(SkillName.Anatomy,  36.0, 68.0);
        SetSkill(SkillName.Fencing,  36.0, 68.0);
        SetSkill(SkillName.Macing,   36.0, 68.0);
        SetSkill(SkillName.Swords,   36.0, 68.0);
        SetSkill(SkillName.Tactics,  36.0, 68.0);
    }

    public SanitationWarden(Serial serial) : base(serial) { }

    // ── Ambient speech ────────────────────────────────────────────────────────

    public override void OnThink()
    {
        base.OnThink();

        if (DateTime.UtcNow >= _nextSpeech && Combatant == null)
        {
            Say(AmbientLines[Utility.Random(AmbientLines.Length)]);
            _nextSpeech = DateTime.UtcNow + TimeSpan.FromSeconds(Utility.RandomMinMax(60, 120));
        }
    }

    // ── Guild join bonus ──────────────────────────────────────────────────────

    /// <summary>
    /// Called after Custodians membership is granted (from gump or [guild command).
    /// Issues a TrashBag to the new member if they don't already have one.
    /// </summary>
    public static void OnCustodiansJoined(PlayerMobile pm)
    {
        if (pm.Backpack == null) return;

        // Only issue one bag per player
        foreach (var item in pm.Backpack.Items)
        {
            if (item is TrashBag) return;
        }

        var bag = new TrashBag();
        pm.Backpack.DropItem(bag);
        pm.SendMessage(0x44,
            "You have been issued a Trash Bag. " +
            "Drag litter into it, then double-click to dump for Civic Tokens.");
    }
}
