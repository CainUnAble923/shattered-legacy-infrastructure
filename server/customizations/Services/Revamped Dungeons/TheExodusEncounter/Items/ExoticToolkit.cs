// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Items/ExoticToolkit.cs (cc-P195): the kit an adept tinker makes
// from a Mechanical Component; used on a broken Nexus machine it opens the circuit puzzle. Lives a week.

using ModernUO.Serialization;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExoticToolkit : BaseDecayingItem
{
    [Constructible]
    public ExoticToolkit() : base(0x1EB9)
    {
        Hue = 2500;
        Weight = 1;
    }

    public override int Lifespan => 604800;
    public override bool UseSeconds => false;

    public override int LabelNumber => 1153866; // Exotic Toolkit

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(500325); // I am too far away to do that.
        }
        else
        {
            from.Target = new InternalTarget(this);
            from.SendLocalizedMessage(1152378); // Use this on a broken Nexus.
        }
    }

    /// <summary>ServUO InternalTarget.OnTarget: a Nexus piece whose machine is broken opens its puzzle.</summary>
    public void Use(Mobile from, object targeted)
    {
        if (targeted is NexusComponent { Addon: ExodusNexus { Active: false } nexus })
        {
            nexus.OpenGump(from);
        }
    }

    private class InternalTarget : Target
    {
        private readonly ExoticToolkit _toolkit;

        public InternalTarget(ExoticToolkit toolkit) : base(-1, true, TargetFlags.None) => _toolkit = toolkit;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (!_toolkit.Deleted)
            {
                _toolkit.Use(from, targeted);
            }
        }
    }
}
