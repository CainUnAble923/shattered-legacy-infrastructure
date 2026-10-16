// ServUO: Mobiles/Normal/Viscera.cs (cc-P129). Values verbatim; serialization by the generator.
// AuraOfNausea and StickySkin go to the profile (both ported line for line in cc-P121).

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Viscera : BaseCreature
{
    [Constructible]
    public Viscera() : base(AIType.AI_Melee)
    {
        Body = 775;
        Hue = 2718;
        BaseSoundID = 679;
        SpeechHue = 0x3B2;

        SetStr(250, 293);
        SetDex(120, 130);
        SetInt(100);

        SetHits(200, 230);

        SetDamage(8, 18);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Poison, 50);

        SetResistance(ResistanceType.Physical, 30, 50);
        SetResistance(ResistanceType.Fire, 20, 30);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 20, 30);
        SetResistance(ResistanceType.Energy, 30, 40);

        SetSkill(SkillName.MagicResist, 60.0, 80.0);
        SetSkill(SkillName.Tactics, 85.0, 100.0);
        SetSkill(SkillName.Wrestling, 85.0, 100.0);
        SetSkill(SkillName.Poisoning, 20.0, 30.0);
        SetSkill(SkillName.DetectHidden, 50.0);

        Fame = 2000;
        Karma = -2000;

        VirtualArmor = 50;

        SetAreaEffect(AreaEffect.AuraOfNausea);
        SetSpecialAbility(SpecialAbility.StickySkin);
    }

    public override string CorpseName => "a Visceral corpse";
    public override string DefaultName => "Viscera";

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich);
        AddLoot(LootPack.Gems, Utility.Random(1, 3));
    }

    public override Poison PoisonImmune => Poison.Lethal;
    public override Poison HitPoison => Poison.Lethal;

    public override int GetIdleSound() => 0x1BF;
    public override int GetAttackSound() => 0x1C0;
    public override int GetHurtSound() => 0x1C1;
    public override int GetDeathSound() => 0x1C2;
}
