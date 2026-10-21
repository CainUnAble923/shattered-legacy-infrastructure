// cc-P242: loot that flies out of the box. Notes: shard-migration notes/cc-P242-loot-box-flying-loot.md.
//
// On a successful open (LootBox.Open), the best item the box held pops out over the opener: a location effect of one of
// nine animations from Clint Bellanger's Flying Loot set (CC-BY 3.0, reworked to UO art: our player package's art
// 15536-15589, six frames each, the base ID of each carrying our tiledata and animdata record). It plays at once, as the
// box opens; P79's fanfare follows when its merge window closes, unchanged. A tier 4+ box (a major fanfare) shows it to
// everyone nearby, a smaller box to the opener alone, as the fanfare does. A player who muted their fanfare
// ([FanfareMute) sees none, and clusterf.lootBoxes.flyingLoot = false turns it off for everyone. Nothing here changes
// what a box holds or hands over.
//
// Kind by the item handed over (not by the pool it came from), best first: gear (weapons, armor, shields, jewelry), then
// clothing (the box cosmetics), then gems, then potions. Everything else a box gives (gold, Renown, bandages, resources,
// reagents, scrolls, maps, recipes, placeholders, components, a box in the box) has no animation: no effect, the fanfare
// alone (the stated fallback).
//
// Timing: TazUO plays an effect's animdata frames at FrameInterval x speed x 10 ms each, loops them, and ends the effect
// after duration x 50 ms (GameEffect.cs, EffectManager.cs). Our animdata is interval 1 with the landed frame held twice
// (8 frames), so speed 10 and duration 16 play the flight exactly once: 800 ms.

#nullable enable

using System;
using System.Collections.Generic;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.LootBoxes;

public enum FlyingLootKind
{
    None,
    Blade,
    Bow,
    Staff,
    Armor,
    Shield,
    Jewelry,
    Gem,
    Potion,
    Clothing
}

/// <summary>The server's copy of the nine records.json records (the package fact keeps the two the same).</summary>
public sealed record FlyingLootRecord(FlyingLootKind Kind, int BaseId, string Name);

public static class ClusterFFlyingLoot
{
    public const string SettingKey = "clusterf.lootBoxes.flyingLoot";

    public static bool Enabled => ServerConfiguration.GetOrUpdateSetting(SettingKey, true);

    public const int FrameCount = 6;       // the author's six frames, art BaseId + 0 to + 5
    public const int AnimFrames = 8;       // animdata: 0 to 5, then the landed frame twice more
    public const int AnimInterval = 1;     // animdata frame interval
    public const int Speed = 10;           // 100 ms a frame in TazUO (interval x speed x 10)
    public const int Duration = 16;        // x 50 ms = 800 ms: the eight frames once
    public const TileFlag TileFlags = TileFlag.Animation; // OSI's effect art (the sparkle 0x373A): Animation only
    public const int TileWeight = 255;
    public const int TileHeight = 0;

    public static readonly FlyingLootRecord[] Records =
    [
        new(FlyingLootKind.Blade, 0x3CB0, "flying longsword"),
        new(FlyingLootKind.Bow, 0x3CB6, "flying longbow"),
        new(FlyingLootKind.Staff, 0x3CBC, "flying staff"),
        new(FlyingLootKind.Armor, 0x3CC2, "flying armor"),
        new(FlyingLootKind.Shield, 0x3CC8, "flying shield"),
        new(FlyingLootKind.Jewelry, 0x3CCE, "flying ring"),
        new(FlyingLootKind.Gem, 0x3CD4, "flying gem"),
        new(FlyingLootKind.Potion, 0x3CDA, "flying potion"),
        new(FlyingLootKind.Clothing, 0x3CE0, "flying clothes")
    ];

    /// <summary>The effect's item ID for a kind; 0 for None (no effect).</summary>
    public static int EffectId(FlyingLootKind kind)
    {
        foreach (var r in Records)
        {
            if (r.Kind == kind)
            {
                return r.BaseId;
            }
        }

        return 0;
    }

    public static FlyingLootKind KindOf(Item? item) => item switch
    {
        null => FlyingLootKind.None,
        BaseShield => FlyingLootKind.Shield, // before BaseArmor: a shield is armor in pinned
        BaseArmor => FlyingLootKind.Armor,
        BaseRanged => FlyingLootKind.Bow,
        BaseStaff or BaseBashing or BaseSpear or BasePoleArm => FlyingLootKind.Staff,
        BaseWeapon => FlyingLootKind.Blade, // swords, axes, knives and any other weapon
        BaseJewel => FlyingLootKind.Jewelry,
        BaseClothing => FlyingLootKind.Clothing,
        BasePotion => FlyingLootKind.Potion,
        _ when Array.IndexOf(Loot.GemTypes, item.GetType()) >= 0 => FlyingLootKind.Gem,
        _ => FlyingLootKind.None
    };

    private static int Rank(FlyingLootKind kind) => kind switch
    {
        FlyingLootKind.None => 0,
        FlyingLootKind.Potion => 1,
        FlyingLootKind.Gem => 2,
        FlyingLootKind.Clothing => 3,
        _ => 4 // gear
    };

    /// <summary>The best kind among the items, the first rolled winning a tie; None when nothing has an animation.</summary>
    public static FlyingLootKind Best(IEnumerable<Item?> items)
    {
        var best = FlyingLootKind.None;
        foreach (var item in items)
        {
            var kind = KindOf(item);
            if (Rank(kind) > Rank(best))
            {
                best = kind;
            }
        }

        return best;
    }

    /// <summary>Raised for every effect sent (the opener, the effect ID, whether nearby players saw it). For the tests.</summary>
    public static event Action<PlayerMobile, int, bool>? Sent;

    /// <summary>Sends the flight for the best of the items over the opener, if anything has one. Returns the effect ID sent, or 0.</summary>
    public static int Play(PlayerMobile pm, IEnumerable<Item?> items, bool major)
    {
        if (!Enabled || pm.Deleted || pm.Map == null || pm.Map == Map.Internal || ClusterFFanfare.IsMuted(pm))
        {
            return 0;
        }

        var id = EffectId(Best(items));
        if (id == 0)
        {
            return 0;
        }

        if (major)
        {
            Effects.SendLocationEffect(pm.Location, pm.Map, id, Duration, Speed);
        }
        else if (pm.NetState is { } ns)
        {
            Span<byte> effect = stackalloc byte[OutgoingEffectPackets.HuedEffectLength].InitializePacket();
            OutgoingEffectPackets.CreateLocationHuedEffect(effect, pm.Location, id, Speed, Duration, 0, 0);
            ns.Send(effect);
        }
        else
        {
            return 0;
        }

        Sent?.Invoke(pm, id, major);
        return id;
    }
}
