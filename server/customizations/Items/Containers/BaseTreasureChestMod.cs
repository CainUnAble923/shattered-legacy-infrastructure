// ServUO: Items/Containers/BaseTreasureChestMod.cs (CC4 Shame) - "Treasure Chest Pack 0.99H by Nerun".
//
// Ported because 116 of the 194 rows in RevampedSpawns/ShameRevamped.xml spawn TreasureLevel1-4, which derive
// from this. ModernUO has BaseTreasureChest (gold only, re-locks itself after 10-60 minutes); this pack is a
// different thing - a locked, trapped chest that carries a level-graded loot table, and deletes itself a
// few minutes after being opened so its spawner replaces it - and it is what ServUO's Shame spawn data asks
// for, so it is carried as is. Only the four levels the XML names are ported (TreasureChestMod.cs).
//
// Two hooks are absent here and dropped, both recorded in notes/cc4-shame.md:
//   RefinementComponent.Roll(this, 1, 0.08)   the SA armour-refinement drop; Services/Refinement is not here
//   RunicReforging.GenerateRandomItem(item...) in AddLoot; RunicReforging (3,391 lines) is the Blackthorn
//                                            decision, so AddLoot drops the plain item.
//
// ServUO's delete timer: first opened, the chest deletes itself after Utility.Random(2, 5) = 2-6 minutes.
// Re-opening sets ChestTimer.Delay to 1-2 s and calls Start(), which RunUO ignores on a running timer, so the
// first delay stands; reproduced as "start the timer once".

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public abstract partial class BaseTreasureChestMod : LockableContainer
{
    private TimerExecutionToken _deleteTimer;

    public BaseTreasureChestMod(int itemID) : base(itemID)
    {
        Locked = true;
        Movable = false;

        FindItemByType(typeof(Key))?.Delete();
    }

    public override int DefaultGumpID => 0x42;
    public override int DefaultDropSound => 0x42;
    public override Rectangle2D Bounds => new(20, 105, 150, 180);
    public override bool IsDecoContainer => false;

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (!Locked)
        {
            StartDeleteTimer();
        }
    }

    public override void OnTelekinesis(Mobile from)
    {
        if (CheckLocked(from))
        {
            Effects.SendLocationParticles(EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x376A, 9, 32, 5022);
            Effects.PlaySound(Location, Map, 0x1F5);
            return;
        }

        base.OnTelekinesis(from);
        Name = "a treasure chest";
        StartDeleteTimer();
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (CheckLocked(from))
        {
            return;
        }

        base.OnDoubleClick(from);
        Name = "a treasure chest";
        StartDeleteTimer();
    }

    /// <summary>
    ///     ServUO reforges the item here when RandomItemGenerator is enabled. Not here; the item drops plain.
    /// </summary>
    protected void AddLoot(Item item)
    {
        if (item == null)
        {
            return;
        }

        DropItem(item);
    }

    public bool DeleteTimerRunning => _deleteTimer.Running;

    private void StartDeleteTimer()
    {
        if (_deleteTimer.Running)
        {
            return;
        }

        Timer.StartTimer(TimeSpan.FromMinutes(Utility.Random(2, 5)), Delete, out _deleteTimer);
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        _deleteTimer.Cancel();
    }
}
