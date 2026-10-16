// cc-P128: ServUO pub57 Scripts/Items/Consumables/PoppiesDust.cs -> ours. Poppies dust (SA): eight charges; each one
// identifies an unidentified seed (ShowType), and two dusts combine their charges.

using ModernUO.Serialization;
using Server.Engines.Plants;
using Server.Targeting;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class PoppiesDust : Item, IUsesRemaining
{
    [SerializableField(0)]
    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _usesRemaining;

    [Constructible]
    public PoppiesDust(int charges = 8) : base(0xC4D)
    {
        Weight = 1.0;
        Stackable = false;
        _usesRemaining = charges;
    }

    public override int LabelNumber => 1095223; // poppies dust

    public virtual bool ShowUsesRemaining
    {
        get => true;
        set
        {
        }
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060741, $"{_usesRemaining}"); // charges: ~1_val~
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042664); // You must have the object in your backpack to use it.
            return;
        }

        from.Target = new InternalTarget(this);
        from.SendLocalizedMessage(500343); // What do you wish to appraise and identify?
    }

    public void Use(Mobile from, object targeted)
    {
        if (Deleted || UsesRemaining <= 0)
        {
            return;
        }

        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042664); // You must have the object in your backpack to use it.
        }
        else if (targeted is PoppiesDust pd && pd != this)
        {
            pd.UsesRemaining += UsesRemaining;
            Delete();

            from.SendLocalizedMessage(1158448); // You combine the charges on the items.
        }
        else if (targeted is Seed seed)
        {
            if (seed.ShowType)
            {
                from.SendLocalizedMessage(1114369, "", 946); // This seed has already been identified.
                return;
            }

            seed.ShowType = true;

            UsesRemaining--;
        }
    }

    private class InternalTarget : Target
    {
        private readonly PoppiesDust _dust;

        public InternalTarget(PoppiesDust dust) : base(3, false, TargetFlags.None) => _dust = dust;

        protected override void OnTarget(Mobile from, object targeted) => _dust.Use(from, targeted);
    }
}
