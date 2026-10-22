// ServUO: Services/Expansions/Time Of Legends/Mobiles/DesertScorpion.cs (cc-P198). Values verbatim; serialization by
// the generator.
//
// ServUO calls SetDamageType(Physical, 60) and then SetDamageType(Physical, 40); the second call wins, so it deals
// 40% physical and no other type. Kept as written (the fidelity principle: what ServUO runs, not what it meant).

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class DesertScorpion : BaseCreature
{
    [Constructible]
    public DesertScorpion() : base(AIType.AI_Melee)
    {
        Body = 0x2CD;
        BaseSoundID = 397;

        SetStr(600, 700);
        SetDex(120, 128);
        SetInt(150, 200);

        SetDamage(15, 25);

        SetHits(350, 400);

        SetResistance(ResistanceType.Physical, 30, 40);
        SetResistance(ResistanceType.Fire, 50, 60);
        SetResistance(ResistanceType.Cold, 10, 20);
        SetResistance(ResistanceType.Poison, 70, 80);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetDamageType(ResistanceType.Physical, 60);
        SetDamageType(ResistanceType.Physical, 40);

        SetSkill(SkillName.MagicResist, 60, 70);
        SetSkill(SkillName.Tactics, 80, 90);
        SetSkill(SkillName.Wrestling, 50, 60);
        SetSkill(SkillName.Poisoning, 110, 120);

        Fame = 8100;
        Karma = -8100;
    }

    public override string CorpseName => "a desert scorpion corpse";
    public override string DefaultName => "a desert scorpion";

    public override Poison HitPoison => Poison.Lethal;
    public override Poison PoisonImmune => Poison.Lethal;
    public override int Meat => 3;
    public override int TreasureMapLevel => 1;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich);
    }
}
