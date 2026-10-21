// ServUO: Services/Revamped Dungeons/WrongDungeon/Items/WrongBarredMetalDoor.cs (cc-P194).
//
// The 34 locked doors of the level-3 maze. Lockpicking (80 to 110, 100 required) opens one for ten seconds, then
// it locks again; Telekinesis cast by a mage of 100 Magery or more does the same; anyone else gets "It appears to
// be locked." ServUO's IMageUnlockable (the Unlock spell at 100 Magery) has no counterpart: pinned Unlock.cs targets
// LockableContainers only (Spells/Third/Unlock.cs:45), so OnMageUnlock is kept here but nothing calls it, a
// deviation recorded in the notes. A door loaded from a save is locked, as on ServUO.
//
// Conversion notes: pinned ILockpickable (Items/Skill Items/Thief/LockPick.cs:7) wants LockLevel, Locked, Picker,
// MaxLockLevel, RequiredSkill and LockPick(Mobile); BaseDoor already carries Locked. The relock timer is
// Timer.DelayCall; ServUO's InternalTimer with a priority has no priority here.

using System;
using ModernUO.Serialization;
using Server.Network;
using Server.Spells.Third;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class WrongBarredMetalDoor : BaseDoor, ILockpickable, ITelekinesisable
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _requiredSkill;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _maxLockLevel;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _lockLevel;

    private Timer _timer;
    private bool _magicUnlocked;

    [Constructible]
    public WrongBarredMetalDoor(DoorFacing facing)
        : base(0x685 + 2 * (int)facing, 0x686 + 2 * (int)facing, 0xEC, 0xF3, GetOffset(facing))
    {
        Locked = true;
        _lockLevel = 80;
        _maxLockLevel = 110;
        _requiredSkill = 100;
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public Mobile Picker { get; set; }

    public virtual void LockPick(Mobile from)
    {
        Picker = from;
        Locked = false;
        BeginRelock();
    }

    private void BeginRelock()
    {
        _timer?.Stop();
        _timer = Timer.DelayCall(TimeSpan.FromSeconds(10.0), () => Locked = true);
    }

    // Pinned BaseDoor implements ITelekinesisable.OnTelekinesis without a virtual (BaseDoor.cs:107), so the
    // interface is re-implemented here and the spell (Spells/Third/Telekinesis.cs:36) reaches this one.
    public new void OnTelekinesis(Mobile from)
    {
        BeginRelock();

        if (from.Skills.Magery.Value >= _requiredSkill)
        {
            _magicUnlocked = true;
        }

        Use(from);
    }

    // ServUO: IMageUnlockable.OnMageUnlock, called by its Unlock spell. No caller on this shard (see the header).
    public void OnMageUnlock(Mobile from)
    {
        BeginRelock();

        if (from.Skills.Magery.Value >= _requiredSkill)
        {
            _magicUnlocked = true;
        }
    }

    public override void Use(Mobile from)
    {
        if (_magicUnlocked)
        {
            Locked = false;
            _magicUnlocked = false;
        }

        if (Locked && !Open)
        {
            from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 501746); // It appears to be locked.
            return;
        }

        base.Use(from);
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Locked = true;
    }
}
