// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Items/ExodusChest.cs (cc-P195): the twelve hidden supply
// caches of the Exodus Dungeon (the Rogue's path to the ritual's keys). Invisible, locked at 90, poison-trapped,
// holding gold, gems and a few consumables; found by Detect Hidden (98 within three tiles, through
// server/patches/DetectHidden-revealable-items.patch) or sensed by a passer-by at 80 (one in 300 scaled by skill),
// and once revealed it drops a ritual key one time in five and vanishes five minutes later. Each chest keeps a 5x5
// region around itself whose OnEnter tells a 98-skill rogue something is hidden; here that region also runs ServUO's
// passive detect on every step (ServUO's PlayerMobile.OnMovement does it over 8 tiles; pinned's is not patched).
// Conversion notes: the stock DecorativeBox is 0x2DF4; ServUO's Initialize makes 0x2DF3 passable (kept, guarded for a
// test host with no tile data); the chest deletes itself on a load if it was unlocked (ServUO's Deserialize). The
// essence roll is over the shard's ten essences (ServUO Loot.RandomEssence; pinned has no essence helper).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Mobiles;
using Server.Regions;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ExodusChest : DecorativeBox, IRevealableItem
{
    public static readonly TimeSpan DeleteDelay = TimeSpan.FromMinutes(5);

    /// <summary>ServUO RituelItem: the four keys to the ritual; one is drawn at random where a key is given.</summary>
    public static Type[] RituelItem { get; } =
    {
        typeof(ExodusSummoningRite), typeof(ExodusSacrificalDagger), typeof(RobeofRite), typeof(ExodusSummoningAlter)
    };

    /// <summary>The shard's essences, for ServUO's Loot.RandomEssence (pinned has none).</summary>
    public static Type[] EssenceTypes { get; } =
    {
        typeof(EssenceAchievement), typeof(EssenceBalance), typeof(EssenceControl), typeof(EssenceDiligence),
        typeof(EssenceDirection), typeof(EssenceFeeling), typeof(EssenceOrder), typeof(EssencePassion),
        typeof(EssencePrecision), typeof(EssenceSingularity)
    };

    private Timer _timer;
    private ExodusChestRegion _region;

    [Constructible]
    public ExodusChest()
    {
        Visible = false;
        Locked = true;
        LockLevel = 90;
        RequiredSkill = 90;
        MaxLockLevel = 100;
        Weight = 0.0;
        Hue = 2700;
        Movable = false;

        TrapType = TrapType.PoisonTrap;
        TrapPower = 100;
        GenerateTreasure();
    }

    public static void Initialize()
    {
        // ServUO: the hidden chest's art does not block the tile.
        if (TileData.ItemTable.Length > 0x2DF3)
        {
            TileData.ItemTable[0x2DF3].Flags = TileFlag.None;
        }
    }

    public override int DefaultGumpID => 0x10C;

    public override bool IsDecoContainer => false;

    public bool CheckWhenHidden => true;

    public ExodusChestRegion ChestRegion => _region;

    public static Item RandomRitualItem() => Loot.Construct(RituelItem);

    public bool CheckReveal(Mobile m) => m.InRange(Location, 3) && m.Skills[SkillName.DetectHidden].Value >= 98.0;

    public virtual void OnRevealed(Mobile m)
    {
        Visible = true;
        StartDeleteTimer();
    }

    public virtual bool CheckPassiveDetect(Mobile m)
    {
        if (m.InRange(Location, 4))
        {
            var skill = (int)m.Skills[SkillName.DetectHidden].Value;

            if (skill >= 80 && Utility.Random(300) < skill)
            {
                return true;
            }
        }

        return false;
    }

    public void StartDeleteTimer()
    {
        if (Utility.RandomDouble() < 0.2)
        {
            var item = RandomRitualItem();

            if (item != null)
            {
                DropItem(item);
            }
        }

        _timer?.Stop();
        _timer = Timer.DelayCall(DeleteDelay, Delete);
    }

    public override void OnLocationChange(Point3D oldLoc)
    {
        base.OnLocationChange(oldLoc);

        if (!Deleted)
        {
            UpdateRegion();
        }
    }

    public override void OnMapChange()
    {
        base.OnMapChange();

        if (!Deleted)
        {
            UpdateRegion();
        }
    }

    public void UpdateRegion()
    {
        _region?.Unregister();
        _region = null;

        if (!Deleted && Map != null && Map != Map.Internal)
        {
            _region = new ExodusChestRegion(this);
            _region.Register();
        }
    }

    public override void OnAfterDelete()
    {
        _timer?.Stop();
        _timer = null;

        base.OnAfterDelete();

        UpdateRegion();
    }

    /// <summary>ServUO GenerateTreasure: 1,500-3,000 gold, one to five of every gem, and the consumables at their odds.</summary>
    protected virtual void GenerateTreasure()
    {
        DropItem(new Gold(1500, 3000));

        foreach (var gemType in Loot.GemTypes)
        {
            var gem = Loot.Construct(gemType);

            if (gem != null)
            {
                gem.Amount = Utility.Random(1, 6);
                DropItem(gem);
            }
        }

        if (Utility.RandomDouble() < 0.25)
        {
            DropStack(new SmokeBomb(), Utility.Random(3, 6));
        }

        if (Utility.RandomDouble() < 0.25)
        {
            Item potion = Utility.RandomBool() ? new ParasiticPotion() : new InvisibilityPotion();
            DropStack(potion, Utility.Random(1, 3));
        }

        if (Utility.RandomDouble() < 0.2)
        {
            var essence = Loot.Construct(EssenceTypes);

            if (essence != null)
            {
                essence.Amount = Utility.Random(3, 6);
                DropItem(essence);
            }
        }

        if (Utility.RandomDouble() < 0.1)
        {
            Item rare = Utility.Random(4) switch
            {
                0 => new Taint(),
                1 => new Corruption(),
                2 => new Blight(),
                _ => new LuminescentFungi()
            };

            DropItem(rare);
        }
    }

    // ServUO builds the stack through the amount constructor; pinned's bombs and potions stack only under ML.
    private void DropStack(Item item, int amount)
    {
        if (item.Stackable)
        {
            item.Amount = amount;
            DropItem(item);
            return;
        }

        DropItem(item);

        for (var i = 1; i < amount; i++)
        {
            DropItem((Item)Activator.CreateInstance(item.GetType()));
        }
    }

    /// <summary>ServUO GiveRituelItem: a random key straight into the pack, with the sound and the message.</summary>
    public static void GiveRituelItem(Mobile m)
    {
        var item = RandomRitualItem();
        m.PlaySound(0x5B4);

        if (item == null)
        {
            return;
        }

        m.AddToBackpack(item);
        m.SendLocalizedMessage(1072223); // An item has been placed in your backpack.
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        if (!Locked)
        {
            Timer.DelayCall(Delete);
            return;
        }

        Timer.DelayCall(UpdateRegion);
    }
}

/// <summary>ServUO ExodusChestRegion: the 5x5 around a hidden chest. Pinned: it also runs the passive detect on each step.</summary>
public class ExodusChestRegion : BaseRegion
{
    public ExodusChestRegion(ExodusChest chest)
        : base(null, chest.Map, Region.Find(chest.Location, chest.Map), new Rectangle2D(chest.Location.X - 2, chest.Location.Y - 2, 5, 5))
    {
        Chest = chest;
    }

    public ExodusChest Chest { get; }

    public override void OnEnter(Mobile m)
    {
        base.OnEnter(m);

        if (!Chest.Visible && m is PlayerMobile && m.Skills[SkillName.DetectHidden].Value >= 98.0)
        {
            m.SendLocalizedMessage(1153493); // Your keen senses detect something hidden in the area...
        }
    }

    public override void OnLocationChanged(Mobile m, Point3D oldLocation)
    {
        base.OnLocationChanged(m, oldLocation);

        if (!Chest.Deleted && !Chest.Visible && m is PlayerMobile && Chest.CheckPassiveDetect(m))
        {
            m.SendLocalizedMessage(1153493); // Your keen senses detect something hidden in the area...
        }
    }
}
