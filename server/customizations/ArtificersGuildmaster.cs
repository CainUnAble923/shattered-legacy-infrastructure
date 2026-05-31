using ModernUO.Serialization;
using Server.Collections;
using Server.ContextMenus;
using Server.Gumps;

namespace Server.Mobiles;

/// <summary>
/// Shattered Legacy — Artificers' Order Guildmaster.
///
/// Teaches the Imbuing skill and runs the Artificers' Order guild.
/// Placed in Ter Mur (Royal City) via ClusterFInstitutionSeeder.
///
/// Context menu:
///   - "Guild Membership"  — opens ArtificersGuildmasterGump (via ClusterFGuildmasterExtension)
///   - "Imbuing Table"     — opens ArtificersImbueGump directly
/// </summary>
[SerializationGenerator(0, false)]
public partial class ArtificersGuildmaster : BaseGuildmaster
{
    [Constructible]
    public ArtificersGuildmaster() : base("artificer")
    {
        SetSkill(SkillName.Imbuing,      100.0, 100.0);
        SetSkill(SkillName.EvalInt,       85.0, 100.0);
        SetSkill(SkillName.Magery,        85.0, 100.0);
        SetSkill(SkillName.MagicResist,   80.0, 100.0);
        SetSkill(SkillName.ItemID,        75.0,  90.0);
    }

    public override NpcGuild NpcGuild => NpcGuild.MagesGuild;

    public override void AddCustomContextEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.AddCustomContextEntries(from, ref list);

        if (from is PlayerMobile pm && pm.Alive && pm.InRange(Location, 5))
            list.Add(new ImbueTableEntry());
    }

    // ── Context menu entry — "Imbuing Table" ─────────────────────────────────
    // TODO: cliloc 6277 shows "Smelt Metal" — replace with a proper
    //       "Open Imbuing Table" cliloc once the correct number is confirmed.

    private sealed class ImbueTableEntry : ContextMenuEntry
    {
        public ImbueTableEntry() : base(6277, 5) { }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (from is PlayerMobile pm && !pm.Deleted && pm.CheckAlive())
                pm.SendGump(new ArtificersImbueGump(pm));
        }
    }

    // ── Serialization (v0 — no custom fields) ─────────────────────────────────

    private void Deserialize(IGenericReader reader, int version) { }
}
