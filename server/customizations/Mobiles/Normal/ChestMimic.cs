// cc-P50, F-32: the chest mimic as a monster. Notes: shard-migration notes/cc-P50-chest-mimic-monster.md.
//
// A dungeon chest that comes alive and fights where it stood. Its look is Archaaz's "Animated Mimic" (servuo.dev
// thread 12406; no license stated), drawn as item art: frame 1 is, pixel for pixel, the stock wooden chest (0xE43,
// 0xE42 in the other facing), and the rest ship as our PNGs through the player package (registry.csv, IDs
// 0x3C70-0x3C81). No creature body draws it:
//
//   - ChestMimic is the creature: a normal BaseCreature on mage AI whose body (239) has no frames in the client's
//     animation files, so it draws nothing. Combat, spells, damage, notoriety and the corpse are all stock.
//   - ChestMimicShell is an item on the same tile that players see and click. The server swaps its ItemID to play
//     each state (waking, idle loop, bite, hurt, lid shut). A target aimed at it lands on the creature
//     (ITargetRedirect, IncomingTargetingPackets-target-redirect.patch); a war-mode double-click attacks it.
//
// It never moves (DisallowAllMoves, as stock's Reaper and Corpser; CanFlee false; OnMove refused). An attacker it
// cannot reach (out of its range or line of sight) makes it shut its lid: it takes no damage and heals until someone
// is in reach again. Stats by chest level 1 to 6 are data (Levels); level 4 is OSI's Mimic (ServUO
// Scripts/Mobiles/Normal/Mimic.cs). Our class is not OSI's Mimic, which may be ported later under its own name.
//
// Out of scope here, for the F-32 build: springing from dungeon chests and the odds, the tame (it is not tameable),
// the PetMimic hand-off, loot of our own (OSI's Mimic's loot stands in), spawn placement.

using System;
using ModernUO.Serialization;
using Server.Commands;
using Server.Items;
using Server.Targeting;

namespace Server
{
    /// <summary>
    /// An item that stands in for a mobile players cannot click (cc-P50). A target aimed at the item is handed to
    /// the object this returns, or kept on the item when it returns null. Called from the target packet handler
    /// (server\patches\IncomingTargetingPackets-target-redirect.patch).
    /// </summary>
    public interface ITargetRedirect
    {
        object RedirectTarget(Mobile from, Target target);
    }

    /// <summary>
    /// The chest a mimic sprang from, when it wants to know the mimic is gone (F-32's dungeon chest). A chest that is
    /// not one is consumed when its mimic dies (its contents go into the corpse) and put back when staff delete it.
    /// </summary>
    public interface IMimicSource
    {
        void OnMimicGone(Mobiles.ChestMimic mimic, bool killed);
    }

    /// <summary>
    /// The chest mimic's art IDs and our tiledata for them (cc-P50). The values are records.json's: change the two
    /// together (player-package\tests\Build-UoOverrides.Tests.ps1 compares them). ShatteredLegacyArt.Apply writes
    /// them into the server's in-memory TileData.ItemTable, so the mimic blocks on the server as on the client.
    /// </summary>
    public static class ChestMimicArt
    {
        public sealed record Record(int Id, string Name, TileFlag Flags, int Weight, int Height);

        // The 18 IDs registered for the mimic (player-package\vendor\shattered-legacy-art\registry.csv).
        public const int FirstId = 0x3C70;
        public const int LastId = 0x3C81;

        // Nine IDs per facing. NorthSouth is the stock wooden chest 0xE43's facing, WestEast 0xE42's.
        public const int NorthSouthBase = 0x3C70;
        public const int WestEastBase = 0x3C79;

        // Offsets in each facing's block.
        public const int Teeth = 0;     // Archaaz frame 2
        public const int Open = 1;      // frame 3
        public const int Tongue1 = 2;   // frames 4 to 6
        public const int Tongue2 = 3;
        public const int Tongue3 = 4;
        public const int IdleLoop = 5;  // animated: frames 2 and 3 (its second frame, +6, is never an item's ID)
        public const int DeadEmpty = 7; // Extra 6 (NS), 5 (WE)
        public const int DeadTongue = 8; // Extra 8 (NS), 7 (WE)

        // Frame 1, the closed chest, is the stock art itself.
        public const int ClosedNorthSouth = 0xE43;
        public const int ClosedWestEast = 0xE42;

        public static readonly Record[] Records =
        {
            // OSI's wooden chest 0xE43 is 0x00204040 (Container | ArticleA | Impassable), height 4, weight 255. The
            // mimic is no container, so ours drop Container; the idle loop adds Animation; the dead tiles block nothing.
            new(0x3C70, "mimic", TileFlag.ArticleA | TileFlag.Impassable, 255, 4),
            new(0x3C71, "mimic", TileFlag.ArticleA | TileFlag.Impassable, 255, 4),
            new(0x3C72, "mimic", TileFlag.ArticleA | TileFlag.Impassable, 255, 4),
            new(0x3C73, "mimic", TileFlag.ArticleA | TileFlag.Impassable, 255, 4),
            new(0x3C74, "mimic", TileFlag.ArticleA | TileFlag.Impassable, 255, 4),
            new(0x3C75, "mimic", TileFlag.Animation | TileFlag.ArticleA | TileFlag.Impassable, 255, 4),
            new(0x3C77, "dead mimic", TileFlag.ArticleA, 255, 4),
            new(0x3C78, "dead mimic", TileFlag.ArticleA, 255, 4),
            new(0x3C79, "mimic", TileFlag.ArticleA | TileFlag.Impassable, 255, 4),
            new(0x3C7A, "mimic", TileFlag.ArticleA | TileFlag.Impassable, 255, 4),
            new(0x3C7B, "mimic", TileFlag.ArticleA | TileFlag.Impassable, 255, 4),
            new(0x3C7C, "mimic", TileFlag.ArticleA | TileFlag.Impassable, 255, 4),
            new(0x3C7D, "mimic", TileFlag.ArticleA | TileFlag.Impassable, 255, 4),
            new(0x3C7E, "mimic", TileFlag.Animation | TileFlag.ArticleA | TileFlag.Impassable, 255, 4),
            new(0x3C80, "dead mimic", TileFlag.ArticleA, 255, 4),
            new(0x3C81, "dead mimic", TileFlag.ArticleA, 255, 4)
        };

        public static void Apply()
        {
            foreach (var r in Records)
            {
                TileData.ItemTable[r.Id] = new ItemData(r.Name, r.Flags, r.Weight, 0, 0, 0, 0, r.Height);
            }
        }

        public static int Id(Mobiles.MimicFacing facing, int offset) =>
            (facing == Mobiles.MimicFacing.WestEast ? WestEastBase : NorthSouthBase) + offset;

        public static int Closed(Mobiles.MimicFacing facing) =>
            facing == Mobiles.MimicFacing.WestEast ? ClosedWestEast : ClosedNorthSouth;

        // Stock chests and crates by facing: pinned Container.cs [Flippable(a, b)], where a faces as 0xE43 does and b as
        // 0xE42 (checked on a render of all eight pairs, 2026-10-12). Anything else is taken as NorthSouth.
        private static readonly int[] WestEastIds = { 0xE42, 0xE7D, 0xE7E, 0xE3E, 0xE3C, 0xE80, 0xE7C, 0xE40 };

        public static Mobiles.MimicFacing FacingOf(int itemId) =>
            Array.IndexOf(WestEastIds, itemId) >= 0 ? Mobiles.MimicFacing.WestEast : Mobiles.MimicFacing.NorthSouth;
    }
}

namespace Server.Mobiles
{
    public enum MimicFacing
    {
        NorthSouth,
        WestEast
    }

    public enum MimicState
    {
        Resting,
        Waking,
        Idle,
        Bite,
        Hurt,
        LidShut,
        Dead
    }

    /// <summary>One chest level's stats (cc-P50 Part A 5). Damage is split evenly over the five types at every level.</summary>
    public sealed record ChestMimicLevel(
        int Level,
        int HitsMin, int HitsMax,
        int Str, int Dex, int Int,
        int DamageMin, int DamageMax,
        int Physical, int Fire, int Cold, int Poison, int Energy,
        double EvalInt, double Magery, double Meditation, double MagicResist, double Tactics, double Wrestling,
        int Fame, int VirtualArmor
    );

    [SerializationGenerator(0, false)]
    public partial class ChestMimic : BaseCreature
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 6;
        public const int DefaultLevel = 4;

        // No frames for this body in any of the client's animation files (anim*.mul, AnimationFrame*.uop), and it is
        // named in none of Body.def, Bodyconv.def or mobtypes.txt (notes section 3.1). The client draws nothing.
        public const int EmptyBody = 239;

        // Its reach: as far as it perceives (OSI's Mimic: 10), in line of sight.
        public const int ReachRange = 10;

        public static readonly ChestMimicLevel[] Levels =
        {
            //   lvl  hits        str  dex  int  dmg     resists P/F/C/Po/E   EvalInt Magery Med  Resist Tact  Wrest  fame   VA
            new(1,   90,  110,  110,  75, 120,  5,  9, 30, 20, 15, 15, 20,  50.0,  55.0,  50.0,  60.0,  50.0,  50.0,  3000, 30),
            new(2,  180,  220,  160,  85, 160,  8, 12, 40, 28, 22, 23, 28,  70.0,  72.0,  70.0,  85.0,  70.0,  65.0,  6000, 36),
            new(3,  330,  370,  220, 110, 210, 10, 16, 52, 35, 29, 30, 35,  90.0,  90.0,  85.0, 105.0,  85.0,  80.0, 10000, 44),
            new(4,  543,  543,  281, 140, 261, 13, 20, 63, 43, 36, 37, 42, 100.0, 107.5, 100.0, 126.5,  98.5,  92.2, 15000, 50),
            new(5,  750,  800,  330, 150, 400, 16, 23, 68, 50, 45, 45, 50, 110.0, 115.0, 105.0, 145.0, 105.0, 100.0, 19000, 55),
            new(6, 1000, 1100,  400, 160, 550, 19, 27, 72, 57, 52, 52, 57, 120.0, 120.0, 110.0, 165.0, 110.0, 110.0, 23000, 60)
        };

        // Frame timings. One-shots are ItemID swaps on a timer: animdata only loops, in step for every item of an ID.
        public static readonly TimeSpan WakeStep = TimeSpan.FromMilliseconds(250);
        public static readonly TimeSpan BiteStep = TimeSpan.FromMilliseconds(150);
        public static readonly TimeSpan HurtStep = TimeSpan.FromMilliseconds(120);
        public static readonly TimeSpan RegenInterval = TimeSpan.FromSeconds(2);

        // Stock sounds: OSI's Mimic's set (ServUO Mimic.cs:65-83) and a wooden door's for the lid (pinned Doors.cs:150-151).
        public const int IdleSound = 1561;
        public const int AngerSound = 1558;
        public const int HurtSound = 1560;
        public const int DeathSound = 1559;
        public const int LidOpenSound = 0xEA;
        public const int LidShutSound = 0xF1;

        public static ChestMimicLevel LevelRow(int level) =>
            level is >= MinLevel and <= MaxLevel
                ? Levels[level - 1]
                : throw new ArgumentOutOfRangeException(nameof(level), level, "A chest mimic's level is 1 to 6.");

        [SerializableField(0, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _chestLevel;

        [SerializableField(1, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private MimicFacing _lookFacing;

        [SerializableField(2, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private ChestMimicShell _shell;

        // The chest it sprang from, kept on the internal map (P49 5.3: the chest is the record F-32 restores).
        [SerializableField(3, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private Item _sourceChest;

        [SerializableField(4, setter: "private")]
        private Map _homeMap;

        private MimicState _state = MimicState.Idle;
        private Timer _frames;
        private int _restingItemId;
        private int _restingHue;
        private bool _died;
        private long _nextRegen;

        [Constructible]
        public ChestMimic() : this(DefaultLevel)
        {
        }

        [Constructible]
        public ChestMimic(int level) : this(level, MimicFacing.NorthSouth)
        {
        }

        public ChestMimic(int level, MimicFacing facing) : base(AIType.AI_Mage, FightMode.Closest, ReachRange)
        {
            var row = LevelRow(level);
            _chestLevel = level;
            _lookFacing = facing;

            Body = EmptyBody;

            SetStr(row.Str);
            SetDex(row.Dex);
            SetInt(row.Int);
            SetHits(row.HitsMin, row.HitsMax);
            SetDamage(row.DamageMin, row.DamageMax);

            SetDamageType(ResistanceType.Physical, 20);
            SetDamageType(ResistanceType.Fire, 20);
            SetDamageType(ResistanceType.Cold, 20);
            SetDamageType(ResistanceType.Poison, 20);
            SetDamageType(ResistanceType.Energy, 20);

            SetResistance(ResistanceType.Physical, row.Physical);
            SetResistance(ResistanceType.Fire, row.Fire);
            SetResistance(ResistanceType.Cold, row.Cold);
            SetResistance(ResistanceType.Poison, row.Poison);
            SetResistance(ResistanceType.Energy, row.Energy);

            SetSkill(SkillName.EvalInt, row.EvalInt);
            SetSkill(SkillName.Magery, row.Magery);
            SetSkill(SkillName.Meditation, row.Meditation);
            SetSkill(SkillName.MagicResist, row.MagicResist);
            SetSkill(SkillName.Tactics, row.Tactics);
            SetSkill(SkillName.Wrestling, row.Wrestling);

            Fame = row.Fame;
            Karma = -row.Fame;
            VirtualArmor = row.VirtualArmor;

            Tamable = false; // F-32 opens the tame window

            PackReg(20); // OSI's Mimic
        }

        public override string DefaultName => "a chest mimic";
        public override string CorpseName => "a dead chest mimic";

        public override bool DisallowAllMoves => true;
        public override bool CanFlee => false;

        [CommandProperty(AccessLevel.GameMaster)]
        public MimicState State => _state;

        [CommandProperty(AccessLevel.GameMaster)]
        public bool LidShut => _state == MimicState.LidShut;

        public bool Died => _died;

        // Stock creatures' fallback when the AI tries to step: refused, whoever asks.
        protected override bool OnMove(Direction d) => false;

        public override int GetIdleSound() => IdleSound;
        public override int GetAngerSound() => AngerSound;
        public override int GetHurtSound() => HurtSound;
        public override int GetDeathSound() => DeathSound;

        // OSI's Mimic's loot stands in until F-32 gives the mimic its own (ServUO Mimic.cs:53-62).
        public override void GenerateLoot()
        {
            AddLoot(LootPack.FilthyRich, 4);
            AddLoot(LootPack.MedScrolls);
        }

        public override void OnDeath(Container c)
        {
            _died = true;
            StopFrames();

            // A plain chest's contents go to whoever kills it; F-32's chest decides for itself.
            if (_sourceChest is { Deleted: false } chest and not IMimicSource)
            {
                foreach (var item in chest.Items.ToArray())
                {
                    c.DropItem(item);
                }
            }

            base.OnDeath(c);

            // OSI's Mimic (ServUO Mimic.cs:56-62).
            if (Utility.RandomDouble() < 0.03)
            {
                c.DropItem(new LuckyCoin());
            }

            // The stock corpse is 0x2006 drawn by its body, and this body draws nothing: show the dead mimic instead.
            // Swapped as Corpse.cs does for bones (remove, then resend), so clients drop the corpse graphic.
            c.ProcessDelta();
            c.SendRemovePacket();
            c.ItemID = ChestMimicArt.Id(_lookFacing, Utility.RandomBool() ? ChestMimicArt.DeadEmpty : ChestMimicArt.DeadTongue);
            c.Hue = 0;
            c.ProcessDelta();

            _state = MimicState.Dead;
            _shell?.Delete();
        }

        public override void OnDelete()
        {
            StopFrames();

            var chest = _sourceChest;
            if (chest is { Deleted: false })
            {
                if (chest is IMimicSource source)
                {
                    source.OnMimicGone(this, _died);
                }
                else if (_died)
                {
                    chest.Delete(); // emptied into the corpse
                }
                else if (_homeMap != null && _homeMap != Map.Internal)
                {
                    chest.MoveToWorld(Home, _homeMap); // staff deleted it: the chest comes back where it stood
                }
            }

            base.OnDelete();
        }

        public override void OnAfterDelete()
        {
            base.OnAfterDelete();
            StopFrames();
            _shell?.Delete();
        }

        // --- staying put, and its look following it if staff move it ------------------------------------------------

        protected override void OnLocationChange(Point3D oldLocation)
        {
            base.OnLocationChange(oldLocation);
            SyncShell();
        }

        protected override void OnMapChange(Map oldMap)
        {
            base.OnMapChange(oldMap);
            SyncShell();
        }

        private void SyncShell()
        {
            if (Deleted || Map == null || _died)
            {
                return;
            }

            if (_shell == null || _shell.Deleted)
            {
                // Not on the internal map: the base constructor puts a new mobile there through this same path,
                // before this class's constructor has set its level and facing.
                if (Map == Map.Internal)
                {
                    return;
                }

                var resting = _restingItemId != 0;
                _shell = new ChestMimicShell(this, resting ? _restingItemId : IdleId) { Hue = resting ? _restingHue : 0 };
                this.MarkDirty();

                if (!resting && Map != Map.Internal)
                {
                    _state = MimicState.Idle;
                }
            }

            if (_shell.Map != Map || _shell.Location != Location)
            {
                _shell.MoveToWorld(Location, Map);
            }

            if (Map != Map.Internal)
            {
                _homeMap = Map;
                Home = Location;
                RangeHome = 0;
            }
        }

        // Invisibility (MageAI casts it) would hide it from every target cursor and from its look: undo it at once.
        public override void OnHiddenChanged()
        {
            base.OnHiddenChanged();

            if (Hidden && Alive && !Deleted)
            {
                Timer.DelayCall(() =>
                    {
                        if (!Deleted && Alive)
                        {
                            Hidden = false;
                        }
                    }
                );
            }
        }

        // --- the states ---------------------------------------------------------------------------------------------

        public int IdleId => ChestMimicArt.Id(_lookFacing, ChestMimicArt.IdleLoop);

        /// <summary>The item IDs a one-shot state plays, in order, before the idle loop comes back.</summary>
        public int[] SequenceFor(MimicState state) =>
            state switch
            {
                MimicState.Waking => new[]
                {
                    ChestMimicArt.Id(_lookFacing, ChestMimicArt.Teeth),
                    ChestMimicArt.Id(_lookFacing, ChestMimicArt.Open)
                },
                MimicState.Bite => new[]
                {
                    ChestMimicArt.Id(_lookFacing, ChestMimicArt.Tongue1),
                    ChestMimicArt.Id(_lookFacing, ChestMimicArt.Tongue2),
                    ChestMimicArt.Id(_lookFacing, ChestMimicArt.Tongue3),
                    ChestMimicArt.Id(_lookFacing, ChestMimicArt.Tongue2),
                    ChestMimicArt.Id(_lookFacing, ChestMimicArt.Open)
                },
                // A shudder: the lid half shuts and opens again. The art is coloured, so a hue flash would repaint it all.
                MimicState.Hurt => new[]
                {
                    ChestMimicArt.Id(_lookFacing, ChestMimicArt.Teeth),
                    ChestMimicArt.Closed(_lookFacing),
                    ChestMimicArt.Id(_lookFacing, ChestMimicArt.Teeth)
                },
                _ => Array.Empty<int>()
            };

        /// <summary>The resting chest comes alive: lid up, teeth, mouth open, then the idle loop.</summary>
        public void Wake()
        {
            if (_shell != null)
            {
                _shell.Hue = 0;
            }

            _restingItemId = 0;
            Play(MimicState.Waking, WakeStep);
        }

        public void PlayBite()
        {
            if (_state == MimicState.Idle)
            {
                Play(MimicState.Bite, BiteStep);
            }
        }

        public void PlayHurt()
        {
            if (_state == MimicState.Idle)
            {
                Play(MimicState.Hurt, HurtStep);
            }
        }

        /// <summary>Ends whatever one-shot is playing and shows the idle loop. Staff and tests use it.</summary>
        public void SkipToIdle()
        {
            StopFrames();

            if (_state != MimicState.LidShut && _state != MimicState.Dead)
            {
                _state = MimicState.Idle;
                Show(IdleId);
            }
        }

        private void Play(MimicState state, TimeSpan step)
        {
            StopFrames();

            var frames = SequenceFor(state);
            _state = state;
            Show(frames[0]);

            var next = 1;
            _frames = Timer.DelayCall(step, step, frames.Length, () =>
                {
                    if (Deleted || _state != state)
                    {
                        StopFrames();
                        return;
                    }

                    if (next < frames.Length)
                    {
                        Show(frames[next++]);
                    }
                    else
                    {
                        _state = MimicState.Idle;
                        Show(IdleId);
                    }
                }
            );
        }

        private void StopFrames()
        {
            _frames?.Stop();
            _frames = null;
        }

        private void Show(int itemId)
        {
            if (_shell is { Deleted: false } && _shell.ItemID != itemId)
            {
                _shell.ItemID = itemId;
            }
        }

        public override void OnGaveMeleeAttack(Mobile defender, int damage)
        {
            base.OnGaveMeleeAttack(defender, damage);
            PlayBite();
        }

        public override void OnSpellCast(ISpell spell)
        {
            base.OnSpellCast(spell);
            PlayBite();
        }

        // --- out of reach: the lid shuts -----------------------------------------------------------------------------

        public bool CanReach(Mobile m) =>
            m is { Deleted: false, Alive: true } && m.Map == Map && Map != null && InRange(m, ReachRange) && InLOS(m);

        /// <summary>Anyone fighting it (its combatant or an aggressor of the last two minutes) that it can reach.</summary>
        public bool AnyAttackerInReach()
        {
            if (CanReach(Combatant))
            {
                return true;
            }

            foreach (var info in Aggressors)
            {
                if (CanReach(info.Attacker))
                {
                    return true;
                }
            }

            return false;
        }

        public override bool CanBeDamaged() => _state != MimicState.LidShut && base.CanBeDamaged();

        public override void OnDamage(int amount, Mobile from, bool willKill)
        {
            base.OnDamage(amount, from, willKill);

            if (willKill || !Alive)
            {
                return;
            }

            if (from != null && from != this && !CanReach(from) && !AnyAttackerInReach())
            {
                ShutLid();
            }
            else if (amount * 20 >= HitsMax)
            {
                PlayHurt(); // a hit of 5% or more
            }
        }

        public void ShutLid()
        {
            if (_state is MimicState.LidShut or MimicState.Dead)
            {
                return;
            }

            StopFrames();
            _state = MimicState.LidShut;
            Show(ChestMimicArt.Closed(_lookFacing));
            PlaySound(LidShutSound);
            PublicOverheadMessage(MessageType.Emote, 0x3B2, false, "*the lid snaps shut*");
            _nextRegen = Core.TickCount + (long)RegenInterval.TotalMilliseconds;
        }

        public void OpenLid()
        {
            if (_state != MimicState.LidShut)
            {
                return;
            }

            PlaySound(LidOpenSound);
            PublicOverheadMessage(MessageType.Emote, 0x3B2, false, "*the lid creaks open*");
            _state = MimicState.Idle; // so Wake's sequence may replace it
            Play(MimicState.Waking, WakeStep);
        }

        public override void OnThink()
        {
            base.OnThink();

            if (_state != MimicState.LidShut || !Alive)
            {
                return;
            }

            if (AnyAttackerInReach())
            {
                OpenLid();
            }
            else if (Core.TickCount - _nextRegen >= 0)
            {
                // Shut, it heals 5% of its hits every two seconds.
                Hits = Math.Min(HitsMax, Hits + Math.Max(1, HitsMax / 20));
                _nextRegen = Core.TickCount + (long)RegenInterval.TotalMilliseconds;
            }
        }

        // --- a chest becomes a mimic --------------------------------------------------------------------------------

        /// <summary>
        /// Turns a chest on the ground into a mimic of the given level on the same tile, facing the way the chest
        /// faced, in one step: the mimic and its look (the chest's own graphic and hue) arrive, the chest goes to the
        /// internal map, then the mimic wakes. Returns null if the chest is not on the ground of a real map.
        /// </summary>
        public static ChestMimic Spring(Item chest, int level)
        {
            if (chest is not { Deleted: false } || chest.Parent != null || chest.Map == null || chest.Map == Map.Internal)
            {
                return null;
            }

            var mimic = new ChestMimic(level, ChestMimicArt.FacingOf(chest.ItemID))
            {
                _sourceChest = chest,
                _restingItemId = chest.ItemID,
                _restingHue = chest.Hue
            };
            mimic._state = MimicState.Resting;

            mimic.MoveToWorld(chest.Location, chest.Map); // its look arrives showing the chest (SyncShell)
            chest.Internalize();
            mimic._shell?.ProcessDelta();

            mimic.Wake();
            return mimic;
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            _state = MimicState.Idle;

            Timer.DelayCall(() =>
                {
                    if (Deleted)
                    {
                        return;
                    }

                    SyncShell();
                    Show(IdleId);
                }
            );
        }

        // --- staff: [MimicChest -----------------------------------------------------------------------------------------

        public static void Configure()
        {
            CommandSystem.Register("MimicChest", AccessLevel.GameMaster, OnMimicChestCommand);
        }

        [Usage("MimicChest [level]")]
        [Description("Turns a targeted chest on the ground into a chest mimic of that level (1 to 6, default 4) on the same tile and facing (cc-P50 test tool).")]
        [ShardCommand(CommandCategory.DevTool, Summary = "Turns a targeted chest into a chest mimic on the spot.")]
        private static void OnMimicChestCommand(CommandEventArgs e)
        {
            var level = e.Length > 0 ? e.GetInt32(0) : DefaultLevel;

            if (level is < MinLevel or > MaxLevel)
            {
                e.Mobile.SendMessage("A chest mimic's level is 1 to 6.");
                return;
            }

            e.Mobile.SendMessage("Target a chest on the ground.");
            e.Mobile.Target = new MimicChestTarget(level);
        }

        private class MimicChestTarget : Target
        {
            private readonly int _level;

            public MimicChestTarget(int level) : base(12, false, TargetFlags.None) => _level = level;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (targeted is not Container chest || targeted is Corpse || chest.Parent != null)
                {
                    from.SendMessage("That is not a chest on the ground.");
                    return;
                }

                var mimic = Spring(chest, _level);
                from.SendMessage(mimic == null ? "That chest cannot become a mimic." : $"The chest comes alive (level {_level}).");
            }
        }
    }

    /// <summary>
    /// What players see of a chest mimic (cc-P50): an item on its tile whose ItemID shows the mimic's state. It sends
    /// clicks, targets and attacks on to the mimic. It belongs to the mimic: it goes when the mimic goes.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class ChestMimicShell : Item, ITargetRedirect
    {
        [SerializableField(0, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private ChestMimic _mimic;

        public ChestMimicShell(ChestMimic mimic, int itemId) : base(itemId)
        {
            _mimic = mimic;
            Movable = false;
        }

        public override bool Decays => false;

        public override string DefaultName => _mimic?.Name ?? "a chest mimic";

        private bool Live => _mimic is { Deleted: false, Alive: true };

        public object RedirectTarget(Mobile from, Target target) => Live ? _mimic : null;

        public override void OnSingleClick(Mobile from)
        {
            if (Live)
            {
                _mimic.OnSingleClick(from); // its name, in its notoriety colour
            }
            else
            {
                base.OnSingleClick(from);
            }
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!Live)
            {
                return;
            }

            // As a war-mode double-click on a creature does (the client sends that as an attack request, which reaches
            // Mobile.Attack); the combatant packet then opens the player's health bar for the mimic.
            if (from.Warmode)
            {
                from.Attack(_mimic);
            }
            else
            {
                from.SendMessage("That chest is alive!");
            }
        }

        public override void OnAfterDelete()
        {
            base.OnAfterDelete();

            if (_mimic is { Deleted: false, Died: false } && _mimic.Shell == this)
            {
                _mimic.Delete(); // staff deleting what they see delete the creature too
            }
        }

        // A look whose mimic is gone (a save between the two deletes) has nothing to show.
        [AfterDeserialization]
        private void AfterDeserialization()
        {
            if (_mimic is not { Deleted: false })
            {
                Timer.DelayCall(Delete);
            }
        }
    }
}
