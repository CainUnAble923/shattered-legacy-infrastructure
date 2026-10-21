// ServUO: Services/Revamped Dungeons/WrongDungeon/Mobile/Fezzik.cs (cc-P194).
//
// Fezzik the Ogre Cook. Half the melee hits and half the spells that land on him drop a Stinking Cauldron at his
// feet (two minutes apart at most), which bursts into Gooey Maggots.
//
// Deviation (notes): ServUO drops RecipeScroll(603), the Three Tiered Cake cooking recipe of its 2016 cooking
// revamp, one death in ten. Neither pinned nor the shard's cooking table has that recipe (CookingCraftRegistrations),
// so a scroll for it would be a scroll for nothing; the drop waits for the recipe.
//
// Conversion notes: OnGotMeleeAttack takes a damage argument here; the cauldron timer is in StinkingCauldron.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Fezzik : BaseCreature
{
    private DateTime _stinkingCauldronTime;

    [Constructible]
    public Fezzik() : base(AIType.AI_Melee)
    {
        Title = "The Ogre Cook";
        Body = 1;
        BaseSoundID = 427;

        SetStr(1142, 1381);
        SetDex(73, 90);
        SetInt(52, 84);

        SetMana(0);

        SetDamage(25, 30);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 75, 80);
        SetResistance(ResistanceType.Fire, 70, 75);
        SetResistance(ResistanceType.Cold, 65, 75);
        SetResistance(ResistanceType.Poison, 55, 65);
        SetResistance(ResistanceType.Energy, 65, 75);

        SetSkill(SkillName.MagicResist, 133.3, 151.9);
        SetSkill(SkillName.Tactics, 120.3, 130.0);
        SetSkill(SkillName.Wrestling, 122.2, 128.9);
        SetSkill(SkillName.Anatomy, 10.0, 15.0);
        SetSkill(SkillName.DetectHidden, 90.0);
        SetSkill(SkillName.Parry, 95.0, 100.0);

        Fame = 3000;
        Karma = -3000;

        VirtualArmor = 52;

        PackItem(new Club());
    }

    public override string CorpseName => "an ogre corpse";
    public override string DefaultName => "Fezzik";

    public override int Meat => 2;
    public override int TreasureMapLevel => 3;

    public override void AlterDamageScalarFrom(Mobile caster, ref double scalar)
    {
        if (0.5 >= Utility.RandomDouble())
        {
            SpawnGreenGoo();
        }
    }

    public override void OnGotMeleeAttack(Mobile attacker, int damage)
    {
        base.OnGotMeleeAttack(attacker, damage);

        if (0.5 >= Utility.RandomDouble())
        {
            SpawnGreenGoo();
        }
    }

    public void SpawnGreenGoo()
    {
        if (_stinkingCauldronTime <= Core.Now && Map != null && Map != Map.Internal)
        {
            new StinkingCauldron().MoveToWorld(Location, Map);

            _stinkingCauldronTime = Core.Now + TimeSpan.FromMinutes(2);
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager, 2);
    }
}
