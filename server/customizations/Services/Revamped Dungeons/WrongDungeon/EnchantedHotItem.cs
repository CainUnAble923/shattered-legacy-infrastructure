// ServUO: Services/Revamped Dungeons/WrongDungeon/EnchantedHotItem.cs (cc-P194), plus the socket it attaches,
// Items/Internal/ItemSockets/EnchantedHotItem.cs (EnchantedHotItemSocket).
//
// Seven locked chests ("hot item chests", 92 Lockpicking) stand in the level-3 jail at ServUO's seven tiles.
// Each holds a spell scroll and one to three pieces of magic equipment hued 1258; picking the lock re-locks and
// restocks the chest ten minutes later. Every piece taken is a "hot item": while its holder is in Wrong it burns
// 10-13 fire damage every three seconds ("Ouch! These stolen items are hot!" one tick in five), and the curse is
// lifted the moment the holder leaves the dungeon ("The curse is removed from the item you stole!").
//
// Pinned ModernUO has no item sockets (ServUO's ItemSocket, GetSocket, AttachSocket). The curse lives here
// instead: a registry of item -> chest, ticked by one three-second timer and saved with the world
// (GenericPersistence "EnchantedHotItem", the shape of ServUO's own pre-socket save: version 0 wrote item, chest
// pairs). Two pieces of the socket could not come across without patching stock types and are recorded as
// deviations in notes/cc-P194-port-wrong-and-covetous-void.md: the two property lines ("Enchanted Hot Item",
// "(Escape from dungeon to remove spell)") on the stolen piece, and the return to the trunk when it is dropped
// anywhere but the holder's pack (ServUO hooks Container.OnDragDrop and the four equipment types' OnRemoved).
// Leaving the dungeon is detected by the tick (within three seconds) rather than ServUO's OnEnterRegion event.
//
// Also not here: RunicReforging.GenerateRandomItem (the chest loot is plain magic loot, the D-38 shape from
// Shame's chests) and ExtendedWeaponAttributes.AssassinHoned (no pinned member).

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Items;

public static class EnchantedHotItem
{
    public static readonly TimeSpan TickDuration = TimeSpan.FromSeconds(3);

    public static readonly Point3D[] ChestLocs =
    {
        new(5703, 664, 0), new(5703, 663, 0), new(5703, 662, 0), new(5703, 661, 0),
        new(5707, 659, 0), new(5708, 659, 0), new(5709, 659, 0)
    };

    private static readonly Dictionary<Item, HotItemChest> _hotItems = new();
    private static Timer _timer;
    private static EnchantedHotItemPersistence _persistence;

    public static IReadOnlyDictionary<Item, HotItemChest> HotItems => _hotItems;

    public static void Configure()
    {
        _persistence = new EnchantedHotItemPersistence();
    }

    public static bool IsHot(Item item) => item != null && _hotItems.ContainsKey(item);

    public static HotItemChest GetChest(Item item) => item == null ? null : _hotItems.GetValueOrDefault(item);

    /// <summary>ServUO: item.AttachSocket(new EnchantedHotItemSocket(chest)).</summary>
    public static void Attach(Item item, HotItemChest chest)
    {
        if (item == null || item.Deleted)
        {
            return;
        }

        _hotItems[item] = chest;
        EnsureTimer();
    }

    /// <summary>ServUO: EnchantedHotItemSocket.Remove, with OnRemoved restoring the hue.</summary>
    public static bool Remove(Item item)
    {
        if (item == null || !_hotItems.Remove(item))
        {
            return false;
        }

        if (!item.Deleted)
        {
            item.Hue = item is BaseWeapon weapon ? weapon.GetElementalDamageHue() : 0;
        }

        return true;
    }

    private static void EnsureTimer()
    {
        if (_timer == null && _hotItems.Count > 0)
        {
            _timer = Timer.DelayCall(TickDuration, TickDuration, OnTick);
        }
    }

    public static void OnTick()
    {
        if (_hotItems.Count == 0)
        {
            _timer?.Stop();
            _timer = null;
            return;
        }

        foreach (var item in _hotItems.Keys.ToList())
        {
            if (item.Deleted)
            {
                _hotItems.Remove(item);
                continue;
            }

            if (item.RootParent is not Mobile holder)
            {
                continue;
            }

            if (holder.Region.IsPartOf("Wrong"))
            {
                AOS.Damage(holder, Utility.RandomMinMax(10, 13), 0, 100, 0, 0, 0);

                if (0.2 > Utility.RandomDouble())
                {
                    holder.SendLocalizedMessage(1152086); // Ouch! These stolen items are hot!
                }
            }
            else if (holder.Player)
            {
                // ServUO: EnchantedHotItemSocket.OnEnterRegion, when the holder steps out of Wrong.
                Remove(item);
                holder.SendLocalizedMessage(1152085); // The curse is removed from the item you stole!
            }
        }
    }

    /// <summary>Replaces any MetalGoldenChest within 15 tiles of the jail with the seven hot-item chests.</summary>
    public static List<HotItemChest> SpawnChests(Map map)
    {
        var old = new List<Item>();

        foreach (var item in map.GetItemsInRange(new Point3D(5707, 662, 0), 15))
        {
            if (item is MetalGoldenChest or HotItemChest)
            {
                old.Add(item);
            }
        }

        foreach (var item in old)
        {
            item.Delete();
        }

        var chests = new List<HotItemChest>(ChestLocs.Length);

        for (var i = 0; i < ChestLocs.Length; i++)
        {
            var chest = new HotItemChest(i <= 3 ? 3648 : 3649);

            WeakEntityCollection.Add(Engines.WrongRevamp.WrongGenerator.CollectionKey, chest);
            chest.MoveToWorld(ChestLocs[i], map);
            chest.SpawnLoot();
            chests.Add(chest);
        }

        return chests;
    }

    public static void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(0); // version

        var live = _hotItems.Where(kv => kv.Key?.Deleted == false).ToList();
        writer.WriteEncodedInt(live.Count);

        foreach (var (item, chest) in live)
        {
            writer.Write(item);
            writer.Write(chest);
        }
    }

    public static void Deserialize(IGenericReader reader)
    {
        reader.ReadEncodedInt(); // version

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var item = reader.ReadEntity<Item>();
            var chest = reader.ReadEntity<HotItemChest>();

            if (item?.Deleted == false)
            {
                _hotItems[item] = chest;
            }
        }

        EnsureTimer();
    }
}

public sealed class EnchantedHotItemPersistence : GenericPersistence
{
    public EnchantedHotItemPersistence() : base("EnchantedHotItem", 10)
    {
    }

    public override void Serialize(IGenericWriter writer) => EnchantedHotItem.Serialize(writer);

    public override void Deserialize(IGenericReader reader) => EnchantedHotItem.Deserialize(reader);
}

[SerializationGenerator(0, false)]
public partial class HotItemChest : LockableContainer
{
    public HotItemChest(int itemID) : base(itemID)
    {
        Movable = false;
        RequiredSkill = 92;
        LockLevel = RequiredSkill - Utility.Random(1, 10);
        MaxLockLevel = RequiredSkill;

        LiftOverride = true;
        Locked = true;
    }

    public void SpawnLoot()
    {
        foreach (var old in Items.ToList())
        {
            EnchantedHotItem.Remove(old);
            old.Delete();
        }

        var toSpawn = Utility.RandomMinMax(2, 4);

        for (var i = 0; i < toSpawn; i++)
        {
            Item item;

            if (i == 0)
            {
                // ServUO passes RegularScrollTypes.Length as the inclusive maximum (EnchantedHotItem.cs:117), so one
                // chest in 65 rolls past the table and gets no scroll (Construct returns null out of range on both
                // sides). Length - 1 is the last scroll; the D-4 rule, a bug that leaves the chest short is fixed.
                item = Loot.RandomScroll(0, Loot.RegularScrollTypes.Length - 1, SpellbookType.Regular);
            }
            else
            {
                item = Loot.RandomArmorOrShieldOrWeaponOrJewelry();

                if (item == null)
                {
                    continue;
                }

                item.Hue = 1258;
                EnchantedHotItem.Attach(item, this);
            }

            if (item != null)
            {
                DropItem(item);
            }
        }
    }

    public override void LockPick(Mobile from)
    {
        base.LockPick(from);

        Timer.DelayCall(
            TimeSpan.FromMinutes(10),
            () =>
            {
                if (Deleted)
                {
                    return;
                }

                Visible = false;
                Visible = true;

                Locked = true;

                SpawnLoot();
            }
        );
    }

    // ServUO: a chest found unlocked at load locks itself again five minutes later.
    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (!Locked)
        {
            Timer.DelayCall(TimeSpan.FromMinutes(5), () => Locked = true);
        }
    }
}
