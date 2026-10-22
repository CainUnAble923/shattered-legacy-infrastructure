// ServUO: Services/Expansions/Time Of Legends/Mobiles/EodonTribesman.cs (cc-P198). One file as in ServUO: the
// EodonTribe enum, BaseEodonTribesman and its three tribesmen. Values verbatim; serialization by the generator, the
// tribe the one persisted field ([SerializableField(0)], ServUO's version 0 + the int it writes).
//
// What a tribesman does: spawned innocent (InitialInnocent), it fights only what has attacked it (IsEnemy is "is an
// aggressor of mine", ServUO's own comment: "basically FightMode.Aggressor"); when attacked, every idle tribesman of
// the same tribe within its perception joins in, and each one that has not yelled for 3-6 minutes yells
// "Ahhhh-OOOO! Ahhh-OOOO!" (1156584). Each tribe has its own body, hue, outfit, weapon and one 100% resistance.
// Names come from pinned's "savage", "savage rider" and "savage shaman" lists (Data/names.json), as ServUO's.
//
// Changed or dropped, each in the notes:
//   - Masteries: ServUO hands the creature AI the non-passive masteries of its weapon's skill. Skill Masteries are
//     not on this shard (BasePeerless.cs states the same absence), so there is nothing to hand; dropped with
//     _NextMastery, which nothing in this file read.
//   - IsEnemy's Myrmidex Invasion branch (MyrmidexInvasionSystem.Active && AreEnemies): the invasion is out of
//     scope; ServUO with it inactive runs exactly the aggressor test kept here.
//   - ShowSpellMantra (shamans): pinned's Spell.SayMantra speaks only for players (Spell.cs:457) and has no
//     creature switch, so shamans cast silently. Not patched here.
//   - Initialize built the name pool at startup; here it is built on first use, so a test host (no Initialize) and
//     the server read the same lists.
//   - The perception 10 and speeds are dropped (port recipe). The chieftain's name reads "chieftan" whatever the
//     sex: ServUO formats it before BuildBody sets Female, so it never says "chieftess". Kept.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Network;
using Server.Regions;

namespace Server.Mobiles;

public enum EodonTribe
{
    Jukari,
    Kurak,
    Barrab,
    Barako,
    Urali,
    Sakkhra
}

[SerializationGenerator(0, false)]
public abstract partial class BaseEodonTribesman : BaseCreature
{
    private static string[] _names;

    [SerializableField(0, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private EodonTribe _tribeType;

    private bool _hasYelled;

    public BaseEodonTribesman(AIType ai, EodonTribe type) : base(ai)
    {
        _tribeType = type;

        BuildBody();
        BuildEquipment();

        switch (type)
        {
            case EodonTribe.Jukari:
                SetResistance(ResistanceType.Physical, 20, 30);
                SetResistance(ResistanceType.Fire, 100);
                SetResistance(ResistanceType.Cold, 10, 20);
                SetResistance(ResistanceType.Poison, 40, 50);
                SetResistance(ResistanceType.Energy, 40, 50);
                break;
            case EodonTribe.Kurak:
                SetResistance(ResistanceType.Physical, 20, 30);
                SetResistance(ResistanceType.Fire, 10, 20);
                SetResistance(ResistanceType.Cold, 100);
                SetResistance(ResistanceType.Poison, 40, 50);
                SetResistance(ResistanceType.Energy, 40, 50);
                break;
            case EodonTribe.Barrab:
                SetResistance(ResistanceType.Physical, 20, 30);
                SetResistance(ResistanceType.Fire, 40, 50);
                SetResistance(ResistanceType.Cold, 40, 50);
                SetResistance(ResistanceType.Poison, 100);
                SetResistance(ResistanceType.Energy, 10, 20);
                break;
            case EodonTribe.Barako:
                SetResistance(ResistanceType.Physical, 20, 30);
                SetResistance(ResistanceType.Fire, 40, 50);
                SetResistance(ResistanceType.Cold, 40, 50);
                SetResistance(ResistanceType.Poison, 10, 20);
                SetResistance(ResistanceType.Energy, 40, 50);
                break;
            case EodonTribe.Urali:
                SetResistance(ResistanceType.Physical, 20, 30);
                SetResistance(ResistanceType.Fire, 40, 50);
                SetResistance(ResistanceType.Cold, 40, 50);
                SetResistance(ResistanceType.Poison, 10, 20);
                SetResistance(ResistanceType.Energy, 100);
                break;
            case EodonTribe.Sakkhra:
                SetResistance(ResistanceType.Physical, 100);
                SetResistance(ResistanceType.Fire, 40, 50);
                SetResistance(ResistanceType.Cold, 40, 50);
                SetResistance(ResistanceType.Poison, 60, 70);
                SetResistance(ResistanceType.Energy, 40, 50);
                break;
        }
    }

    [CommandProperty(AccessLevel.GameMaster)]
    public bool HasYelled
    {
        get => _hasYelled;
        set
        {
            if (value != _hasYelled)
            {
                _hasYelled = value;

                if (_hasYelled)
                {
                    Timer.StartTimer(TimeSpan.FromSeconds(Utility.RandomMinMax(180, 360)), () => _hasYelled = false);
                }
            }
        }
    }

    public override Poison PoisonImmune => TribeType == EodonTribe.Barrab ? Poison.Deadly : null;

    public override bool InitialInnocent => true;
    public override int TreasureMapLevel => 2;

    public abstract void BuildBody();
    public abstract void BuildEquipment();

    public static string GetRandomName()
    {
        if (_names == null)
        {
            var names = new List<string>();

            foreach (var list in new[] { "savage", "savage rider", "savage shaman" })
            {
                var nameList = NameList.GetNameList(list);

                if (nameList?.List != null)
                {
                    names.AddRange(nameList.List);
                }
            }

            _names = names.ToArray();
        }

        return _names.Length > 0 ? _names.RandomElement() : "";
    }

    public override void AggressiveAction(Mobile aggressor, bool criminal)
    {
        base.AggressiveAction(aggressor, criminal);

        if (Map == null)
        {
            return;
        }

        foreach (var tribesman in Map.GetMobilesInRange<BaseEodonTribesman>(Location, RangePerception))
        {
            if (tribesman != this && tribesman != aggressor && tribesman.TribeType == TribeType &&
                tribesman.Combatant == null)
            {
                tribesman.Warmode = true;
                tribesman.Combatant = aggressor;

                if (!tribesman.HasYelled)
                {
                    tribesman.PublicOverheadMessage(MessageType.Regular, 0x47E, 1156584); // Ahhhh-OOOO! Ahhh-OOOO!
                    tribesman.HasYelled = true;
                }
            }
        }
    }

    public override bool IsEnemy(Mobile m)
    {
        // ServUO: "Basically, this makes them FightMode.Agressor."
        for (var i = 0; i < Aggressors.Count; i++)
        {
            if (Aggressors[i].Attacker == m)
            {
                return true;
            }
        }

        return false;
    }

    public override WeaponAbility GetWeaponAbility()
    {
        if (Weapon is not BaseWeapon wep)
        {
            return null;
        }

        return Utility.RandomBool() ? wep.PrimaryAbility : wep.SecondaryAbility;
    }
}

[SerializationGenerator(0, false)]
public partial class TribeWarrior : BaseEodonTribesman
{
    [Constructible]
    public TribeWarrior(EodonTribe type) : base(AIType.AI_Melee, type)
    {
    }

    public override bool AlwaysAttackable => Region.IsPartOf<BattleRegion>();
    public override bool ShowFameTitle => false;

    public override void BuildBody()
    {
        Name = $"{GetRandomName()} the {TribeType} warrior";

        SetStr(150);
        SetDex(150);
        SetInt(75);

        SetHits(2500);

        SetDamage(10, 23);

        SetDamageType(ResistanceType.Physical, 100);

        SetSkill(SkillName.Wrestling, 100, 120);
        SetSkill(SkillName.Fencing, 100, 120);
        SetSkill(SkillName.Swords, 100, 120);
        SetSkill(SkillName.Macing, 100, 120);
        SetSkill(SkillName.Archery, 100, 120);

        SetSkill(SkillName.Tactics, 100, 120);
        SetSkill(SkillName.Anatomy, 100, 120);
        SetSkill(SkillName.MagicResist, 100, 120);
        SetSkill(SkillName.Parry, 120);

        switch (TribeType)
        {
            case EodonTribe.Jukari:
                Female = Utility.RandomBool();
                Body = Female ? 0x191 : 0x190;
                HairItemID = 0;
                Hue = 34723;
                break;
            case EodonTribe.Kurak:
                Female = Utility.RandomBool();
                Body = Female ? 0x191 : 0x190;
                HairItemID = 0x2047;
                Hue = 33960;
                break;
            case EodonTribe.Barrab:
                Female = false;
                Body = 0x190;
                HairItemID = 0x203B;
                Hue = 34214;
                break;
            case EodonTribe.Barako:
                Female = Utility.RandomBool();
                Body = Female ? 0x191 : 0x190;
                HairItemID = 0x203C;
                Hue = 35187;
                break;
            case EodonTribe.Urali:
                Female = true;
                Body = 0x25E;
                Race = Race.Elf;
                HairItemID = 0x2FC1;
                Hue = 35356;
                break;
            case EodonTribe.Sakkhra:
                Female = Utility.RandomBool();
                Body = Female ? 0x191 : 0x190;
                HairItemID = 0x203C;
                Hue = 34894;
                RangeFight = 7;
                break;
        }

        Fame = 12000;
        Karma = 8000;
    }

    public override void BuildEquipment()
    {
        Item weapon = null;

        switch (TribeType)
        {
            default:
            case EodonTribe.Jukari:
                weapon = new Pickaxe();
                weapon.Hue = 1175;
                if (Female)
                {
                    this.SetWearable(new LeatherShorts(), 1175);
                    this.SetWearable(new LeatherBustierArms(), 1175);
                }
                else
                {
                    this.SetWearable(new LeatherLegs(), 1175);
                    this.SetWearable(new BodySash(), 1175);
                }

                this.SetWearable(new Torch());
                break;
            case EodonTribe.Kurak:
                weapon = new Tekagi();
                this.SetWearable(new LeatherDo());
                this.SetWearable(new PlateMempo(), 1192);
                this.SetWearable(new ShortPants(), 1192);
                this.SetWearable(new Sandals(), 1192);
                break;
            case EodonTribe.Barrab:
                weapon = new Spear();
                this.SetWearable(new PlateDo(), 1828);
                this.SetWearable(new Obi(), 1828);
                this.SetWearable(new PlateSuneate(), 1828);
                this.SetWearable(new DecorativePlateKabuto(), 1834);
                this.SetWearable(new SilverEarrings());
                this.SetWearable(new Sandals(), 1828);
                break;
            case EodonTribe.Barako:
                if (Female)
                {
                    weapon = new Maul();
                    this.SetWearable(new DeerMask(), 2414);
                }
                else
                {
                    weapon = new WarMace();
                    this.SetWearable(new BearMask(), 2414);
                }

                weapon.Hue = 2414;
                this.SetWearable(new StuddedChest(), 2414);
                this.SetWearable(new StuddedArms(), 2414);
                this.SetWearable(new StuddedLegs(), 2414);
                this.SetWearable(new StuddedGorget(), 2414);
                this.SetWearable(new LeatherNinjaMitts(), 2414);
                this.SetWearable(new Boots(), 2414);
                break;
            case EodonTribe.Urali:
                this.SetWearable(new DragonChest(), 2576);
                this.SetWearable(new LeatherJingasa(), 2576);
                this.SetWearable(new MetalShield(), 2576);
                this.SetWearable(new Waraji(), 2576);
                this.SetWearable(new ChainLegs(), 2576);
                break;
            case EodonTribe.Sakkhra:
                weapon = new Bow();
                weapon.Hue = 2125;
                if (Female)
                {
                    this.SetWearable(new LeatherBustierArms(), 2128);
                    this.SetWearable(new LeatherSkirt(), 2125);
                }
                else
                {
                    this.SetWearable(new LeatherChest(), 2128);
                    this.SetWearable(new SkullCap(), 2125);
                    this.SetWearable(new Kilt(), 2125);
                }

                this.SetWearable(new ThighBoots(), 2129);
                break;
        }

        if (weapon != null)
        {
            weapon.LootType = LootType.Blessed;
            this.SetWearable(weapon);
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich, 2);
    }
}

[SerializationGenerator(0, false)]
public partial class TribeShaman : BaseEodonTribesman
{
    [Constructible]
    public TribeShaman(EodonTribe type) : base(AIType.AI_Mage, type)
    {
        RangeFight = 7;

        PackGold(60, 70);
        PackReg(1, 3);

        PackItem(new Bandage(Utility.RandomMinMax(3, 5)));
    }

    public override bool AlwaysAttackable => Region.IsPartOf<BattleRegion>();
    public override bool ShowFameTitle => false;

    public override void BuildBody()
    {
        Name = $"{GetRandomName()} the {TribeType} shaman";

        SetStr(125);
        SetDex(75, 100);
        SetInt(200, 250);

        SetHits(2500);

        SetDamage(10, 15);

        SetDamageType(ResistanceType.Physical, 100);

        SetSkill(SkillName.Wrestling, 100);
        SetSkill(SkillName.Fencing, 100);
        SetSkill(SkillName.Swords, 100);
        SetSkill(SkillName.Macing, 100);
        SetSkill(SkillName.Archery, 100);

        SetSkill(SkillName.Tactics, 100);
        SetSkill(SkillName.Anatomy, 100);
        SetSkill(SkillName.MagicResist, 100);
        SetSkill(SkillName.Magery, 120);
        SetSkill(SkillName.EvalInt, 120);

        switch (TribeType)
        {
            case EodonTribe.Jukari:
                Female = Utility.RandomBool();
                Body = Female ? 0x191 : 0x190;
                HairItemID = 0;
                Hue = 34723;
                break;
            case EodonTribe.Kurak:
                Female = Utility.RandomBool();
                Body = Female ? 0x191 : 0x190;
                HairItemID = Female ? 0x203C : 0x203B;
                Hue = 33960;
                break;
            case EodonTribe.Barrab:
                Female = true;
                Body = 0x191;
                HairItemID = 0x203B;
                Hue = 34214;
                break;
            case EodonTribe.Barako:
                Female = Utility.RandomBool();
                Body = Female ? 0x191 : 0x190;
                HairItemID = 0x203C;
                Hue = 35187;
                break;
            case EodonTribe.Urali:
                Female = false;
                Body = 0x25D;
                Race = Race.Elf;
                HairItemID = 0x2FC1;
                Hue = 35356;
                break;
            case EodonTribe.Sakkhra:
                Female = Utility.RandomBool();
                Body = Female ? 0x191 : 0x190;
                HairItemID = 0x203C;
                Hue = 34894;
                RangeFight = 7;
                break;
        }

        Fame = 12000;
        Karma = 8000;
    }

    public override void BuildEquipment()
    {
        Item weapon = new WildStaff();

        switch (TribeType)
        {
            default:
            case EodonTribe.Jukari:
                this.SetWearable(new FemaleLeatherChest(), 1933);
                this.SetWearable(new LeatherSkirt(), 1933);
                this.SetWearable(new Torch());
                weapon.Hue = 1933;
                break;
            case EodonTribe.Kurak:
                this.SetWearable(new LeatherDo(), 1150);
                this.SetWearable(new PlateMempo(), 1150);
                this.SetWearable(new TattsukeHakama(), 1150);
                this.SetWearable(new Sandals(), 1150);
                weapon.Hue = 1150;
                break;
            case EodonTribe.Barrab:
                var robe = new Robe();
                robe.ItemID = 9860;
                this.SetWearable(robe, 1834);
                this.SetWearable(new Obi(), 1834);
                this.SetWearable(new Sandals(), 1831);
                weapon.Hue = 1831;
                break;
            case EodonTribe.Barako:
                this.SetWearable(new LeatherHiroSode(), 1518);
                this.SetWearable(new LeatherGloves(), 1518);
                this.SetWearable(new TribalMask(), 1518);
                this.SetWearable(new LeatherNinjaPants(), 1518);
                this.SetWearable(new BoneChest(), 1518);
                this.SetWearable(new StuddedGorget(), 1518);
                this.SetWearable(new Boots(), 1518);
                this.SetWearable(new Surcoat(), 1518);
                weapon.Hue = 1518;
                break;
            case EodonTribe.Urali:
                this.SetWearable(new DragonLegs(), 2576);
                this.SetWearable(new GoldEarrings());
                this.SetWearable(new NinjaTabi(), 2576);
                weapon.Hue = 2576;
                break;
            case EodonTribe.Sakkhra:
                this.SetWearable(new StuddedChest(), 2118);
                this.SetWearable(new LeatherArms(), 2106);
                this.SetWearable(new LeatherGloves(), 2106);
                this.SetWearable(new SkullCap(), 2118);
                this.SetWearable(new RingmailLegs(), 2106);
                this.SetWearable(new ThighBoots(), 2106);
                weapon.Hue = 2118;
                break;
        }

        weapon.LootType = LootType.Blessed;
        this.SetWearable(weapon);
    }
}

[SerializationGenerator(0, false)]
public partial class TribeChieftan : BaseEodonTribesman
{
    [Constructible]
    public TribeChieftan(EodonTribe type) : base(AIType.AI_Melee, type)
    {
    }

    public override void BuildBody()
    {
        // Female is read before the switch below sets it, as in ServUO: always "chieftan" (header).
        Name = $"{GetRandomName()} the {TribeType} {(Female ? "chieftess" : "chieftan")}";

        SetStr(200);
        SetDex(2000);
        SetInt(200, 250);

        SetHits(4500);

        SetDamage(15, 28);

        SetDamageType(ResistanceType.Physical, 100);

        SetSkill(SkillName.Wrestling, 120);
        SetSkill(SkillName.Fencing, 120);
        SetSkill(SkillName.Swords, 120);
        SetSkill(SkillName.Macing, 120);
        SetSkill(SkillName.Archery, 120);

        SetSkill(SkillName.Tactics, 120);
        SetSkill(SkillName.Anatomy, 120);
        SetSkill(SkillName.MagicResist, 120);
        SetSkill(SkillName.Parry, 120);

        switch (TribeType)
        {
            case EodonTribe.Jukari:
                Female = Utility.RandomBool();
                Body = Female ? 0x191 : 0x190;
                HairItemID = 0;
                Hue = 34723;
                break;
            case EodonTribe.Kurak:
                Female = Utility.RandomBool();
                Body = Female ? 0x191 : 0x190;
                HairItemID = Female ? 0x2046 : 0x203B;
                Hue = 33960;
                break;
            case EodonTribe.Barrab:
                Female = true;
                Body = 0x191;
                HairItemID = 0x203B;
                Hue = 34214;
                break;
            case EodonTribe.Barako:
                Female = Utility.RandomBool();
                Body = Female ? 0x191 : 0x190;
                HairItemID = 0x203C;
                Hue = 35187;
                break;
            case EodonTribe.Urali:
                Female = true;
                Body = 0x25E;
                Race = Race.Elf;
                HairItemID = 0x2FD0;
                Hue = 35356;
                break;
            case EodonTribe.Sakkhra:
                Female = Utility.RandomBool();
                Body = Female ? 0x191 : 0x190;
                HairItemID = 0x203C;
                Hue = 34894;
                RangeFight = 7;
                break;
        }

        Fame = 18000;
        Karma = 8000;
    }

    public override void BuildEquipment()
    {
        Item weapon = null;

        switch (TribeType)
        {
            default:
            case EodonTribe.Jukari:
                this.SetWearable(new LeatherLegs(), 1175);
                this.SetWearable(new Shirt(), 1175);
                this.SetWearable(new Torch());
                weapon = new Bokuto();
                weapon.Hue = 1175;
                break;
            case EodonTribe.Kurak:
                this.SetWearable(new LeatherDo(), 1175);
                this.SetWearable(new FancyShirt(), 1175);
                this.SetWearable(new TattsukeHakama());
                this.SetWearable(new Sandals(), 1175);
                weapon = new Tekagi();
                weapon.Hue = 1175;
                break;
            case EodonTribe.Barrab:
                this.SetWearable(new PlateDo(), 1828);
                this.SetWearable(new PlateSuneate(), 1828);
                this.SetWearable(new DecorativePlateKabuto(), 1834);
                this.SetWearable(new Sandals(), 1828);
                weapon = new Spear();
                weapon.Hue = 1828;
                break;
            case EodonTribe.Barako:
                this.SetWearable(new BoneChest(), 2407);
                this.SetWearable(new LeatherNinjaPants(), 2407);
                this.SetWearable(new StuddedHiroSode(), 2407);
                this.SetWearable(new BoneGloves(), 2407);
                this.SetWearable(new StuddedGorget(), 2407);
                this.SetWearable(new Boots(), 2407);
                weapon = new Scepter();
                weapon.Hue = 2407;
                break;
            case EodonTribe.Urali:
                this.SetWearable(new ChainLegs(), 2576);
                this.SetWearable(new DragonChest(), 2576);
                this.SetWearable(new DragonArms(), 2576);
                this.SetWearable(new MetalShield(), 2576);
                this.SetWearable(new Circlet(), 2576);
                this.SetWearable(new JinBaori(), 2592);
                this.SetWearable(new Waraji(), 2576);
                break;
            case EodonTribe.Sakkhra:
                this.SetWearable(new StuddedChest(), 2118);
                this.SetWearable(new LeatherArms(), 2106);
                this.SetWearable(new LeatherGloves(), 2106);
                this.SetWearable(new SkullCap(), 2118);
                this.SetWearable(new RingmailLegs(), 2106);
                this.SetWearable(new ThighBoots(), 2106);
                weapon = new Yumi();
                weapon.Hue = 2118;
                break;
        }

        if (weapon != null)
        {
            weapon.LootType = LootType.Blessed;
            this.SetWearable(weapon);
        }
    }

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 2);
    }
}
