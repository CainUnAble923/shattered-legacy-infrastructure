// =============================================================================
// ShardKeys.cs
// cc-P363: keys drawn with Hestia's "Quest, Item, Token Keys" (registry 46630-46663, her words: "use however you want,
// drops, gate keys, items keys"), and the locks they open.
//
// ShardKey          a key with a name, a hue, one of Hestia's 34 designs, and a lock tag: it opens only a lock whose tag is
//                   the same (ordinal, case and all). Not a stock Key: a stock key blank copies any Key's KeyValue
//                   (pinned Key.CopyTarget), and a stock blank's KeyValue 0 would open a door left at 0. A ShardKey can be
//                   neither copied nor copied onto.
// IShardLock        anything a ShardKey opens: its LockTag and the name players are told ("the Vault key").
// ShardLockedDoor   a dungeon metal door (MetalDoor2) that opens only for someone carrying its key; staff pass. Optionally
//                   takes the key (ConsumeKey). Uses: a dungeon boss room (ShardKeys.BossRoomKey).
// ShardLockedLootBox a loot box of the keyed tier: it opens only with a Vault key, which it takes
//                   (clusterf.lootBoxes.keyTier, default 6: the top tier; 0 turns it off). Boxes earned before cc-P363 are
//                   plain LootBoxes and stay keyless. A Vault key drops on a boss kill (clusterf.lootBoxes.vaultKeyChance,
//                   default 0.25), into the killer's pack.
// Keyed moongates   ShardMoongate.LockTag (ShardMoongates.cs): a gate that lets through only a carrier of its key.
//                   Uses: the League's trial gates (ShardKeys.LeagueTrialGateKey, one tag per trial chapter).
//
// Other uses worth making (notes/cc-P363-quest-markers-keys-gates.md lists them): Peerless-style altar keys, a key ring
// gump for ShardKeys, quest-chain keys handed out by NPCs, guild hall vault rooms, seasonal event gates.
// =============================================================================

#nullable enable

using System;
using ModernUO.Serialization;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Targeting;

namespace Server.Items;

public interface IShardLock
{
    string LockTag { get; }

    string LockName { get; }
}

[SerializationGenerator(0, false)]
public partial class ShardKey : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _lockTag;

    [SerializableField(1)]
    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _keyName;

    [Constructible]
    public ShardKey() : this("", "a key")
    {
    }

    [Constructible]
    public ShardKey(string lockTag, string keyName, int design = 2, int hue = 0) : base(ShardItemArt.KeyItemId(design))
    {
        _lockTag = lockTag ?? "";
        _keyName = keyName ?? "a key";
        Hue = hue;
        Weight = 1.0;
    }

    public override string DefaultName => _keyName;

    /// <summary>Hestia's design, 1 to 34 (design 1 is her blank partial-hue key, for a hue of the staff's choice).</summary>
    [CommandProperty(AccessLevel.GameMaster)]
    public int Design
    {
        get => ItemID - ShardItemArt.FirstKey + 1;
        set => ItemID = ShardItemArt.KeyItemId(value); // throws outside 1 to 34: [props then says it was not set
    }

    /// <summary>The one test: this key opens the lock only when both carry the same, non-empty tag.</summary>
    public bool Opens(IShardLock? lck) =>
        lck != null && !string.IsNullOrEmpty(_lockTag) && string.Equals(_lockTag, lck.LockTag, StringComparison.Ordinal);

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("a key: double-click, then target what it opens");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(501661); // That key is unreachable.
            return;
        }

        from.SendLocalizedMessage(501662); // What shall I use this key on?
        from.Target = new UseTarget(this);
    }

    private class UseTarget : Target
    {
        private readonly ShardKey _key;

        public UseTarget(ShardKey key) : base(3, false, TargetFlags.None) => _key = key;

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_key.Deleted || !_key.IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(501661); // That key is unreachable.
            }
            else if (targeted is IShardLock lck)
            {
                if (!_key.Opens(lck))
                {
                    from.SendMessage(0x22, "This key does not fit that lock.");
                }
                else if (targeted is Item item)
                {
                    item.OnDoubleClick(from); // carrying the key is what each lock checks
                }
            }
            else
            {
                from.SendLocalizedMessage(501666); // You can't unlock that!
            }
        }
    }
}

public static class ShardKeys
{
    public const string KeyTierSetting = "clusterf.lootBoxes.keyTier";
    public const string VaultKeyChanceSetting = "clusterf.lootBoxes.vaultKeyChance";

    public const string VaultTag = "lootbox.vault";
    public const string VaultKeyName = "a Vault key";

    // Hestia's designs used ("Item Key n"): 14 a padlock, 12 a skull, 30 a blue star.
    public const int VaultDesign = 14;
    public const int BossRoomDesign = 12;
    public const int TrialGateDesign = 30;

    /// <summary>The loot box tier that needs a Vault key to open; 0 when none does.</summary>
    public static int KeyTier => ServerConfiguration.GetOrUpdateSetting(KeyTierSetting, 6);

    public static double VaultKeyChance => ServerConfiguration.GetOrUpdateSetting(VaultKeyChanceSetting, 0.25);

    public static string TrialGateTag(int chapter) => $"league.trial.{chapter}";

    public static string BossRoomTag(string dungeon) => $"boss.{dungeon.Trim().ToLowerInvariant()}";

    public static ShardKey VaultKey() => new(VaultTag, VaultKeyName, VaultDesign, 0x8A5);

    public static ShardKey LeagueTrialGateKey(int chapter) =>
        new(TrialGateTag(chapter), $"a League trial key (chapter {chapter})", TrialGateDesign, 0x501);

    public static ShardKey BossRoomKey(string dungeon) =>
        new(BossRoomTag(dungeon), $"a key to the {dungeon} boss room", BossRoomDesign, 0x485);

    /// <summary>A key in this mobile's pack (at any depth) that opens the lock, or null.</summary>
    public static ShardKey? FindKey(Mobile m, IShardLock lck)
    {
        if (m?.Backpack == null)
        {
            return null;
        }

        foreach (var item in m.Backpack.FindItems())
        {
            if (item is ShardKey key && key.Opens(lck))
            {
                return key;
            }
        }

        return null;
    }

    /// <summary>A box rolled now: the keyed tier comes as a ShardLockedLootBox (ClusterFLootBoxes.MakeBox).</summary>
    public static bool NeedsKey(int tier) => KeyTier > 0 && tier >= KeyTier;

    /// <summary>A boss kill (ClusterFLootBoxes.OnBossKilled): sometimes a Vault key for the killer.</summary>
    public static ShardKey? OnBossKilled(PlayerMobile pm, double roll)
    {
        if (KeyTier <= 0 || roll >= VaultKeyChance)
        {
            return null;
        }

        var key = VaultKey();
        ClusterFGuildPromotion.Deliver(pm, key);
        pm.SendMessage(0x44, "System: You found a Vault key. It opens one of the top tier's loot boxes.");
        return key;
    }
}

/// <summary>A dungeon door that opens only for someone carrying its key (staff pass).</summary>
[SerializationGenerator(0, false)]
public partial class ShardLockedDoor : MetalDoor2, IShardLock
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _lockTag;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _lockName;

    [SerializableField(2)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _consumeKey;

    [Constructible]
    public ShardLockedDoor(DoorFacing facing) : this(facing, "", "its key")
    {
    }

    [Constructible]
    public ShardLockedDoor(DoorFacing facing, string lockTag, string lockName, bool consumeKey = false) : base(facing)
    {
        _lockTag = lockTag ?? "";
        _lockName = lockName ?? "its key";
        _consumeKey = consumeKey;
        Locked = true;
    }

    public static ShardLockedDoor BossRoom(DoorFacing facing, string dungeon) =>
        new(facing, ShardKeys.BossRoomTag(dungeon), $"a key to the {dungeon} boss room");

    public override void Use(Mobile from)
    {
        if (!Locked || Open || from.AccessLevel >= AccessLevel.GameMaster)
        {
            base.Use(from);
            return;
        }

        var key = ShardKeys.FindKey(from, this);

        if (key == null)
        {
            from.SendMessage(0x22, $"That is locked. It opens with {_lockName}.");
            return;
        }

        if (_consumeKey)
        {
            key.Delete();
            from.SendMessage(0x3B2, "The lock takes your key.");
        }

        // Past our lock, the stock door opens and closes as usual; its own key check never sees our tag.
        Locked = false;

        try
        {
            base.Use(from);
        }
        finally
        {
            Locked = true;
        }
    }
}

/// <summary>A loot box of the keyed tier (ShardKeys.KeyTier): it opens only with a Vault key, which it takes.</summary>
[SerializationGenerator(0, false)]
public partial class ShardLockedLootBox : LootBox, IShardLock
{
    [Constructible]
    public ShardLockedLootBox()
    {
    }

    public ShardLockedLootBox(BoxTheme theme, int tier, string boundAccount, string earnedFor)
        : base(theme, tier, boundAccount, earnedFor)
    {
    }

    public string LockTag => ShardKeys.VaultTag;

    public string LockName => ShardKeys.VaultKeyName;

    /// <summary>Once opened (its contents handed over), the box stays unlocked.</summary>
    public bool Unlocked => OpenedAt != DateTime.MinValue;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (!Unlocked)
        {
            list.Add($"Locked: opens with {ShardKeys.VaultKeyName}");
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (Unlocked || from is not PlayerMobile pm)
        {
            base.OnDoubleClick(from);
            return;
        }

        var key = ShardKeys.FindKey(pm, this);

        if (key == null)
        {
            pm.SendMessage(0x22, $"System: This box is locked. It opens with {ShardKeys.VaultKeyName}; bosses drop them.");
            return;
        }

        // The box's own checks (account, in the pack, a safe place) come first; the key goes only if it opened.
        base.OnDoubleClick(from);

        if (Unlocked || Deleted)
        {
            key.Delete();
            pm.SendMessage(0x3B2, "System: The Vault key turns, and stays in the lock.");
        }
    }
}
