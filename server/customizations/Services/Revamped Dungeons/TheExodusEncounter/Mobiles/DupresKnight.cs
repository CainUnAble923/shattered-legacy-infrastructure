// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Mobiles/DupresKnight.cs (cc-P195): Lord Dupre's knight, in
// plain plate with a longsword, blue (InitialInnocent) and aggressor-only. One kill in ten hands the killer a ritual
// key. Conversion notes as DupresChampion.cs.

using ModernUO.Serialization;
using Server.Engines.Exodus;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class DupresKnight : BaseCreature
{
    public const int SashHue = 0x794;

    [Constructible]
    public DupresKnight() : base(AIType.AI_Melee, FightMode.Aggressor)
    {
        Name = NameList.RandomName("male");
        Title = "The Knight";
        Body = 0x190;
        Hue = Race.Human.RandomSkinHue();
        Female = false;

        SetStr(190, 200);
        SetDex(50, 75);
        SetInt(150, 250);
        SetHits(3900, 4100);
        SetDamage(22, 28);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 50, 70);
        SetResistance(ResistanceType.Fire, 50, 70);
        SetResistance(ResistanceType.Cold, 50, 70);
        SetResistance(ResistanceType.Poison, 50, 70);
        SetResistance(ResistanceType.Energy, 50, 70);

        SetSkill(SkillName.EvalInt, 195.0, 220.0);
        SetSkill(SkillName.Magery, 195.0, 220.0);
        SetSkill(SkillName.Meditation, 195.0, 200.0);
        SetSkill(SkillName.MagicResist, 100.0, 120.0);
        SetSkill(SkillName.Tactics, 195.0, 220.0);
        SetSkill(SkillName.Wrestling, 195.0, 220.0);

        VirtualArmor = 70;

        Wear(new Longsword());
        Wear(new PlateHelm());
        Wear(new PlateArms());
        Wear(new PlateGorget());
        Wear(new PlateGloves());
        Wear(new PlateLegs());
        Wear(new PlateChest());
        Wear(new MetalKiteShield(), SashHue);
        Wear(new BodySash(SashHue));

        PackGold(400, 600);
    }

    private void Wear(Item item, int hue = -1)
    {
        item.LootType = LootType.Blessed;
        ExodusEncounter.Wear(this, item, hue);
    }

    public override string CorpseName => "a human corpse";

    public override bool InitialInnocent => true;
    public override Poison PoisonImmune => Poison.Lethal;
    public override int TreasureMapLevel => 5;

    public override void OnBeforeSpawn(Point3D location, Map m)
    {
        base.OnBeforeSpawn(location, m);

        if (IsParagon)
        {
            IsParagon = false; // ServUO CanBeParagon => false
        }
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
