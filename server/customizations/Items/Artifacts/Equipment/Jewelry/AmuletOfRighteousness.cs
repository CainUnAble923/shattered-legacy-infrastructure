using ModernUO.Serialization;
using Server.Targeting;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Equipment/Jewelry/AmuletOfRighteousness.cs (CC9). Bolt-effect
    // toy with 100 charges. ServUO's AddUsesRemainingProperties hook does not exist on
    // ModernUO's Item, so the "uses remaining" line is added from GetProperties instead, which
    // is where ModernUO's own IUsesRemaining items put it.
    [SerializationGenerator(0, false)]
    public partial class AmuletOfRighteousness : SilverNecklace, IUsesRemaining
    {
        [InvalidateProperties]
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _usesRemaining;

        [Constructible]
        public AmuletOfRighteousness(int uses = 100)
        {
            LootType = LootType.Blessed;
            _usesRemaining = uses;
        }

        public override double DefaultWeight => 1.0;

        public override int LabelNumber => 1075313; // Amulet of Righteousness

        public bool ShowUsesRemaining
        {
            get => true;
            set { }
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            list.Add(1060584, _usesRemaining); // uses remaining: ~1_val~
        }

        public override void OnDoubleClick(Mobile from)
        {
            base.OnDoubleClick(from);

            if (IsChildOf(from.Backpack))
            {
                from.Target = new InternalTarget(this);
            }
            else
            {
                from.SendLocalizedMessage(1062334); // This item must be in your backpack to be used.
            }
        }

        private class InternalTarget : Target
        {
            private readonly AmuletOfRighteousness _amulet;

            public InternalTarget(AmuletOfRighteousness amulet) : base(12, false, TargetFlags.None) =>
                _amulet = amulet;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (_amulet?.Deleted != false)
                {
                    return;
                }

                if (targeted is not Mobile target)
                {
                    return;
                }

                if (_amulet.UsesRemaining <= 0)
                {
                    from.SendLocalizedMessage(1042544); // This item is out of charges.
                    return;
                }

                target.BoltEffect(0);
                _amulet.UsesRemaining -= 1;
                _amulet.InvalidateProperties();
            }
        }
    }
}
