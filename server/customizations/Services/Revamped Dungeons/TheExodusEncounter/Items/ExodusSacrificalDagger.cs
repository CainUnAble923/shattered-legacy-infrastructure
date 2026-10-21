// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Items/ExodusSacrificalDagger.cs (cc-P195): the blackrock
// dagger of the ritual. Worn in the hand with the Robe of Rite, in a party, after the rite has been read on a tome,
// it is thrust into the tome's owner's altar: the second of each member's two rituals, 10,000 karma, and the dagger
// is gone. Lives a week (ServUO's own count on a BaseKnife, the BaseDecayingItem shape; DecayingLifespan here).
// Conversion notes: ServUO's [Alterable(DefBlacksmithy, gargish)] has no pinned counterpart (the Arch Zealot swaps
// a human dagger for a gargish one by hand, as ServUO's does too); the gargish dagger's RequiredRace and
// CanBeWornByGargoyles are BaseClothing's only in pinned, so the gargish dagger is the same weapon with gargish art
// (D, notes). UseSummoningRite is read from the altars (ExodusTomeAltar.HasUsedRite).

using System;
using ModernUO.Serialization;
using Server.Engines.PartySystem;
using Server.Misc;
using Server.Targeting;

namespace Server.Items;

[Flippable(0x2D21, 0x2D2D)]
[SerializationGenerator(0, false)]
public partial class ExodusSacrificalDagger : BaseKnife
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _timeLeft;

    private Timer _timer;

    [Constructible]
    public ExodusSacrificalDagger() : base(0x2D2D)
    {
        Weight = 4.0;
        Layer = Layer.OneHanded;
        Hue = 2500;

        if (Lifespan > 0)
        {
            _timeLeft = Lifespan;
            StartTimer();
        }
    }

    public override int LabelNumber => 1153500; // exodus sacrificial dagger

    public virtual int Lifespan => 604800;

    public override int InitMinHits => 60;
    public override int InitMaxHits => 60;
    public override int AosStrengthReq => 15;
    public override SkillName DefSkill => SkillName.Fencing;
    public override float MlSpeed => 2.00f;
    public override int AosMinDamage => 10;
    public override int AosMaxDamage => 12;
    public override int PhysicalResistance => 12;

    public override void OnDoubleClick(Mobile from)
    {
        var robe = from.FindItemOnLayer(Layer.OuterTorso) as RobeofRite;
        var dagger = from.FindItemOnLayer(Layer.OneHanded) as ExodusSacrificalDagger;

        if (Party.Get(from) == null)
        {
            from.SendLocalizedMessage(1153596); // You must join a party with the players you wish to perform the ritual with.
        }
        else if (robe == null || dagger == null)
        {
            from.SendLocalizedMessage(1153591); // Thou art not properly attired to perform such a ritual.
        }
        else if (!ExodusTomeAltar.HasUsedRite(from))
        {
            from.SendLocalizedMessage(1153603); // You must first use the Summoning Rite on a Summoning Tome.
        }
        else
        {
            from.SendLocalizedMessage(1153604); // Target the summoning tome or yourself to declare your intentions for performing this ritual...
            from.Target = new SacrificalTarget(this);
        }
    }

    /// <summary>ServUO SacrificalTarget.OnTarget, for the facts and the target alike.</summary>
    public void Sacrifice(Mobile from, object targeted)
    {
        if (targeted is ExodusTomeAltar altar)
        {
            if (altar.CheckParty(altar.Owner, from))
            {
                var ritual = altar.FindRitual(from);

                if (ritual == null || !ritual.Ritual1)
                {
                    from.SendLocalizedMessage(1153603); // You must first use the Summoning Rite on a Summoning Tome.
                }
                else if (!ritual.Ritual2)
                {
                    from.Say(1153605); // *You thrust the dagger into your flesh as tribute to Exodus!*
                    ritual.Ritual2 = true;
                    Delete();
                    Titles.AwardKarma(from, 10000, true);
                    Effects.SendLocationParticles(
                        EffectItem.Create(altar.Location, altar.Map, TimeSpan.FromSeconds(2)), 0x373A, 10, 10, 2023
                    );

                    from.SendLocalizedMessage(1153598, from.Name); // ~1_PLAYER~ has read the Summoning Rite!
                }
                else
                {
                    from.SendLocalizedMessage(1153599); // You've already used this item in another ritual.
                }
            }
            else
            {
                from.SendLocalizedMessage(1153595); // You must first join the party of the person who built this altar.
            }
        }
        else
        {
            from.SendLocalizedMessage(1153601); // That is not a Summoning Tome.
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (Lifespan > 0)
        {
            DecayingLifespan.AddProperty(list, _timeLeft, false);
        }
    }

    public virtual void StartTimer()
    {
        if (_timer != null)
        {
            return;
        }

        _timer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), Slice);
    }

    public virtual void StopTimer()
    {
        _timer?.Stop();
        _timer = null;
    }

    public virtual void Slice()
    {
        TimeLeft -= 10;
        InvalidateProperties();

        if (TimeLeft <= 0)
        {
            Decay();
        }
    }

    public virtual void Decay()
    {
        DecayingLifespan.Expire(this);
        StopTimer();
        Delete();
    }

    public override void OnAfterDelete()
    {
        StopTimer();
        base.OnAfterDelete();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        StartTimer();
    }

    private class SacrificalTarget : Target
    {
        private readonly ExodusSacrificalDagger _dagger;

        public SacrificalTarget(ExodusSacrificalDagger dagger) : base(2, true, TargetFlags.None) => _dagger = dagger;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (!_dagger.Deleted)
            {
                _dagger.Sacrifice(from, targeted);
            }
        }
    }
}

[Flippable(0x0902, 0x406A)]
[SerializationGenerator(0, false)]
public partial class ExodusSacrificalGargishDagger : ExodusSacrificalDagger
{
    [Constructible]
    public ExodusSacrificalGargishDagger()
    {
        ItemID = 0x406A;
        Weight = 4.0;
    }
}
