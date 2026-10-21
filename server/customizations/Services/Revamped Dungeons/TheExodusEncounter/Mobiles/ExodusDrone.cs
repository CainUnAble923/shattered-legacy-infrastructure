// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Mobiles/ExodusDrone.cs (cc-P195): the small clockwork of the
// Exodus Dungeon, ten to a spawner. ServUO hand-rolls the Exodus minions' magical barrier (no melee damage while above
// 90 percent, no spell damage below it) and the energy bolt counter (50 energy at a spellcaster or an archer, 40
// percent) in each file; pinned has both as monster abilities (Mobiles/Abilities/MagicalBarrier.cs,
// EnergyBoltCounter.cs, the ones pinned's own ExodusMinion chooses between by era), so each of the four uses the two
// abilities and keeps ServUO's behaviour, which runs both at once. One kill in ten hands the killer a ritual key.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class ExodusDrone : BaseCreature
{
    private static readonly MonsterAbility[] _abilities = { MonsterAbilities.MagicalBarrier, MonsterAbilities.EnergyBoltCounter };

    [Constructible]
    public ExodusDrone() : base(AIType.AI_Melee)
    {
        Body = 0x2F4;
        Hue = 0xA92;

        SetStr(554, 650);
        SetDex(77, 85);
        SetInt(64, 85);

        SetHits(332, 389);
        SetDamage(13, 19);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Energy, 50);

        SetResistance(ResistanceType.Physical, 45, 55);
        SetResistance(ResistanceType.Fire, 40, 60);
        SetResistance(ResistanceType.Cold, 25, 35);
        SetResistance(ResistanceType.Poison, 25, 34);
        SetResistance(ResistanceType.Energy, 25, 35);

        SetSkill(SkillName.MagicResist, 83.8, 95.4);
        SetSkill(SkillName.Tactics, 82.9, 94.9);
        SetSkill(SkillName.Wrestling, 84.7, 95.9);

        Fame = 18000;
        Karma = -18000;
        VirtualArmor = 65;

        PackItem(new PowerCrystal());
        PackItem(new ArcaneGem());
        PackItem(new ClockworkAssembly());
    }

    public override string CorpseName => "a drone's corpse";
    public override string DefaultName => "Exodus Drone";

    public override bool IsScaredOfScaryThings => false;
    public override bool IsScaryToPets => true;
    public override bool BardImmune => !Core.AOS;
    public override Poison PoisonImmune => Poison.Lethal;

    public override MonsterAbility[] GetMonsterAbilities() => _abilities;

    public override int GetIdleSound() => 0x218;
    public override int GetAngerSound() => 0x26C;
    public override int GetDeathSound() => 0x211;
    public override int GetAttackSound() => 0x232;
    public override int GetHurtSound() => 0x140;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
    }

    public override void OnKilledBy(Mobile m)
    {
        base.OnKilledBy(m);

        if (Utility.RandomDouble() < 0.1)
        {
            ExodusChest.GiveRituelItem(m);
        }
    }
}
