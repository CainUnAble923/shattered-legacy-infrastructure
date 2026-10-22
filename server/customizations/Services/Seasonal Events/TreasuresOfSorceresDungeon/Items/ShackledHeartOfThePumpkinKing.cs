// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Items/ShackledHeartOfThePumpkinKing.cs (cc-P408), a
// Treasures of Sorcerer's Dungeon reward (20 points). Double-click: "Thou shall know the pain of a chained heart..."
// and the user bleeds for about ten seconds (a drop of blood and a sound every two seconds, five times), harmlessly.
//
// The bleed timer is not saved in ServUO and is not here. Blood with a chosen art is pinned's Blood(int itemID).

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ShackledHeartOfThePumpkinKing : Item
{
    [Constructible]
    public ShackledHeartOfThePumpkinKing() : base(0x4A9C)
    {
    }

    public override int LabelNumber => 1157653; // Shackled Heart of the Pumpkin King

    private Timer BleedTimer { get; set; }

    public override void OnDoubleClick(Mobile m)
    {
        if (BleedTimer == null)
        {
            m.SendLocalizedMessage(1157631); // Thou shall know the pain of a chained heart...
            m.SendSound(Utility.RandomMinMax(0x423, 0x427));

            BleedTimer = new InternalTimer(m, this);
        }
        else
        {
            m.SendLocalizedMessage(1157630); // You are already bleeding!
        }
    }

    private class InternalTimer : Timer
    {
        private readonly Mobile _owner;
        private readonly ShackledHeartOfThePumpkinKing _heart;
        private int _ticks;

        public InternalTimer(Mobile m, ShackledHeartOfThePumpkinKing heart)
            : base(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2))
        {
            _owner = m;
            _heart = heart;

            Start();
        }

        protected override void OnTick()
        {
            if (!_owner.Alive || _owner.Map == Map.Internal || _ticks++ >= 5)
            {
                Stop();
                _heart.BleedTimer = null;
            }
            else
            {
                _owner.PlaySound(0x133);

                var blood = new Blood(Utility.Random(0x122A, 5));
                blood.MoveToWorld(_owner.Location, _owner.Map);
            }
        }
    }
}
