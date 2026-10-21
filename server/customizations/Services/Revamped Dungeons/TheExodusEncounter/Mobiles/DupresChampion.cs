// ServUO: Services/Revamped Dungeons/TheExodusEncounter/Mobiles/DupresChampion.cs (cc-P195): Lord Dupre's champion,
// in gold plate with a cutlass and the purple sash and cloak, fighting Exodus's minions in the dungeon; aggressor
// mode, so he answers an attack. One kill in ten hands the killer a ritual key and one loot roll in ten packs one.
// Conversion notes: SetWearable is ExodusEncounter.Wear (blessed, never dropped); CanBeParagon has no pinned
// virtual, so OnBeforeSpawn undoes a paragon conversion (Paragon.CheckConvert runs there, and the dungeon is on
// Ilshenar, the one paragon facet); the recipe's transformation for the rest.

using ModernUO.Serialization;
using Server.Engines.Exodus;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class DupresChampion : BaseCreature
{
    public const int GoldHue = 0x8A5;
    public const int DarkPurple = 0x486;

    [Constructible]
    public DupresChampion() : base(AIType.AI_Melee, FightMode.Aggressor)
    {
        Name = NameList.RandomName("male");
        Title = "The Champion";
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

        Wear(new Cutlass());
        Wear(new PlateHelm(), GoldHue);
        Wear(new PlateArms(), GoldHue);
        Wear(new PlateGorget(), GoldHue);
        Wear(new PlateGloves(), GoldHue);
        Wear(new PlateLegs(), GoldHue);
        Wear(new PlateChest(), GoldHue);
        Wear(new MetalKiteShield(), 0x776);
        Wear(new BodySash(DarkPurple));
        Wear(new Cloak(DarkPurple));

        PackGold(400, 600);
    }

    private void Wear(Item item, int hue = -1)
    {
        item.LootType = LootType.Blessed;
        ExodusEncounter.Wear(this, item, hue);
    }

    public override string CorpseName => "a human corpse";

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

    public override void GenerateLoot()
    {
        if (Utility.RandomDouble() < 0.1)
        {
            PackItem(ExodusChest.RandomRitualItem());
        }
    }
}
