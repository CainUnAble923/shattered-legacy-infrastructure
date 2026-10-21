// cc-P199: ServUO pub57 Scripts/Items/Functional/CircuitTrapTrainingKit.cs. Use Remove Trap on it (the
// IRemoveTrapTrainingKit branch of patches/RemoveTrap-trap-training-kits.patch) and walk the circuit; finishing it rolls
// Remove Trap at 0-100. The grid is 9 below 80 skill, 16 below 100, 25 from 100.
//
// One change from ServUO: the grid size is saved with the path. ServUO saves the path and progress but not the size, so
// a kit reloaded mid-circuit had a size of 0 and drew no grid until the path was finished another way.

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.SkillHandlers;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class CircuitTrapTrainingKit : Item, ICircuitTrap, IRemoveTrapTrainingKit
    {
        [SerializableField(0, setter: "private")]
        private CircuitCount _count;

        [SerializableField(1)]
        private List<int> _path = [];

        [SerializableField(2)]
        private List<int> _progress = [];

        [Constructible]
        public CircuitTrapTrainingKit() : base(41875)
        {
        }

        public override int LabelNumber => 1159014; // Circuit Trap Training Kit

        public int GumpTitle => 1159005;       // <center>Trap Disarm Mechanism</center>
        public int GumpDescription => 1159006; // <center>disarm the trap</center>

        public bool CanDecipher => false;

        public override void OnDoubleClick(Mobile m)
        {
            if (m.InRange(GetWorldLocation(), 1))
            {
                // That appears to be trapped, using the remove trap skill would yield better results...
                m.SendLocalizedMessage(1159008);
            }
        }

        public void OnRemoveTrap(Mobile m)
        {
            if (m is PlayerMobile pm)
            {
                if (_path == null || _path.Count == 0)
                {
                    var skill = m.Skills.RemoveTrap.Base;

                    Count = skill switch
                    {
                        < 80.0  => CircuitCount.Nine,
                        < 100.0 => CircuitCount.Sixteen,
                        _       => CircuitCount.TwentyFive
                    };
                }

                pm.SendGump(new CircuitTrapGump(pm, this));
            }
        }

        public void OnSelfClose(Mobile m)
        {
            _progress?.Clear();
        }

        public void OnProgress(Mobile m, int pick)
        {
            m.SendSound(0x1F4);
        }

        public void OnFailed(Mobile m)
        {
            m.SendLocalizedMessage(1159013); // You fail to disarm the trap and reset it.
        }

        public void OnComplete(Mobile m)
        {
            m.SendLocalizedMessage(1159009); // You successfully disarm the trap!

            m.CheckTargetSkill(SkillName.RemoveTrap, this, 0, 100);
        }

        public override void OnDelete()
        {
            if (RootParent is Mobile m && m.HasGump<CircuitTrapGump>())
            {
                m.CloseGump<CircuitTrapGump>();
            }

            base.OnDelete();
        }
    }
}
