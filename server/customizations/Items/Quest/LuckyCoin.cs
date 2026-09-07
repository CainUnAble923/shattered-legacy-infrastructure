using ModernUO.Serialization;
using Server.Targeting;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class LuckyCoin : Item, ICommodity
    {
        [Constructible]
        public LuckyCoin(int amount = 1) : base(0xF87)
        {
            Stackable = true;
            Amount = amount;
            Hue = 1174;
        }

        public override int LabelNumber => 1113366; // lucky coin

        int ICommodity.DescriptionNumber => LabelNumber;
        bool ICommodity.IsDeedable => true;

        public override void OnDoubleClick(Mobile from)
        {
            if (IsChildOf(from.Backpack) && Amount >= 1)
            {
                from.SendLocalizedMessage(1113367); // Make a wish then toss me into sacred waters!!
                from.Target = new InternalTarget(this);
            }
        }

        private class InternalTarget : Target
        {
            private readonly LuckyCoin m_Coin;

            public InternalTarget(LuckyCoin coin) : base(3, false, TargetFlags.None) => m_Coin = coin;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (targeted is AddonComponent { Addon: FountainOfFortune fountain })
                {
                    fountain.OnTarget(from, m_Coin);
                }
                else
                {
                    from.SendLocalizedMessage(1113369); // That is not sacred waters. Try looking in the Underworld.
                }
            }
        }
    }
}
