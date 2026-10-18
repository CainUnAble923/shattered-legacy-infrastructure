// cc-P140 (F-45): the Warren (tier 1), the Burrowers family: Burrow Grub, Burrow Scamp, Redcap Scamp, and the Burrow
// Mother in the Brood Pit. Part: Burrower Claws (one per kill, PackItem); the grub and the hog carve Burrowhide (P36).
// Normal spawns: Young allowed, P36's limit of three hostiles on one Young player stands (OldHavenBurrowsRegion).
//
// The Burrow Mother and the mini champ: P24 section 4 costed ServUO's MiniChampionSystem at about 610 lines plus a test
// and AGENTS.md says never to build a subsystem inside a content task, so the engine is not ported here (notes,
// Decisions). She carries her own brood instead: while she fights she keeps up to BroodAtOnce Burrow Grubs beside her,
// BroodPerLife in all, and her death takes the brood with her, so the fight ends when she does. A later prompt can put
// her at the top of a mini champ when the engine lands; nothing here is in the way.
//
// Numbers are first proposals scaled from the nearest pinned creature (notes, Part C table).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

/// <summary>Base: Slime and GiantRat (Misc/Melee/Slime.cs, Rodents/GiantRat.cs) at tier 1, on the Diablo 2 sand maggot body 2058.</summary>
[SerializationGenerator(0, false)]
public partial class BurrowGrub : BaseCreature
{
    public const int BurrowhideHidesCount = 2;

    [Constructible]
    public BurrowGrub() : base(AIType.AI_Melee)
    {
        Name = "a burrow grub";
        Body = 2058;
        BaseSoundID = 456;

        SetStr(30, 40);
        SetDex(20, 30);
        SetInt(10);
        SetHits(20, 28);
        SetMana(0);
        SetDamage(3, 6);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 10, 15);
        SetResistance(ResistanceType.Poison, 20, 30);
        SetSkill(SkillName.MagicResist, 15.1, 20.0);
        SetSkill(SkillName.Tactics, 20.1, 35.0);
        SetSkill(SkillName.Wrestling, 20.1, 35.0);

        Fame = 300;
        Karma = -300;
        VirtualArmor = 12;

        PackItem(new BurrowerClaws());
    }

    public override string CorpseName => "a burrow grub corpse";
    public override Poison PoisonImmune => Poison.Lesser;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Poor);
    }

    public override void OnCarve(Mobile from, Corpse corpse, Item with) =>
        BurrowsCarving.Carve(this, from, corpse, BurrowhideHidesCount, 0);
}

/// <summary>Base: Ratman (Humanoid/Melee/Ratman.cs) at tier 1, on the Vanguard kobold body 2059 (an animal slot, so the server types it Animal).</summary>
[SerializationGenerator(0, false)]
public partial class BurrowScamp : BaseCreature
{
    [Constructible]
    public BurrowScamp() : base(AIType.AI_Melee)
    {
        Name = "a burrow scamp";
        Body = 2059;
        BaseSoundID = 437;

        SetStr(50, 60);
        SetDex(50, 60);
        SetInt(20, 30);
        SetHits(35, 45);
        SetDamage(4, 7);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 15, 20);
        SetResistance(ResistanceType.Fire, 5, 10);
        SetResistance(ResistanceType.Cold, 5, 10);
        SetResistance(ResistanceType.Poison, 5, 10);
        SetResistance(ResistanceType.Energy, 5, 10);
        SetSkill(SkillName.MagicResist, 20.1, 35.0);
        SetSkill(SkillName.Tactics, 40.1, 55.0);
        SetSkill(SkillName.Wrestling, 40.1, 55.0);

        Fame = 600;
        Karma = -600;
        VirtualArmor = 18;

        PackItem(new BurrowerClaws());
    }

    public override string CorpseName => "a burrow scamp corpse";
    public override bool CanRummageCorpses => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}

/// <summary>The scamps' tougher kin (the red-capped Vanguard kobold, body 2060). Base: Ratman up a step.</summary>
[SerializationGenerator(0, false)]
public partial class RedcapScamp : BaseCreature
{
    [Constructible]
    public RedcapScamp() : base(AIType.AI_Melee)
    {
        Name = "a redcap scamp";
        Body = 2060;
        BaseSoundID = 437;

        SetStr(60, 70);
        SetDex(55, 65);
        SetInt(25, 35);
        SetHits(45, 55);
        SetDamage(5, 8);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 20, 25);
        SetResistance(ResistanceType.Fire, 10, 15);
        SetResistance(ResistanceType.Cold, 5, 10);
        SetResistance(ResistanceType.Poison, 10, 15);
        SetResistance(ResistanceType.Energy, 5, 10);
        SetSkill(SkillName.MagicResist, 25.1, 40.0);
        SetSkill(SkillName.Tactics, 45.1, 60.0);
        SetSkill(SkillName.Wrestling, 45.1, 60.0);

        Fame = 900;
        Karma = -900;
        VirtualArmor = 22;

        PackItem(new BurrowerClaws());
    }

    public override string CorpseName => "a redcap scamp corpse";
    public override bool CanRummageCorpses => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}

/// <summary>
/// The Brood Pit's boss (the large sand maggot, body 2061). Base: Ogre (Humanoid/Melee/Ogre.cs) as a first boss for
/// graduated Young players. Her brood: SpawnBrood keeps up to BroodAtOnce grubs beside her while she fights, BroodPerLife
/// in all, one every BroodInterval from OnThink; her death deletes the brood still alive. Drops: jewelry, her mandible
/// (the boss part), claws, Average loot.
/// </summary>
[SerializationGenerator(0, false)]
public partial class TheBurrowMother : BaseCreature
{
    public const int BroodAtOnce = 3;
    public const int BroodPerLife = 8;
    public static readonly TimeSpan BroodInterval = TimeSpan.FromSeconds(15);

    private readonly List<BurrowGrub> _brood = new();
    private int _broodSpawned;
    private DateTime _nextBrood;

    [Constructible]
    public TheBurrowMother() : base(AIType.AI_Melee)
    {
        Name = "The Burrow Mother";
        Body = 2061;
        BaseSoundID = 456;

        SetStr(160, 190);
        SetDex(40, 60);
        SetInt(40, 60);
        SetHits(150, 180);
        SetDamage(8, 12);
        SetDamageType(ResistanceType.Physical, 80);
        SetDamageType(ResistanceType.Poison, 20);
        SetResistance(ResistanceType.Physical, 30, 35);
        SetResistance(ResistanceType.Fire, 15, 25);
        SetResistance(ResistanceType.Cold, 15, 25);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 15, 25);
        SetSkill(SkillName.MagicResist, 50.1, 65.0);
        SetSkill(SkillName.Tactics, 60.1, 70.0);
        SetSkill(SkillName.Wrestling, 65.1, 80.0);

        Fame = 3000;
        Karma = -3000;
        VirtualArmor = 32;

        PackItem(new BurrowerClaws(3));
    }

    public override string CorpseName => "the Burrow Mother's corpse";
    public override Poison PoisonImmune => Poison.Regular;
    public override Poison HitPoison => Poison.Lesser;
    public override double HitPoisonChance => 0.3;
    public override bool CanFlee => false;
    public int BroodSpawned => _broodSpawned;
    public int BroodAlive => LiveBrood();

    private int LiveBrood()
    {
        _brood.RemoveAll(g => g.Deleted || !g.Alive);
        return _brood.Count;
    }

    public override void OnThink()
    {
        base.OnThink();

        if (Combatant != null && Core.Now >= _nextBrood)
        {
            _nextBrood = Core.Now + BroodInterval;
            SpawnBrood();
        }
    }

    /// <summary>One more grub beside her, within the caps. True when one came.</summary>
    public bool SpawnBrood()
    {
        if (!Alive || Map == null || Map == Map.Internal || _broodSpawned >= BroodPerLife || LiveBrood() >= BroodAtOnce)
        {
            return false;
        }

        var grub = new BurrowGrub { Team = Team, Home = Location, RangeHome = 8 };
        if (Combatant is Mobile target)
        {
            grub.Combatant = target;
        }

        grub.MoveToWorld(Map.GetRandomNearbyLocation(Location), Map);
        _brood.Add(grub);
        _broodSpawned++;
        return true;
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Gems);
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(BurrowsLoot.RandomJewel());
        c.DropItem(new BroodMothersMandible());

        foreach (var grub in _brood.ToArray())
        {
            if (!grub.Deleted)
            {
                grub.Delete();
            }
        }

        _brood.Clear();
    }
}
