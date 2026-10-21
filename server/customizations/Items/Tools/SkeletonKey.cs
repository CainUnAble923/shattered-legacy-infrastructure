// cc-P199: ServUO pub57 Scripts/Items/Tools/SkeletonKey.cs. The skeleton key (1095522, art 16650): a lockpick that is
// not stackable, never breaks, has 10 uses (one spent on each lock it opens, gone at 0) and lowers the Lockpicking a
// lock asks for by 10. The class keeps ServUO's spelling, Skeletonkey. MasterSkeletonKey.cs derives from it.
//
// ServUO's key overrides three virtuals on ServUO's Lockpick (IsSkeletonKey, SkillBonus, OnUse; ServUO
// Items/Consumables/LockPick.cs:21-22,66). Pinned's Lockpick has none of them, and its target and timer are private
// (Items/Skill Items/Thief/LockPick.cs:38-143), so the key cannot ride pinned's pick. It brings its own target and
// end-of-pick step instead, a copy of ServUO's Lockpick flow for a key (LockPick.cs:70-181):
//   - BeginLockpick (:70-89): a revamp treasure chest whose guardians are not all deleted refuses with 1115991; a locked
//     target plays 0x241 and resolves 200 ms later; an unlocked one says 502069.
//   - EndLockpick (:104-181): out of range does nothing; lock level 0 or -255 says 502073; Lockpicking below
//     RequiredSkill - SkillBonus says 502072; otherwise a master key always succeeds and a plain key rolls
//     CheckTargetSkill(LockLevel, MaxLockLevel). Success spends a use (OnUse), says 502076, plays 0x4A and picks the lock.
//     Failure says 502075 and never breaks the key (BrokeLockPickTest skips skeleton keys, :94); a treasure chest then
//     remembers the failed pick (revamp) or may lose one item (classic), as patches/LockPick-treasure-chest-failed-pick
//     makes pinned's Lockpick do.
// ServUO subtracts SkillBonus from the skill-check range only when the TARGET is a Skeletonkey (LockPick.cs:136,
// "lockpickable is Skeletonkey"), which a lock never is, so the range is the lock's own. Kept as ServUO behaves: the
// bonus only lowers the RequiredSkill gate.
//
// One difference a player can see: pinned's own Lockpick waits 3 seconds before resolving
// (LockPick.cs:82 here); ServUO's waits 200 ms, and so do these keys.

using System;
using ModernUO.Serialization;
using Server.Targeting;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class Skeletonkey : Lockpick
    {
        [SerializableField(0, fieldChanged: nameof(OnUsesChanged))]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        [InvalidateProperties]
        private int _uses;

        [Constructible]
        public Skeletonkey() : base(1)
        {
            Stackable = false;
            ItemID = 16650;
            _uses = 10;
        }

        public override int LabelNumber => 1095522; // skeleton key

        public virtual bool IsSkeletonKey => true;
        public virtual int SkillBonus => 10;

        // ServUO's Uses setter deletes the key when it reaches 0 (SkeletonKey.cs:16).
        private void OnUsesChanged(int oldValue, int newValue)
        {
            if (newValue <= 0)
            {
                Delete();
            }
        }

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            list.Add(1060584, _uses); // uses remaining: ~1_val~
        }

        public virtual void OnUse()
        {
            Uses--;
        }

        public override void OnDoubleClick(Mobile from)
        {
            from.SendLocalizedMessage(502068); // What do you want to pick?
            from.Target = new InternalTarget(this);
        }

        protected virtual void BeginLockpick(Mobile from, ILockpickable item)
        {
            if (item.Locked)
            {
                if (item is TreasureMapChest chest && TreasureMapInfo.NewSystem && !AllGuardiansDeleted(chest))
                {
                    // You must destroy all the guardians before you can unlock the chest.
                    from.SendLocalizedMessage(1115991);
                }
                else
                {
                    from.PlaySound(0x241);
                    Timer.DelayCall(TimeSpan.FromMilliseconds(200.0), () => EndLockpick(item, from));
                }
            }
            else
            {
                // The door is not locked
                from.SendLocalizedMessage(502069); // This does not appear to be locked
            }
        }

        private static bool AllGuardiansDeleted(TreasureMapChest chest)
        {
            var guardians = chest.Guardians;

            if (guardians == null)
            {
                return true;
            }

            for (var i = 0; i < guardians.Count; i++)
            {
                if (!guardians[i].Deleted)
                {
                    return false;
                }
            }

            return true;
        }

        protected virtual void EndLockpick(ILockpickable lockpickable, Mobile from)
        {
            if (Deleted || from.Deleted)
            {
                return;
            }

            var item = (Item)lockpickable;

            if (!from.InRange(item.GetWorldLocation(), 1))
            {
                return;
            }

            if (lockpickable.LockLevel is ILockpickable.CannotPick or ILockpickable.MagicLock)
            {
                // LockLevel of 0 means that the door can't be picklocked
                // LockLevel of -255 means it's magic locked
                item.SendLocalizedMessageTo(from, 502073); // This lock cannot be picked by normal means
                return;
            }

            if (from.Skills.Lockpicking.Value < lockpickable.RequiredSkill - SkillBonus)
            {
                // The LockLevel is higher thant the LockPicking of the player
                item.SendLocalizedMessageTo(from, 502072); // You don't see how that lock can be manipulated.
                return;
            }

            var maxLevel = lockpickable.MaxLockLevel;
            var minLevel = lockpickable.LockLevel;

            if (this is MasterSkeletonKey || from.CheckTargetSkill(SkillName.Lockpicking, lockpickable, minLevel, maxLevel))
            {
                // Success! Pick the lock!
                OnUse();

                item.SendLocalizedMessageTo(from, 502076); // The lock quickly yields to your skill.
                from.PlaySound(0x4A);
                lockpickable.LockPick(from);
            }
            else
            {
                // The player failed to pick the lock. A skeleton key never breaks (ServUO LockPick.cs:94).
                item.SendLocalizedMessageTo(from, 502075); // You are unable to pick the lock.

                if (item is TreasureMapChest chest)
                {
                    if (TreasureMapInfo.NewSystem)
                    {
                        chest.FailedLockpick = true;
                    }
                    else if (chest.Items.Count > 0 && 0.25 > Utility.RandomDouble())
                    {
                        var toBreak = chest.Items[Utility.Random(chest.Items.Count)];

                        if (toBreak is not Container)
                        {
                            toBreak.Delete();
                            Effects.PlaySound(item.Location, item.Map, 0x1DE);
                            from.SendMessage(0x20, "The sound of gas escaping is heard from the chest.");
                        }
                    }
                }
            }
        }

        private class InternalTarget : Target
        {
            private readonly Skeletonkey _key;

            public InternalTarget(Skeletonkey key) : base(1, false, TargetFlags.None) => _key = key;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (_key.Deleted)
                {
                    return;
                }

                if (targeted is ILockpickable lockpickable)
                {
                    _key.BeginLockpick(from, lockpickable);
                }
                else
                {
                    from.SendLocalizedMessage(501666); // You can't unlock that!
                }
            }
        }
    }
}
