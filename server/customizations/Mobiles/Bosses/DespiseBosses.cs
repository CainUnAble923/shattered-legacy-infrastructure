// ServUO: Mobiles/Bosses/DespiseBosses.cs (CC4 Despise). Five types, one file, as ServUO keeps them.
//
// The overlords (Adrian for the good side, Andros for the evil side) take damage only from
// DespiseCreatures — possessed creatures are the players' weapon — and summon a lieutenant wisp every
// 40-60 seconds while none is alive. The artifact table is CC9 batch 5's DespiseArtifacts.cs, all seven
// already in the tree.
//
// Conversion notes:
//   Damage(int, Mobile, bool, bool) returns int on ServUO and void here (Mobile.cs:5900); the "return 0"
//     arm is simply not calling base.
//   Delete() overrides become OnAfterDelete().
//   BaseCreature.Summon(creature, controlled, caster, loc, sound, duration) exists unchanged
//     (BaseCreature.cs:3530). FollowersMax = 100 is what lets the boss "control" its wisp.
//   The wisps have no [Constructible] on ServUO either; they are summoned, never [add]ed.
//   FightMode.Closest (bosses) and FightMode.None (wisps) exist unchanged.

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.Despise;

[SerializationGenerator(0, false)]
public partial class DespiseBoss : BaseCreature
{
    public static readonly int ArtifactChance = 5;

    public static Type[] Artifacts { get; } =
    {
        typeof(CompassionsEye),
        typeof(UnicornManeWovenSandals),
        typeof(UnicornManeWovenTalons),
        typeof(DespicableQuiver),
        typeof(UnforgivenVeil),
        typeof(HailstormHuman),
        typeof(HailstormGargoyle)
    };

    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private BaseCreature _wisp;

    private Timer _summonTimer;

    public DespiseBoss(AIType ai, FightMode fightmode) : base(ai, fightmode)
    {
        _summonTimer = Timer.DelayCall(TimeSpan.FromSeconds(5), SummonWisp_Callback);

        FollowersMax = 100;
    }

    public virtual BaseCreature SummonWisp => null;
    public virtual double WispScalar => 0.33;

    public void SetNonMovable(Item item)
    {
        item.Movable = false;
        AddItem(item);
    }

    public override void Damage(int amount, Mobile from = null, bool informMount = true, bool ignoreEvilOmen = false)
    {
        if (from is DespiseCreature)
        {
            base.Damage(amount, from, informMount, ignoreEvilOmen);
        }
    }

    public override void OnKilledBy(Mobile mob)
    {
        if (mob is PlayerMobile pm)
        {
            var chance = ArtifactChance + Math.Min(10, pm.Luck / 180);

            if (chance >= Utility.Random(100))
            {
                var t = Artifacts[Utility.Random(Artifacts.Length)];

                if (t != null)
                {
                    var arty = Loot.Construct(t);

                    if (arty != null)
                    {
                        var pack = mob.Backpack;

                        if (pack == null || !pack.TryDropItem(mob, arty, false))
                        {
                            mob.BankBox.DropItem(arty);
                            mob.SendMessage("An artifact has been placed in your bankbox!");
                        }
                        else
                        {
                            mob.SendLocalizedMessage(1153440); // An artifact has been placed in your backpack!
                        }
                    }
                }
            }
        }
    }

    private bool WispIsUp => _wisp?.Deleted == false && _wisp.Alive;

    public override void AlterMeleeDamageTo(Mobile to, ref int damage)
    {
        base.AlterMeleeDamageTo(to, ref damage);

        if (WispIsUp)
        {
            damage += (int)(damage * WispScalar);
        }
    }

    public override void AlterMeleeDamageFrom(Mobile from, ref int damage)
    {
        base.AlterMeleeDamageFrom(from, ref damage);

        if (WispIsUp)
        {
            damage -= (int)(damage * WispScalar);
        }
    }

    public override void AlterSpellDamageTo(Mobile to, ref int damage)
    {
        base.AlterSpellDamageTo(to, ref damage);

        if (WispIsUp)
        {
            damage += (int)(damage * WispScalar);
        }
    }

    public override void AlterSpellDamageFrom(Mobile from, ref int damage)
    {
        base.AlterSpellDamageFrom(from, ref damage);

        if (WispIsUp)
        {
            damage -= (int)(damage * WispScalar);
        }
    }

    public override void OnThink()
    {
        base.OnThink();

        if (_summonTimer == null && (_wisp == null || !_wisp.Alive || _wisp.Deleted))
        {
            _summonTimer = Timer.DelayCall(TimeSpan.FromSeconds(Utility.RandomMinMax(40, 60)), SummonWisp_Callback);
        }
    }

    public void SummonWisp_Callback()
    {
        _wisp = SummonWisp;

        if (_wisp != null)
        {
            Summon(_wisp, true, this, Location, 0, TimeSpan.FromMinutes(90));
        }

        _summonTimer = null;
        this.MarkDirty();
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        if (_wisp?.Alive == true)
        {
            _wisp.Kill();
        }
    }
}

[SerializationGenerator(0, false)]
public partial class AdrianTheGloriousLord : DespiseBoss
{
    [Constructible]
    public AdrianTheGloriousLord() : base(AIType.AI_Mage, FightMode.Closest)
    {
        Title = "the Glorious Lord";

        Race = Race.Human;
        Body = 0x190;
        Female = false;

        Hue = Race.RandomSkinHue();
        HairItemID = 8252;
        HairHue = 153;

        SetStr(900, 1200);
        SetDex(500, 600);
        SetInt(500, 600);

        SetHits(60000);
        SetStam(415);
        SetMana(22000);

        SetDamage(18, 28);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 40, 60);
        SetResistance(ResistanceType.Fire, 40, 60);
        SetResistance(ResistanceType.Cold, 40, 60);
        SetResistance(ResistanceType.Poison, 40, 60);
        SetResistance(ResistanceType.Energy, 40, 60);

        SetSkill(SkillName.MagicResist, 120);
        SetSkill(SkillName.Tactics, 120);
        SetSkill(SkillName.Wrestling, 120);
        SetSkill(SkillName.Anatomy, 120);
        SetSkill(SkillName.Magery, 120);
        SetSkill(SkillName.EvalInt, 120);
        SetSkill(SkillName.Mysticism, 120);
        SetSkill(SkillName.Focus, 160);

        Fame = 22000;
        Karma = 22000;

        var boots = new ThighBoots { Hue = 1 };

        var scimitar = new Item(5046) { Hue = 1818, Layer = Layer.OneHanded };

        SetNonMovable(boots);
        SetNonMovable(scimitar);
        SetNonMovable(new LongPants(1818));
        SetNonMovable(new FancyShirt(194));
        SetNonMovable(new Doublet(1281));
    }

    public override string DefaultName => "Adrian";

    public override bool InitialInnocent => true;
    public override BaseCreature SummonWisp => new EnsorcledWisp();

    public override void GenerateLoot()
    {
        AddLoot(LootPack.SuperBoss, 3);
    }
}

[SerializationGenerator(0, false)]
public partial class AndrosTheDreadLord : DespiseBoss
{
    [Constructible]
    public AndrosTheDreadLord() : base(AIType.AI_Mage, FightMode.Closest)
    {
        Title = "the Dread Lord";

        Race = Race.Human;
        Body = 0x190;
        Female = false;

        Hue = Race.RandomSkinHue();
        HairItemID = 0;

        SetStr(900, 1200);
        SetDex(500, 600);
        SetInt(500, 600);

        SetHits(60000);
        SetStam(415);
        SetMana(22000);

        SetDamage(18, 28);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 40, 60);
        SetResistance(ResistanceType.Fire, 40, 60);
        SetResistance(ResistanceType.Cold, 40, 60);
        SetResistance(ResistanceType.Poison, 40, 60);
        SetResistance(ResistanceType.Energy, 40, 60);

        SetSkill(SkillName.MagicResist, 120);
        SetSkill(SkillName.Tactics, 120);
        SetSkill(SkillName.Wrestling, 120);
        SetSkill(SkillName.Anatomy, 120);
        SetSkill(SkillName.Magery, 120);
        SetSkill(SkillName.EvalInt, 120);
        SetSkill(SkillName.Mysticism, 120);
        SetSkill(SkillName.Focus, 160);

        Fame = 22000;
        Karma = -22000;

        var boots = new ThighBoots { Hue = 1 };

        var staff = new Item(3721) { Layer = Layer.TwoHanded };

        SetNonMovable(boots);
        SetNonMovable(new LongPants(1818));
        SetNonMovable(new FancyShirt(2726));
        SetNonMovable(new Doublet(1153));
        SetNonMovable(staff);
    }

    public override string DefaultName => "Andros";

    public override bool AlwaysMurderer => true;
    public override BaseCreature SummonWisp => new CorruptedWisp();

    public override void GenerateLoot()
    {
        AddLoot(LootPack.SuperBoss, 3);
    }
}

[SerializationGenerator(0, false)]
public partial class EnsorcledWisp : BaseCreature
{
    public EnsorcledWisp() : base(AIType.AI_Melee, FightMode.None)
    {
        Body = 165;
        Hue = 0x901;
        BaseSoundID = 466;

        SetStr(600, 700);
        SetDex(500, 600);
        SetInt(500, 600);

        SetHits(7000, 8000);

        SetDamage(12, 19);

        SetDamageType(ResistanceType.Physical, 40);
        SetDamageType(ResistanceType.Fire, 30);
        SetDamageType(ResistanceType.Energy, 30);

        SetResistance(ResistanceType.Physical, 50);
        SetResistance(ResistanceType.Fire, 60, 70);
        SetResistance(ResistanceType.Cold, 60, 70);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.MagicResist, 110, 125);
        SetSkill(SkillName.Tactics, 110, 125);
        SetSkill(SkillName.Wrestling, 110, 125);
        SetSkill(SkillName.Anatomy, 110, 125);

        Fame = 8000;
        Karma = 8000;
    }

    public override string DefaultName => "Ensorcled Wisp";

    public override bool InitialInnocent => true;

    public override void OnThink()
    {
        base.OnThink();

        if (ControlTarget != ControlMaster || ControlOrder != OrderType.Follow)
        {
            ControlTarget = ControlMaster;
            ControlOrder = OrderType.Follow;
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 3);
    }

    public override bool OnBeforeDeath()
    {
        Summoned = false;
        PackItem(new Gold(Utility.Random(800, 1000)));
        return base.OnBeforeDeath();
    }
}

[SerializationGenerator(0, false)]
public partial class CorruptedWisp : BaseCreature
{
    public CorruptedWisp() : base(AIType.AI_Melee, FightMode.None)
    {
        Body = 165;
        Hue = 1955;
        BaseSoundID = 466;

        SetStr(600, 700);
        SetDex(500, 600);
        SetInt(500, 600);

        SetHits(7000, 8000);

        SetDamage(12, 19);

        SetDamageType(ResistanceType.Physical, 40);
        SetDamageType(ResistanceType.Fire, 30);
        SetDamageType(ResistanceType.Energy, 30);

        SetResistance(ResistanceType.Physical, 50);
        SetResistance(ResistanceType.Fire, 60, 70);
        SetResistance(ResistanceType.Cold, 60, 70);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.MagicResist, 110, 125);
        SetSkill(SkillName.Tactics, 110, 125);
        SetSkill(SkillName.Wrestling, 110, 125);
        SetSkill(SkillName.Anatomy, 110, 125);

        Fame = 8000;
        Karma = -8000;
    }

    public override string DefaultName => "Corrupted Wisp";

    public override bool AlwaysMurderer => true;

    public override void OnThink()
    {
        base.OnThink();

        if (ControlTarget != ControlMaster || ControlOrder != OrderType.Follow)
        {
            ControlTarget = ControlMaster;
            ControlOrder = OrderType.Follow;
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 3);
    }

    public override bool OnBeforeDeath()
    {
        Summoned = false;
        PackItem(new Gold(Utility.Random(800, 1000)));
        return base.OnBeforeDeath();
    }
}
