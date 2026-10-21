// cc-P199: ServUO pub57 Scripts/Items/Functional/CylinderTrapTrainingKit.cs. A Khaldun puzzle chest that never opens or
// hurts: Remove Trap on it opens the cylinder puzzle (pinned's PuzzleChest gump, through
// patches/PuzzleChest-trap-training-kit.patch); the right five cylinders roll Remove Trap at 0-100 and set a new solution.
// A wrong guess shows the usual status and does nothing else.
//
// One change from ServUO, invisible to a player: no treasure is generated inside. ServUO's kit inherits the puzzle
// chest's 600-900 gold and loot, locked in a chest nobody can open.

using ModernUO.Serialization;
using Server.SkillHandlers;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class CylinderTrapTrainingKit : PuzzleChest, IRemoveTrapTrainingKit
    {
        [Constructible]
        public CylinderTrapTrainingKit() : base(41875)
        {
            Solution = new PuzzleChestSolution(this);
            Movable = true;
        }

        public override int LabelNumber => 1159015; // Cylinder Trap Training Kit

        public int Title => 1159017; // A Cylinder Trap

        protected override void GenerateTreasure()
        {
        }

        public override void DisplayTo(Mobile to)
        {
        }

        public override bool CheckLocked(Mobile from) => false;

        public void OnRemoveTrap(Mobile from)
        {
            // ServUO closes the puzzle and status gumps first; pinned's are both singletons, so a new one replaces them.
            SendPuzzleGump(from);
        }

        public override void OnWrongGuess(Mobile m)
        {
        }

        public override void OnDoubleClick(Mobile m)
        {
            if (m.InRange(GetWorldLocation(), 1))
            {
                // That appears to be trapped, using the remove trap skill would yield better results...
                m.SendLocalizedMessage(1159008);
            }
        }

        public override void LockPick(Mobile from)
        {
            from.SendLocalizedMessage(1159009); // You successfully disarm the trap!

            from.CheckTargetSkill(SkillName.RemoveTrap, this, 0, 100);

            Solution = new PuzzleChestSolution(this);
        }
    }
}
