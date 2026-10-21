// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Items/ExodusTomeAltar.cs (cc-P195): the Summoning Tome a
// party builds on a shrine. It stands seven minutes (Lifespan 420) on its four hued steps (ExodusAlterAddon). Each
// member reads the rite (ritual one) and uses the dagger (ritual two); the owner's "Begin the Ritual" context entry
// checks every member is within five tiles, has both rituals and wears the Robe of Rite, then carries the party to
// Ver Lor Reg (764, 640, 0 on Ilshenar) with their pets, burns the robes and deletes the tome two seconds later.
// Double-click shows the tome's text. Conversion notes: the ritual list and the owner are ServUO's runtime state (it
// saves neither; the owner is saved here so a load keeps the tome usable); the steps are a saved reference;
// GetContextMenuEntries takes a PooledRefList here and OnClick (from, target); HasUsedRite stands in for ServUO
// PlayerMobile.UseSummoningRite. Instances lists the live tomes (ServUO's Altar static is never assigned).

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Collections;
using Server.Commands;
using Server.ContextMenus;
using Server.Engines.Exodus;
using Server.Engines.PartySystem;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExodusTomeAltar : BaseDecayingItem
{
    public static readonly TimeSpan DeleteAfterRitual = TimeSpan.FromSeconds(2);

    public static List<ExodusTomeAltar> Instances { get; } = new();

    /// <summary>ServUO's static, never assigned by ServUO; read by the Summoning Altar deed.</summary>
    public static ExodusTomeAltar Altar { get; set; }

    [SerializableField(0, setter: "private")]
    private Item _steps;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private Mobile _owner;

    private readonly List<RitualArray> _rituals = new();

    [Constructible]
    public ExodusTomeAltar(Mobile from = null) : base(0x1C11)
    {
        Hue = 1943;
        Movable = false;
        LootType = LootType.Regular;
        Weight = 0.0;
        _owner = from;

        _steps = new ExodusAlterAddon { Movable = false };

        Instances.Add(this);
    }

    public override int LabelNumber => 1153602; // Exodus Summoning Tome

    public override int Lifespan => 420;
    public override bool UseSeconds => false;

    public List<RitualArray> Rituals => _rituals;

    public RitualArray FindRitual(Mobile m) => _rituals.Find(r => r.RitualMobile == m);

    public RitualArray AddRitual(Mobile m)
    {
        var ritual = new RitualArray { RitualMobile = m };
        _rituals.Add(ritual);
        return ritual;
    }

    /// <summary>ServUO PlayerMobile.UseSummoningRite: the rite is read on a live tome and the dagger not yet used there.</summary>
    public static bool HasUsedRite(Mobile m) =>
        Instances.Any(a => !a.Deleted && a.FindRitual(m) is { Ritual1: true, Ritual2: false });

    public override void GetContextMenuEntries(Mobile from, ref PooledRefList<ContextMenuEntry> list)
    {
        base.GetContextMenuEntries(from, ref list);
        list.Add(new BeginTheRitual(this, from));
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!from.HasGump<AltarGump>())
        {
            from.SendGump(new AltarGump());
        }
    }

    public override bool OnDragDrop(Mobile from, Item dropped) => false;

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        _steps?.Delete();
        Instances.Remove(this);

        if (Altar == this)
        {
            Altar = null;
        }
    }

    public override void OnMapChange()
    {
        base.OnMapChange();

        if (!Deleted && _steps != null)
        {
            _steps.Map = Map;
        }
    }

    public override void OnLocationChange(Point3D oldLoc)
    {
        base.OnLocationChange(oldLoc);

        if (!Deleted && _steps != null)
        {
            _steps.Location = new Point3D(X - 1, Y - 1, Z - 18);
        }
    }

    public bool CheckParty(Mobile from, Mobile m)
    {
        var party = Party.Get(from);

        if (party == null)
        {
            return false;
        }

        foreach (var info in party.Members)
        {
            if (info.Mobile == m)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>ServUO SendConfirmationsExodus. Returns true when the party was sent.</summary>
    public virtual bool SendConfirmationsExodus(Mobile from)
    {
        var party = Party.Get(from);

        if (party == null)
        {
            from.SendLocalizedMessage(1153596); // You must join a party with the players you wish to perform the ritual with.
            return false;
        }

        if (party.Members.Any(x => !from.InRange(x.Mobile, 5)))
        {
            from.SendLocalizedMessage(1153611); // One or more members of your party are not close enough to you to perform the ritual.
            return false;
        }

        foreach (var info in party.Members)
        {
            var robe = info.Mobile.FindItemOnLayer(Layer.OuterTorso) as RobeofRite;

            if (robe == null || FindRitual(info.Mobile) is not { Ritual1: true, Ritual2: true })
            {
                // ~1_PLAYER~ has not fulfilled all the requirements of the Ritual! You cannot commence until they do.
                from.SendLocalizedMessage(1153609, info.Mobile.Name);
                return false;
            }
        }

        var members = party.Members.Select(info => info.Mobile).ToList();

        foreach (var member in members)
        {
            SendBattleground(member);
        }

        return true;
    }

    /// <summary>ServUO SendBattleground: to Ilshenar with the pets, the robe burnt, the tome gone two seconds later.</summary>
    public virtual void SendBattleground(Mobile from)
    {
        if (VerLorRegController.Active && VerLorRegController.CurrentExodus != null && ExodusSummoningAlter.CheckExodus())
        {
            from.FixedParticles(0x376A, 9, 32, 0x13AF, EffectLayer.Waist);
            from.PlaySound(0x1FE);
            from.MoveToWorld(ExodusEncounter.BattlegroundArrival, Map.Ilshenar);
            BaseCreature.TeleportPets(from, ExodusEncounter.BattlegroundArrival, Map.Ilshenar);

            if (from.FindItemOnLayer(Layer.OuterTorso) is RobeofRite robe)
            {
                robe.Delete();
            }

            Timer.DelayCall(DeleteAfterRitual, Delete);
        }
        else
        {
            // The master of this realm has already been summoned and is engaged in combat. Your opportunity will come
            // after he has squashed the current batch of intruders!
            from.SendLocalizedMessage(1075213);
        }
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Instances.Add(this);
    }

    private class BeginTheRitual : ContextMenuEntry
    {
        private readonly ExodusTomeAltar _altar;

        public BeginTheRitual(ExodusTomeAltar altar, Mobile from) : base(1153608, 2) // Begin the Ritual
        {
            _altar = altar;

            if (altar.Owner != from)
            {
                Flags |= CMEFlags.Disabled;
            }
        }

        public override void OnClick(Mobile from, IEntity target)
        {
            if (_altar.Deleted)
            {
                return;
            }

            if (_altar.Owner == from)
            {
                _altar.SendConfirmationsExodus(from);
            }
            else
            {
                from.SendLocalizedMessage(1153610); // Only the altar owner can commence with the ritual.
            }
        }
    }
}

public class RitualArray
{
    public Mobile RitualMobile { get; set; }
    public bool Ritual1 { get; set; }
    public bool Ritual2 { get; set; }
}

/// <summary>ServUO AltarGump: the tome's text; [TomeAltarGump shows it to staff.</summary>
public class AltarGump : Gump
{
    public static void Initialize()
    {
        CommandSystem.Register("TomeAltarGump", AccessLevel.Administrator, TomeAltarGump_OnCommand);
    }

    [Usage("TomeAltarGump")]
    [Description("Shows the Summoning Tome's text gump (ServUO's debug command).")]
    [ShardCommand(CommandCategory.DevTool, Rerun = CommandRerun.Skips, Shard = CommandShard.Any)]
    public static void TomeAltarGump_OnCommand(CommandEventArgs e)
    {
        if (!e.Mobile.HasGump<AltarGump>())
        {
            e.Mobile.SendGump(new AltarGump());
        }
    }

    public AltarGump() : base(100, 100)
    {
        Closable = true;
        Disposable = true;
        Draggable = true;

        AddPage(0);
        AddBackground(0, 0, 447, 195, 5120);
        // Contained within this Tome is the ritual by which Lord Exodus may once again be called upon Britannia ...
        AddHtmlLocalized(17, 14, 412, 161, 1153607, 0x7FFF, false, false);
    }

    public override void OnResponse(NetState state, in RelayInfo info)
    {
    }
}
