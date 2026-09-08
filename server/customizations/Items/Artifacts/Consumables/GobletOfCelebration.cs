using System;
using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Consumables/GobletOfCelebration.cs (CC9). One drink a day; the
    // refill timer is persisted as the refill time and rebuilt after load, as ServUO does.
    [SerializationGenerator(0, false)]
    public partial class GobletOfCelebration : Item
    {
        [DeltaDateTime]
        [SerializableField(1)]
        private DateTime _nextFill;

        private Timer _fillTimer;

        [Constructible]
        public GobletOfCelebration() : base(0x99A)
        {
            LootType = LootType.Blessed;
            Hue = 19;
            _full = true;
        }

        public override double DefaultWeight => 1.0;

        public override int LabelNumber => 1075430; // Goblet of Celebration

        [SerializableProperty(0)]
        [CommandProperty(AccessLevel.GameMaster)]
        public bool Full
        {
            get => _full;
            set
            {
                if (_full == value)
                {
                    return;
                }

                _full = value;
                InvalidateProperties();
                this.MarkDirty();

                _fillTimer?.Stop();

                if (!_full)
                {
                    NextFill = Core.Now + TimeSpan.FromDays(1.0);
                    _fillTimer = Timer.DelayCall(TimeSpan.FromDays(1.0), () => Full = true);
                }
            }
        }

        public override void OnDoubleClick(Mobile from)
        {
            base.OnDoubleClick(from);

            if (!IsChildOf(from.Backpack))
            {
                // You can only drink from the goblet of celebration when its in your inventory.
                from.SendLocalizedMessage(1075438);
            }
            else if (_full)
            {
                from.SendLocalizedMessage(1075272); // You drink from the goblet of celebration

                Full = false;

                from.BAC = 60;
                BaseBeverage.CheckHeaveTimer(from);
            }
            else
            {
                // You need to wait a day for the goblet of celebration to be replenished.
                from.SendLocalizedMessage(1075439);
            }
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            list.Add(_full ? 1042972 : 1042975); // It's full. / It's empty.
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            if (_full)
            {
                return;
            }

            var delay = _nextFill - Core.Now;

            if (delay < TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            _fillTimer = Timer.DelayCall(delay, () => Full = true);
        }
    }
}
