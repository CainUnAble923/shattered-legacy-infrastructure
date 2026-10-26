// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Creatures/JackThePumpkinKing.cs (cc-P408). Jack the
// Pumpkin King, the last of the nine bosses the Treasures of Sorcerer's Dungeon spawner rotates through, and his
// unholy pumpkins.
//
// ServUO's comment says it: the damage handler is "gleefully stolen from DemonKnight". Pinned's DemonKnight
// (Mobiles/Monsters/AOS/DemonKnight.cs:229-288) and UnholyBone (Items/Misc/UnholyBone.cs) are the pinned shape of
// the same code, so this file follows them line for line: every hit Jack takes deals 8 to 20 physical back to the
// attacker with a pumpkin thrown at him, and 5% of the time, a second later, 1 to 3 unholy pumpkins drop around the
// attacker; each hatches an undead (one of twelve) after 5 to 10 seconds unless carved first (30%: 25-100 gold).
//
// The port recipe: AI_NecroMage is AI_Mage (Q-050: pinned MageAI casts Necromancy from the creature's own skill);
// base(..., 10, 1, 0.2, 0.4) is base(AI_Mage) (Q-008); [CorpseName] is the CorpseName override; Race.RandomSkinHue()
// on a mobile whose race is the default is Race.Human's; SetWearable is the shard's extension (Misc/WearableExtensions);
// the TimerStateCallback is a lambda over the attacker, as pinned DemonKnight's. The pumpkin's ServUO timer priority
// (FiftyMS) has no pinned counterpart (pinned timers have no priority) and is dropped, as pinned UnholyBone drops it.
//
// cc-P624 (stage 1 of shard-migration notes/cc-P564-design-pumpkin-king.md), a deliberate repair: ServUO's delayed
// throw puts pumpkins wherever the attacker stands a second later, and they hatch even after the event has ended. An
// event Jack (the spawner's boss) now remembers its spawner when it schedules the throw; the throw happens only while
// that spawner still runs, the event is enabled, Jack is still its living boss and the attacker is alive inside
// Sorcerer's Dungeon on Ilshenar, and only onto spots inside the dungeon. Its pumpkins and what they hatch belong to
// the spawner, which deletes them when the event goes down. A Jack staff added by hand belongs to no spawner and
// throws as in ServUO. The numbers (5%, 1 second, 1 to 3, 5 to 10 seconds, the twelve undead, 30% carving) are as
// before.

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

[SerializationGenerator(0, false)]
public partial class JackThePumpkinKing : BaseCreature
{
    private bool _inHere;

    [Constructible]
    public JackThePumpkinKing() : base(AIType.AI_Mage)
    {
        Name = "jack";
        Title = "the pumpkin king";
        Body = 0x190;
        Hue = Race.RandomSkinHue();

        SetStr(500);
        SetDex(200);
        SetInt(1200);

        SetHits(26000);

        SetDamage(22, 26);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 70, 80);
        SetResistance(ResistanceType.Fire, 70, 80);
        SetResistance(ResistanceType.Cold, 70, 80);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 40, 50);

        SetSkill(SkillName.Anatomy, 25);
        SetSkill(SkillName.MagicResist, 120);
        SetSkill(SkillName.Tactics, 100, 110);
        SetSkill(SkillName.Wrestling, 130, 140);
        SetSkill(SkillName.Parry, 20, 30);

        SetSkill(SkillName.Magery, 120);
        SetSkill(SkillName.EvalInt, 120);
        SetSkill(SkillName.Necromancy, 120);
        SetSkill(SkillName.SpiritSpeak, 120);
        SetSkill(SkillName.Meditation, 120);
        SetSkill(SkillName.Focus, 70, 80);

        Fame = 12000;
        Karma = -12000;

        this.SetWearable(new ClothNinjaHood(), 1281);
        this.SetWearable(new BoneChest(), 1175);
        this.SetWearable(new BoneArms(), 1175);
        this.SetWearable(new BoneGloves(), 1175);
        this.SetWearable(new ThighBoots());
        this.SetWearable(new Scepter());
    }

    public override string CorpseName => "the corpse of jack the pumpkin king";
    public override bool AlwaysMurderer => true;
    public override Poison PoisonImmune => Poison.Deadly;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }

    public override void OnDamage(int amount, Mobile from, bool willKill)
    {
        if (from != null && from != this && !_inHere)
        {
            _inHere = true;
            AOS.Damage(from, this, Utility.RandomMinMax(8, 20), 100, 0, 0, 0, 0);

            MovingEffect(from, Utility.RandomMinMax(0xC6A, 0xC6C), 10, 0, false, false, 0, 0);
            PlaySound(0x491);

            if (0.05 > Utility.RandomDouble())
            {
                SchedulePumpkins(from);
            }

            _inHere = false;
        }
    }

    /// <summary>The 5% branch of OnDamage: pumpkins around <paramref name="from" /> a second later.</summary>
    public void SchedulePumpkins(Mobile from)
    {
        var owner = TOSDSpawner.Instance is { } spawner && spawner.Owns(this) ? spawner : null;

        Timer.StartTimer(TimeSpan.FromSeconds(1.0), () => CreatePumpkin_Callback(from, owner));
    }

    public virtual void CreatePumpkin_Callback(Mobile from, TOSDSpawner owner)
    {
        if (owner != null && !owner.MayThrowPumpkins(this, from))
        {
            return;
        }

        var map = from.Map;

        if (map == null)
        {
            return;
        }

        var count = Utility.RandomMinMax(1, 3);

        for (var i = 0; i < count; ++i)
        {
            var x = from.X + Utility.RandomMinMax(-1, 1);
            var y = from.Y + Utility.RandomMinMax(-1, 1);
            var z = from.Z;

            if (!map.CanFit(x, y, z, 16, false, true))
            {
                z = map.GetAverageZ(x, y);

                if (z == from.Z || !map.CanFit(x, y, z, 16, false, true))
                {
                    continue;
                }
            }

            var at = new Point3D(x, y, z);

            if (owner != null && !TOSDSpawner.MayPlacePumpkin(at, map))
            {
                continue;
            }

            var pumpkin = new UnholyPumpkin
            {
                Hue = 0,
                Name = "unholy pumpkin",
                ItemID = Utility.RandomMinMax(0xC6A, 0xC6C)
            };

            pumpkin.MoveToWorld(at, map);
            owner?.AddPumpkin(pumpkin);
        }
    }
}

[SerializationGenerator(0, false)]
public partial class UnholyPumpkin : Item, ICarvable
{
    private SpawnTimer _timer;

    [Constructible]
    public UnholyPumpkin() : base(0xF7E)
    {
        Movable = false;

        _timer = new SpawnTimer(this);
        _timer.Start();
    }

    public override string DefaultName => "unholy pumpkin";

    public void Carve(Mobile from, Item item)
    {
        Effects.PlaySound(GetWorldLocation(), Map, 0x48F);
        Effects.SendLocationEffect(GetWorldLocation(), Map, 0x3728, 10, 10);

        if (0.3 > Utility.RandomDouble())
        {
            from.SendMessage("You destroy the pumpkin.");
            var gold = new Gold(25, 100);

            gold.MoveToWorld(GetWorldLocation(), Map);

            Delete();
            _timer.Stop();
        }
        else
        {
            from.SendMessage("You damage the pumpkin.");
        }
    }

    [AfterDeserialization]
    private void AfterDeserialize()
    {
        _timer = new SpawnTimer(this);
        _timer.Start();
    }

    private class SpawnTimer : Timer
    {
        private readonly Item _item;

        public SpawnTimer(Item item) : base(TimeSpan.FromSeconds(Utility.RandomMinMax(5, 10))) => _item = item;

        protected override void OnTick()
        {
            if (_item.Deleted)
            {
                return;
            }

            Mobile spawn = Utility.Random(12) switch
            {
                1  => new Zombie(),
                2  => new Wraith(),
                3  => new Spectre(),
                4  => new Ghoul(),
                5  => new Mummy(),
                6  => new Bogle(),
                7  => new RottingCorpse(),
                8  => new BoneKnight(),
                9  => new SkeletalKnight(),
                10 => new Lich(),
                11 => new LichLord(),
                _  => new Skeleton()
            };

            spawn.MoveToWorld(_item.Location, _item.Map);

            if (_item is UnholyPumpkin pumpkin && spawn is BaseCreature hatched)
            {
                TOSDSpawner.Instance?.OnPumpkinHatched(pumpkin, hatched);
            }

            _item.Delete();
        }
    }
}
