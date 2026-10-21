// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Items/ExodusSummoningRite.cs (cc-P195): the rite a robed,
// dagger-bearing party member reads into the Summoning Tome, the first of the two rituals. Lives a week.

using ModernUO.Serialization;
using Server.Engines.PartySystem;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExodusSummoningRite : BaseDecayingItem
{
    [Constructible]
    public ExodusSummoningRite() : base(0x2258)
    {
        Weight = 1;
        Hue = 1910;
        LootType = LootType.Regular;
    }

    public override int LabelNumber => 1153498; // exodus summoning rite

    public override int Lifespan => 604800;
    public override bool UseSeconds => false;

    public override void OnDoubleClick(Mobile from)
    {
        var robe = from.FindItemOnLayer(Layer.OuterTorso) as RobeofRite;
        var dagger = from.FindItemOnLayer(Layer.OneHanded) as ExodusSacrificalDagger;

        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1054107); // This item must be in your backpack.
        }
        else if (Party.Get(from) == null)
        {
            from.SendLocalizedMessage(1153596); // You must join a party with the players you wish to perform the ritual with.
        }
        else if (robe == null || dagger == null)
        {
            from.SendLocalizedMessage(1153591); // Thou art not properly attired to perform such a ritual.
        }
        else
        {
            from.SendLocalizedMessage(1153600); // Which Summoning Tome do you wish to use this on?
            from.Target = new RiteTarget(this);
        }
    }

    /// <summary>ServUO RiteTarget.OnTarget, for the facts and the target alike.</summary>
    public void Read(Mobile from, object targeted)
    {
        if (targeted is ExodusTomeAltar altar)
        {
            if (altar.CheckParty(altar.Owner, from))
            {
                var ritual = altar.FindRitual(from) ?? altar.AddRitual(from);

                if (!ritual.Ritual1)
                {
                    from.Say(1153597); // You place the rite within the tome and begin to meditate...
                    ritual.Ritual1 = true;
                    Delete();
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

    private class RiteTarget : Target
    {
        private readonly ExodusSummoningRite _rite;

        public RiteTarget(ExodusSummoningRite rite) : base(2, true, TargetFlags.None) => _rite = rite;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (!_rite.Deleted)
            {
                _rite.Read(from, targeted);
            }
        }
    }
}
