using ModernUO.Serialization;
using Server.Accounting;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;

namespace Server.Mobiles;

// The guildmasters of the two guilds cc-P15 adds (F-9 Decision 1). No stock guildmaster type fits
// either, so each is its own NPC, built like OutridersGuildmaster. Talking to one opens the guild's
// hall page. Placed by ClusterFGuildHallSeeder, never by pinned spawn data.

[SerializationGenerator(0, false)]
public abstract partial class StarterGuildmasterBase : BaseCreature
{
    protected StarterGuildmasterBase() : base(AIType.AI_Animal, FightMode.None, 10, 1)
    {
        SetStr(80);
        SetDex(80);
        SetInt(80);
        SetHits(150);

        Fame  = 0;
        Karma = 0;
    }

    public abstract string GuildKey { get; }

    public override bool IsInvulnerable => true;
    public override bool ClickTitle      => true;
    public override bool ShowFameTitle   => false;

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm) return;

        if (!pm.InRange(Location, 4))
        {
            pm.SendLocalizedMessage(500446); // That is too far away.
            return;
        }

        var def = ClusterFGuildSystem.GetDef(GuildKey);
        if (def != null)
            ClusterFGuildSystem.OpenGuildHall(pm, def, this);
    }

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);

        if (from is PlayerMobile pm && pm.InRange(Location, 4) && pm.Account is IAccount acct &&
            ClusterFGuildSystem.GetDef(GuildKey) is { } def)
        {
            list.Add(new GuildMembershipEntry(pm, def, acct));
        }
    }
}

[SerializationGenerator(0, false)]
public partial class TwinPathsDojoGuildmaster : StarterGuildmasterBase
{
    [Constructible]
    public TwinPathsDojoGuildmaster()
    {
        Name  = "Sensei Haruka";
        Title = "of the Twin Paths Dojo";
        Body  = 0x191;
        Hue   = 0x83EA;
        Female = true;

        AddItem(new Hakama { Movable = false, Hue = 0x455 });
        AddItem(new FemaleKimono { Movable = false, Hue = 0x497 });
        AddItem(new SamuraiTabi { Movable = false });
        AddItem(new Wakizashi { Movable = false });

        var hair = new Item(0x203C) { Movable = false, Hue = 0x455, Layer = Layer.Hair };
        AddItem(hair);
    }

    public override string GuildKey => "dojo";
}

[SerializationGenerator(0, false)]
public partial class KeepersOfTheLastDoorGuildmaster : StarterGuildmasterBase
{
    [Constructible]
    public KeepersOfTheLastDoorGuildmaster()
    {
        Name  = "Warden Morrow";
        Title = "of the Keepers of the Last Door";
        Body  = 0x190;
        Hue   = 0x83F8;

        AddItem(new Robe { Movable = false, Hue = 0x455 });
        AddItem(new Sandals { Movable = false, Hue = 0x455 });
        AddItem(new BoneHelm { Movable = false });

        var hair = new Item(0x203B) { Movable = false, Hue = 0x3B2, Layer = Layer.Hair };
        AddItem(hair);
    }

    public override string GuildKey => "keepers";
}
