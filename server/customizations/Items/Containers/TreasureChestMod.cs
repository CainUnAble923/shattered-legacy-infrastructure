// ServUO: Items/Containers/TreasureChestMod.cs (CC4 Shame) - "Treasure Chest Pack 0.99I by Nerun".
//
// The four levels RevampedSpawns/ShameRevamped.xml spawns (TreasureLevel1 x20 rows, 2 x48, 3 x38, 4 x10).
// ServUO's file also carries TreasureLevel1h and other variants that nothing here references; not ported.
// Values are ServUO's. See BaseTreasureChestMod.cs for what the pack is and the two dropped hooks.
//
// The pre-AOS branch that hands out old-style magic levels is kept as ServUO has it (Core.AOS is true on
// this shard, so it never runs); ServUO's ItemQuality.Normal is WeaponQuality.Regular / ArmorQuality.Regular.

using ModernUO.Serialization;

namespace Server.Items;

// ---------- [Level 1] ----------
// Large, Medium and Small Crate
[Flippable(0xE3E, 0xE3F)]
[SerializationGenerator(0, false)]
public partial class TreasureLevel1 : BaseTreasureChestMod
{
    [Constructible]
    public TreasureLevel1() : base(Utility.RandomList(0xE3C, 0xE3E, 0x9A9))
    {
        RequiredSkill = 52;
        LockLevel = RequiredSkill - Utility.Random(1, 10);
        MaxLockLevel = RequiredSkill;
        TrapType = TrapType.MagicTrap;
        TrapPower = 1 * Utility.Random(1, 25);

        DropItem(new Gold(30, 100));
        DropItem(new Bolt(10));
        DropItem(Loot.RandomClothing());

        AddLoot(Loot.RandomWeapon());
        AddLoot(Loot.RandomArmorOrShield());
        AddLoot(Loot.RandomJewelry());

        for (var i = Utility.Random(3) + 1; i > 0; i--) // random 1 to 3
        {
            DropItem(Loot.RandomGem());
        }
    }

    public override int DefaultGumpID => 0x49;
}

// ---------- [Level 2] ----------
// Large, Medium and Small Crate
// Wooden, Metal and Metal Golden Chest
// Keg and Barrel
[Flippable(0xE43, 0xE42)]
[SerializationGenerator(0, false)]
public partial class TreasureLevel2 : BaseTreasureChestMod
{
    [Constructible]
    public TreasureLevel2() : base(Utility.RandomList(0xE3C, 0xE3E, 0x9A9, 0xE42, 0x9AB, 0xE40, 0xE7F, 0xE77))
    {
        RequiredSkill = 72;
        LockLevel = RequiredSkill - Utility.Random(1, 10);
        MaxLockLevel = RequiredSkill;
        TrapType = TrapType.MagicTrap;
        TrapPower = 2 * Utility.Random(1, 25);

        DropItem(new Gold(70, 100));
        DropItem(new Arrow(10));
        DropItem(Loot.RandomPotion());

        for (var i = Utility.Random(1, 2); i > 1; i--)
        {
            var reagentLoot = Loot.RandomReagent();
            reagentLoot.Amount = Utility.Random(1, 2);
            DropItem(reagentLoot);
        }

        if (Utility.RandomBool()) // 50% chance
        {
            for (var i = Utility.Random(8) + 1; i > 0; i--)
            {
                DropItem(Loot.RandomScroll(0, 39, SpellbookType.Regular));
            }
        }

        if (Utility.RandomBool()) // 50% chance
        {
            for (var i = Utility.Random(6) + 1; i > 0; i--)
            {
                DropItem(Loot.RandomGem());
            }
        }
    }
}

// ---------- [Level 3] ----------
// Wooden, Metal and Metal Golden Chest
[Flippable(0x9AB, 0xE7C)]
[SerializationGenerator(0, false)]
public partial class TreasureLevel3 : BaseTreasureChestMod
{
    [Constructible]
    public TreasureLevel3() : base(Utility.RandomList(0x9AB, 0xE40, 0xE42))
    {
        RequiredSkill = 84;
        LockLevel = RequiredSkill - Utility.Random(1, 10);
        MaxLockLevel = RequiredSkill;
        TrapType = TrapType.MagicTrap;
        TrapPower = 3 * Utility.Random(1, 25);

        DropItem(new Gold(180, 240));
        DropItem(new Arrow(10));

        for (var i = Utility.Random(1, 3); i > 1; i--)
        {
            var reagentLoot = Loot.RandomReagent();
            reagentLoot.Amount = Utility.Random(1, 9);
            DropItem(reagentLoot);
        }

        for (var i = Utility.Random(1, 3); i > 1; i--)
        {
            DropItem(Loot.RandomPotion());
        }

        if (0.67 > Utility.RandomDouble()) // 67% chance = 2/3
        {
            for (var i = Utility.Random(12) + 1; i > 0; i--)
            {
                DropItem(Loot.RandomScroll(0, 47, SpellbookType.Regular));
            }
        }

        if (0.67 > Utility.RandomDouble()) // 67% chance = 2/3
        {
            for (var i = Utility.Random(9) + 1; i > 0; i--)
            {
                DropItem(Loot.RandomGem());
            }
        }

        for (var i = Utility.Random(1, 3); i > 1; i--)
        {
            DropItem(Loot.RandomWand());
        }

        // Magical ArmorOrWeapon
        for (var i = Utility.Random(1, 3); i > 1; i--)
        {
            var item = Loot.RandomArmorOrShieldOrWeapon();

            if (!Core.AOS)
            {
                if (item is BaseWeapon weapon)
                {
                    weapon.DamageLevel = (WeaponDamageLevel)Utility.Random(3);
                    weapon.AccuracyLevel = (WeaponAccuracyLevel)Utility.Random(3);
                    weapon.DurabilityLevel = (WeaponDurabilityLevel)Utility.Random(3);
                    weapon.Quality = WeaponQuality.Regular;
                }
                else if (item is BaseArmor armor)
                {
                    armor.ProtectionLevel = (ArmorProtectionLevel)Utility.Random(3);
                    armor.Durability = (ArmorDurabilityLevel)Utility.Random(3);
                    armor.Quality = ArmorQuality.Regular;
                }
            }
            else
            {
                AddLoot(item);
            }
        }

        for (var i = Utility.Random(1, 2); i > 1; i--)
        {
            AddLoot(Loot.RandomClothing());
        }

        for (var i = Utility.Random(1, 2); i > 1; i--)
        {
            AddLoot(Loot.RandomJewelry());
        }

        // Magic clothing (not implemented)
        // Magic jewelry (not implemented)
    }

    public override int DefaultGumpID => 0x4A;
}

// ---------- [Level 4] ----------
// Wooden, Metal and Metal Golden Chest
[Flippable(0xE41, 0xE40)]
[SerializationGenerator(0, false)]
public partial class TreasureLevel4 : BaseTreasureChestMod
{
    [Constructible]
    public TreasureLevel4() : base(Utility.RandomList(0xE40, 0xE42, 0x9AB))
    {
        RequiredSkill = 92;
        LockLevel = RequiredSkill - Utility.Random(1, 10);
        MaxLockLevel = RequiredSkill;
        TrapType = TrapType.MagicTrap;
        TrapPower = 4 * Utility.Random(1, 25);

        DropItem(new Gold(200, 400));
        DropItem(new BlankScroll(Utility.Random(1, 4)));

        for (var i = Utility.Random(1, 4); i > 1; i--)
        {
            var reagentLoot = Loot.RandomReagent();
            reagentLoot.Amount = Utility.Random(6, 12);
            DropItem(reagentLoot);
        }

        for (var i = Utility.Random(1, 4); i > 1; i--)
        {
            DropItem(Loot.RandomPotion());
        }

        if (0.75 > Utility.RandomDouble()) // 75% chance = 3/4
        {
            for (var i = Utility.RandomMinMax(8, 16); i > 0; i--)
            {
                DropItem(Loot.RandomScroll(0, 47, SpellbookType.Regular));
            }
        }

        if (0.75 > Utility.RandomDouble()) // 75% chance = 3/4
        {
            for (var i = Utility.RandomMinMax(6, 12) + 1; i > 0; i--)
            {
                DropItem(Loot.RandomGem());
            }
        }

        for (var i = Utility.Random(1, 4); i > 1; i--)
        {
            DropItem(Loot.RandomWand());
        }

        // Magical ArmorOrWeapon
        for (var i = Utility.Random(1, 4); i > 1; i--)
        {
            var item = Loot.RandomArmorOrShieldOrWeapon();

            if (!Core.AOS)
            {
                if (item is BaseWeapon weapon)
                {
                    weapon.DamageLevel = (WeaponDamageLevel)Utility.Random(4);
                    weapon.AccuracyLevel = (WeaponAccuracyLevel)Utility.Random(4);
                    weapon.DurabilityLevel = (WeaponDurabilityLevel)Utility.Random(4);
                    weapon.Quality = WeaponQuality.Regular;
                }
                else if (item is BaseArmor armor)
                {
                    armor.ProtectionLevel = (ArmorProtectionLevel)Utility.Random(4);
                    armor.Durability = (ArmorDurabilityLevel)Utility.Random(4);
                    armor.Quality = ArmorQuality.Regular;
                }
            }
            else
            {
                AddLoot(item);
            }
        }

        for (var i = Utility.Random(1, 2); i > 1; i--)
        {
            AddLoot(Loot.RandomClothing());
        }

        for (var i = Utility.Random(1, 2); i > 1; i--)
        {
            AddLoot(Loot.RandomJewelry());
        }
    }
}
