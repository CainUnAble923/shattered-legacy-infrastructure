using System;
using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/GrapeVine.cs (CC9 batch 5). A locked-down vine (two of the ten graphics)
    // yields a GrapeBunch (batch 1) every four hours. The next-harvest time is the one persisted field.
    // Dropped: IsArtifact (D-1).
    [SerializationGenerator(0, false)]
    public partial class GrapeVine : Item
    {
        private static readonly TimeSpan HarvestWait = TimeSpan.FromHours(4);

        [DeltaDateTime]
        [SerializableField(0)]
        private DateTime _nextHarvest;

        [Constructible]
        public GrapeVine() : base(Utility.Random(3355, 10)) => _nextHarvest = Core.Now;

        public override int LabelNumber => 1149954;

        public override void OnDoubleClick(Mobile from)
        {
            if ((ItemID == 3358 || ItemID == 3363) && !Movable && _nextHarvest < Core.Now)
            {
                from.AddToBackpack(new GrapeBunch());
                NextHarvest = Core.Now + HarvestWait;
            }
        }
    }
}
