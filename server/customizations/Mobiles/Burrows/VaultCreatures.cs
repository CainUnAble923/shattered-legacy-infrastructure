// cc-P140 (F-45): the tutorial Vault's creatures: the Ledger Bat swarm, the Penny Rats and their Hoarding Rat, the Safe
// Deposit Boxes (decoy mimics) and the Overdraft (the miniboss). All engage only tutorial participants and drop fixed
// loot (FarmCreatures.cs says why). The Vault room, its doorway and the crawlway are cc-P141's (Part D).
//
// Numbers are first proposals (notes, Part C table). Nothing here is OSI's.

using System;
using System.Linq;
using ModernUO.Serialization;
using Server.Items;
using Server.Tutorial;

namespace Server.Mobiles;

/// <summary>The weak swarm. Base: Mongbat (Humanoid/Melee/Mongbat.cs), on the vampire bat body 2055 (hover idle on action 8, mobtypes flag 4).</summary>
[SerializationGenerator(0, false)]
public partial class LedgerBat : TutorialCreature
{
    [Constructible]
    public LedgerBat()
    {
        Name = "a ledger bat";
        Body = 2055;
        BaseSoundID = 0x270;

        SetStr(8);
        SetDex(30);
        SetInt(6);
        SetHits(6);
        SetDamage(1, 2);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 5, 10);
        SetSkill(SkillName.Wrestling, 10.0);
        SetSkill(SkillName.Tactics, 10.0);
        SetSkill(SkillName.MagicResist, 5.0);
    }

    public override string CorpseName => "a bat corpse";
    public override bool CanFly => true;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(new Gold(3));
    }
}

/// <summary>Tight-space fighting with a pet. Base: GiantRat (Animals/Rodents/GiantRat.cs) cut down, on the stock giant rat body 0xD7.</summary>
[SerializationGenerator(0, false)]
public partial class PennyRat : TutorialCreature
{
    [Constructible]
    public PennyRat()
    {
        Name = "a penny rat";
        Body = 0xD7;
        BaseSoundID = 0x188;

        SetStr(20);
        SetDex(40);
        SetInt(10);
        SetHits(14);
        SetDamage(2, 4);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 10, 15);
        SetResistance(ResistanceType.Poison, 15, 20);
        SetSkill(SkillName.Wrestling, 20.0);
        SetSkill(SkillName.Tactics, 20.0);
        SetSkill(SkillName.MagicResist, 15.0);
    }

    public override string CorpseName => "a rat corpse";

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(new Gold(5));
    }
}

/// <summary>
/// The rat pack's leader (Chase, 2026-10-16), on the Ratman Lord art (body 2056). Base: Ratman (Humanoid/Melee/Ratman.cs)
/// cut to the Vault. It calls its pack: the first time it is hurt it brings PackSize Penny Rats to its side, once per
/// life (the call is not saved; a server restart mid-fight lets it call again, noted).
/// </summary>
[SerializationGenerator(0, false)]
public partial class HoardingRat : TutorialCreature
{
    public const int PackSize = 2;

    private bool _calledPack;

    [Constructible]
    public HoardingRat()
    {
        Name = "a hoarding rat";
        Body = 2056;
        BaseSoundID = 437;

        SetStr(50);
        SetDex(50);
        SetInt(20);
        SetHits(45);
        SetDamage(4, 7);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 15, 20);
        SetResistance(ResistanceType.Poison, 10, 15);
        SetSkill(SkillName.Wrestling, 40.0);
        SetSkill(SkillName.Tactics, 40.0);
        SetSkill(SkillName.MagicResist, 25.0);
    }

    public override string CorpseName => "a hoarding rat corpse";
    public bool CalledPack => _calledPack;

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        base.OnDamage(amount, from, willKill);

        if (!willKill && from != null && Alive)
        {
            CallPack(from);
        }
    }

    /// <summary>Brings PackSize Penny Rats beside it, aimed at <paramref name="target"/>, once. True when it called.</summary>
    public bool CallPack(Mobile target)
    {
        if (_calledPack || Map == null || Map == Map.Internal)
        {
            return false;
        }

        _calledPack = true;
        PublicOverheadMessage(MessageType.Regular, 0x3B2, false, "*squeals for its pack*");

        for (var i = 0; i < PackSize; i++)
        {
            var rat = new PennyRat { Team = Team, Home = Location, RangeHome = 6, Combatant = target };
            rat.MoveToWorld(Map.GetRandomNearbyLocation(Location), Map);
        }

        return true;
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(new Gold(40));
        c.DropItem(new Bandage(3));
    }
}

/// <summary>
/// A decoy chest: "that chest has teeth" before the boss. cc-P50's ChestMimic (level 1, its weakest row) with the
/// tutorial engagement rule and a fixed drop; a sibling type, so ChestMimic's and PetMimic's saves are untouched (the
/// base ctor and every hook are public or virtual: no schema change was needed, notes Part C). Cut below P50's level 1
/// to a Young player's size after the base ctor; its random reagents are removed so the drop is only the scripted one.
/// </summary>
[SerializationGenerator(0, false)]
public partial class SafeDepositBox : ChestMimic
{
    [Constructible]
    public SafeDepositBox() : this(MimicFacing.NorthSouth)
    {
    }

    public SafeDepositBox(MimicFacing facing) : base(ChestMimic.MinLevel, facing)
    {
        Name = "a safe deposit box";

        SetStr(40);
        SetDex(40);
        SetInt(40);
        SetHits(40);
        SetDamage(3, 6);
        SetResistance(ResistanceType.Physical, 10, 15);
        SetResistance(ResistanceType.Fire, 5, 10);
        SetResistance(ResistanceType.Cold, 5, 10);
        SetResistance(ResistanceType.Poison, 5, 10);
        SetResistance(ResistanceType.Energy, 5, 10);
        SetSkill(SkillName.EvalInt, 30.0);
        SetSkill(SkillName.Magery, 30.0);
        SetSkill(SkillName.Meditation, 30.0);
        SetSkill(SkillName.MagicResist, 20.0);
        SetSkill(SkillName.Tactics, 25.0);
        SetSkill(SkillName.Wrestling, 25.0);

        Fame = 0;
        Karma = 0;
        VirtualArmor = 12;
        NoKillAwards = true; // as TutorialCreature sets it (a settable property on BaseCreature, not virtual)

        if (Backpack != null)
        {
            foreach (var item in Backpack.Items.ToArray())
            {
                item.Delete();
            }
        }
    }

    public override string DefaultName => "a safe deposit box";
    public override string CorpseName => "a sprung safe deposit box";
    public override bool IgnoreYoungProtection => true;

    public override bool IsEnemy(Mobile m) => TutorialCreature.Engages(m) && base.IsEnemy(m);

    public override bool CanBeHarmful(Mobile target, bool message, bool ignoreOurBlessedness) =>
        TutorialCreature.Engages(target) && base.CanBeHarmful(target, message, ignoreOurBlessedness);

    public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        if (from != null && !TutorialCreature.Engages(from))
        {
            return;
        }

        base.Damage(amount, from, informMount, ignoreEvilOmen);
    }

    public override void GenerateLoot()
    {
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(new Gold(25));
    }
}

/// <summary>
/// The tutorial miniboss: a living bank strongbox at boss scale (the WoW crate creature at 1.5x, body 2057, Chase's
/// pick 2026-10-15), hued strongbox brown. It teaches death, resurrection, looting and kiting: it hits hard for a Young
/// character and walks slowly. Base: the ChestMimic level 1 row's shape (OSI's Mimic scaled, cc-P50) as a melee
/// creature, cut to a group of new characters. Its drop is exactly its list (Chase, 2026-10-15): a coin pouch from the
/// vault, one Dawnstone ingot, bandages, and one tradeable trinket from BurrowsLoot.OverdraftTrinkets.
/// </summary>
[SerializationGenerator(0, false)]
public partial class TheOverdraft : TutorialCreature
{
    public const int StrongboxHue = 1740;
    public const int PouchGold = 100;
    public const int Bandages = 10;

    [Constructible]
    public TheOverdraft()
    {
        Name = "The Overdraft";
        Body = 2057;
        Hue = StrongboxHue;

        SetStr(90);
        SetDex(30);
        SetInt(20);
        SetHits(120);
        SetDamage(6, 10);
        SetDamageType(ResistanceType.Physical, 100);
        SetResistance(ResistanceType.Physical, 25, 30);
        SetResistance(ResistanceType.Fire, 5, 10);
        SetResistance(ResistanceType.Cold, 15, 20);
        SetResistance(ResistanceType.Poison, 20, 25);
        SetResistance(ResistanceType.Energy, 10, 15);
        SetSkill(SkillName.Wrestling, 45.0);
        SetSkill(SkillName.Tactics, 45.0);
        SetSkill(SkillName.MagicResist, 30.0);

        VirtualArmor = 20;

        // Slow: the kiting lesson. Pinned's speed table gives a melee creature 0.2 / 0.4.
        ActiveSpeed = 0.4;
        PassiveSpeed = 0.8;
    }

    public override string CorpseName => "the Overdraft's wreckage";
    public override int GetIdleSound() => ChestMimic.IdleSound;
    public override int GetAngerSound() => ChestMimic.AngerSound;
    public override int GetHurtSound() => ChestMimic.HurtSound;
    public override int GetDeathSound() => ChestMimic.DeathSound;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);
        c.DropItem(BurrowsLoot.CoinPouch(PouchGold, "a coin pouch from the vault"));
        c.DropItem(new DawnstoneIngot(1));
        c.DropItem(new Bandage(Bandages));
        c.DropItem(BurrowsLoot.RandomOverdraftTrinket());
    }
}
