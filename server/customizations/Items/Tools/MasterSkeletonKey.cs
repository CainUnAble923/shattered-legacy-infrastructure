// cc-P199: ServUO pub57 Scripts/Items/Tools/MasterSkeletonKey.cs. The master skeleton key (1095523): a skeleton key
// (SkeletonKey.cs here) whose bonus is 100, so any lock it can be used on (not 0 or magic-locked) opens on the first
// try, spending one of its 10 uses. The always-succeeds rule is the "this is MasterSkeletonKey" test in
// Skeletonkey.EndLockpick, as in ServUO's Lockpick (Items/Consumables/LockPick.cs:142).
//
// No deliberate differences from ServUO. ServUO's constructor sets Uses = 10 through the property
// (MasterSkeletonKey.cs:16); the base constructor already set it to 10, so this leaves it.

using ModernUO.Serialization;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class MasterSkeletonKey : Skeletonkey
    {
        [Constructible]
        public MasterSkeletonKey()
        {
        }

        public override int LabelNumber => 1095523; // master skeleton key

        public override bool IsSkeletonKey => true;
        public override int SkillBonus => 100;
    }
}
