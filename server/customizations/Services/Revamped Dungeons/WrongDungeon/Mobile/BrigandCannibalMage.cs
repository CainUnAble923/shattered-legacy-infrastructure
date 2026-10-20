// ServUO: Services/Revamped Dungeons/WrongDungeon/Mobile/BrigandCannibalMage.cs (cc-P194). A stock EvilMage with
// cannibal stats and 94 Necromancy.
//
// Conversion notes: ServUO switches it to AI_NecroMage; pinned has no such AIType and its MageAI casts Necromancy
// on a creature with more than 50 Necromancy (MageAI.cs:70, the Q-050 finding), so the stock AI_Mage of EvilMage
// stays. SetSpecialAbility(LifeLeech) is MonsterAbilities.DrainLifeAttack, ModernUO's standard life drain
// (the D-40 mapping from Shame).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class BrigandCannibalMage : EvilMage
{
    [Constructible]
    public BrigandCannibalMage()
    {
        Title = "the brigand cannibal mage";

        SetStr(68, 95);
        SetDex(81, 95);
        SetInt(110, 115);

        SetHits(2058, 2126);
        SetMana(552, 553);

        SetDamage(10, 23);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50, 60);
        SetResistance(ResistanceType.Fire, 50, 60);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 50, 60);
        SetResistance(ResistanceType.Energy, 50, 60);

        SetSkill(SkillName.MagicResist, 96.9, 96.9);
        SetSkill(SkillName.Tactics, 94.0, 94.0);
        SetSkill(SkillName.Wrestling, 54.3, 54.3);
        SetSkill(SkillName.Necromancy, 94.0, 94.0);
        SetSkill(SkillName.SpiritSpeak, 54.3, 54.3);

        Fame = 14500;
        Karma = -14500;

        if (Utility.RandomDouble() < 0.75)
        {
            PackItem(new SeveredHumanEars());
        }
    }

    public override string CorpseName => "an archmage corpse";

    public override MonsterAbility[] GetMonsterAbilities() => [MonsterAbilities.DrainLifeAttack];
}
