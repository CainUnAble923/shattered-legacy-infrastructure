// cc-P65, F-35: evo pets, the creature. Notes: shard-migration notes/cc-P65-evo-pets-framework.md.
//
// One class for every line: EvoPet reads its line and stage from EvoPetData and sets its body, sounds, stats and
// skills from them. A plain pet (not a companion): 1 follower slot at every stage, bonded and resurrected as stock.
//
// XP comes only from fights it deals damage in: when a creature dies, each evo pet in its damage list earns XP for its
// share of the damage (EvoPet.OnCreatureDeath). When its XP reaches the next enabled stage and the stage's gates are
// met, it evolves. A stage with several bodies waits for the owner to pick one (once). The owner picks a hue at
// stage 1; it carries through every stage.
//
// Its line, stage, XP, hue and picks live on the pet, never on the owner, so nothing an owner's loop reset does to the
// owner can touch them (F-35: keeps its stage through a loop reset). F-12 is not built: the owner's loop count comes
// from OwnerLoopsSource, which reads 0 until F-12 sets it; staff can stand in with TestOwnerLoops.

using System;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Gumps;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class EvoPet : BaseCreature
    {
        public const int EchoHue = 0x3B2;

        /// <summary>How many times an owner has looped. F-12 replaces this; until then every owner has looped 0 times.</summary>
        public static Func<Mobile, int> OwnerLoopsSource = _ => 0;

        [SerializableField(0, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private string _lineKey;

        [SerializableField(1, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _stage;

        [SerializableField(2, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _xp;

        [SerializableField(3, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private bool _hueChosen;

        // The body picked at each stage that offers a pick, indexed by stage number; 0 = not picked.
        [SerializableField(4, setter: "private")]
        private int[] _picks;

        // F-35's Bond Trial is not designed yet: a flag staff set by hand ([EvoPet bond on).
        [SerializableField(5)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private bool _bondTrialDone;

        // Staff testing only: stands in for the owner's loop count while F-12 is not built. -1 = not used.
        [SerializableField(6)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _testOwnerLoops = -1;

        // The stage the owner was last told about (a blocked gate or a pick), so it is said once, not on every kill.
        private int _toldAbout;

        [Constructible]
        public EvoPet() : this("dog")
        {
        }

        [Constructible]
        public EvoPet(string line) : base(AIType.AI_Melee, FightMode.Aggressor)
        {
            var evoLine = EvoPetData.Line(line) ??
                          throw new ArgumentException($"No evo line '{line}'. Lines: dog, cat, fox, bird, horse, lizard.", nameof(line));

            _lineKey = evoLine.Key;
            _stage = 1;
            _picks = new int[evoLine.Stages.Length + 1];

            ControlSlots = 1;   // every stage (F-35); never changed while controlled
            Tamable = false;    // it comes from the tutorial, not the wild; a released one cannot be taken by another tamer
            MinTameSkill = 0;
            Fame = 0;
            Karma = 300;

            ApplyStage();
        }

        public EvoLine Line => EvoPetData.Line(_lineKey);

        public EvoStage CurrentStage => Line?.Stage(_stage);

        [CommandProperty(AccessLevel.GameMaster)]
        public int OwnerLoops => Math.Max(ControlMaster == null ? 0 : OwnerLoopsSource(ControlMaster), _testOwnerLoops);

        /// <summary>Where it stands on its road, in words, for [props and the staff command.</summary>
        [CommandProperty(AccessLevel.GameMaster)]
        public string EvoStatus
        {
            get
            {
                var next = Line?.NextEnabled(_stage);

                if (next == null)
                {
                    return $"stage {_stage}, {EvoPetData.TopForNowLine}";
                }

                var need = EvoPetSettings.XpToEnter(next.Number);
                return BlockFor(next) is { } block
                    ? $"stage {_stage}, {_xp}/{need} XP to stage {next.Number}; waiting: {block}"
                    : $"stage {_stage}, {_xp}/{need} XP to stage {next.Number}";
            }
        }

        public override string DefaultName
        {
            get
            {
                var name = CurrentStage?.Name ?? "evo pet";
                return ("aeiou".IndexOf(name[0]) >= 0 ? "an " : "a ") + name;
            }
        }

        // --- body, stats and look -----------------------------------------------------------------------------------

        public int BodyFor(int stageNumber)
        {
            var stage = Line?.Stage(stageNumber);

            if (stage == null)
            {
                return 217;
            }

            if (!stage.HasPick)
            {
                return stage.Bodies[0];
            }

            var picked = _picks != null && stageNumber < _picks.Length ? _picks[stageNumber] : 0;
            return Array.IndexOf(stage.Bodies, picked) >= 0 ? picked : stage.Bodies[0];
        }

        public int PickAt(int stageNumber) =>
            _picks != null && stageNumber < _picks.Length ? _picks[stageNumber] : 0;

        /// <summary>Sets body and stats from the current stage. Skills only ever rise: what it learned by use stays.</summary>
        private void ApplyStage()
        {
            Body = BodyFor(_stage);

            var s = EvoPetData.StatsFor(_stage);

            SetStr(s.Str);
            SetDex(s.Dex);
            SetInt(s.Int);
            SetHits(s.Hits); // also heals it fully
            SetStam(s.Dex);
            SetMana(s.Int);
            SetDamage(s.DamageMin, s.DamageMax);

            SetDamageType(ResistanceType.Physical, 100);

            SetResistance(ResistanceType.Physical, s.Physical);
            SetResistance(ResistanceType.Fire, s.Fire);
            SetResistance(ResistanceType.Cold, s.Cold);
            SetResistance(ResistanceType.Poison, s.Poison);
            SetResistance(ResistanceType.Energy, s.Energy);

            RaiseSkill(SkillName.Wrestling, s.Wrestling);
            RaiseSkill(SkillName.Tactics, s.Tactics);
            RaiseSkill(SkillName.MagicResist, s.MagicResist);
            RaiseSkill(SkillName.Anatomy, s.Anatomy);

            VirtualArmor = s.VirtualArmor;
            InvalidateProperties();
        }

        private void RaiseSkill(SkillName skill, double value)
        {
            if (Skills[skill].Base < value)
            {
                SetSkill(skill, value);
            }
        }

        public override int GetIdleSound() => CurrentStage?.Sounds.Idle ?? base.GetIdleSound();
        public override int GetAngerSound() => CurrentStage?.Sounds.Anger ?? base.GetAngerSound();
        public override int GetAttackSound() => CurrentStage?.Sounds.Attack ?? base.GetAttackSound();
        public override int GetHurtSound() => CurrentStage?.Sounds.Hurt ?? base.GetHurtSound();
        public override int GetDeathSound() => CurrentStage?.Sounds.Death ?? base.GetDeathSound();

        // The UOP-era bodies number their actions in the monster layout, so pinned's animal actions show the wrong
        // thing on them (hit plays an attack, feeding plays a death, and 1309 vanishes): remapped here, before the
        // packet goes. Notes section 5.
        public override void Animate(int action, int frameCount, int repeatCount, bool forward, bool repeat, int delay)
        {
            if (EvoAnimations.IsUopBody(Body))
            {
                action = EvoAnimations.Remap(action);

                if (action < 0)
                {
                    return;
                }
            }

            base.Animate(action, frameCount, repeatCount, forward, repeat, delay);
        }

        // --- XP and step-ups ----------------------------------------------------------------------------------------

        /// <summary>The XP a kill is worth to a pet that dealt the given share of its damage (placeholder until F-12).</summary>
        public static int XpForKill(Mobile victim, double share)
        {
            if (victim == null || share <= 0)
            {
                return 0;
            }

            var xp = Math.Min(share, 1.0) * (20 + Math.Max(0, victim.Fame) / 25.0) * EvoPetSettings.XpRatePercent / 100.0;
            return Math.Max(1, (int)Math.Round(xp));
        }

        // A creature's normal death deletes it (pinned Mobile.cs:4899-4902), and the delete releases its master
        // (BaseCreature.cs:3688) before the death event fires (:3669). The delete event fires first, while it is still
        // controlled (:3673), so that is where "it was someone's pet" is noted. Weak: nothing is kept alive by it.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BaseCreature, object> _controlledWhenDeleted = new();

        [OnEvent(nameof(CreatureEvents.CreatureDeletedEvent))]
        public static void OnCreatureDeleted(BaseCreature bc)
        {
            if (bc is { Controlled: true } or { Summoned: true })
            {
                _controlledWhenDeleted.AddOrUpdate(bc, null);
            }
        }

        // Every creature's death: each controlled evo pet that damaged it earns its share. Pets (anyone's, bonded or
        // not) and summons earn nothing for anyone.
        [OnEvent(nameof(CreatureEvents.CreatureDeathEvent))]
        public static void OnCreatureDeath(BaseCreature victim)
        {
            if (victim == null || victim.Controlled || victim.Summoned || victim.IsBonded ||
                _controlledWhenDeleted.TryGetValue(victim, out _))
            {
                return;
            }

            var hitsMax = Math.Max(1, victim.HitsMax);

            foreach (var entry in victim.DamageEntries.ToArray())
            {
                if (entry.Damager is EvoPet { Deleted: false, Controlled: true, ControlMaster: not null } pet)
                {
                    // cc-P82: Bond on the master's gear adds its percent.
                    pet.AwardXp(ClusterFImbueEffects.BondXp(pet, XpForKill(victim, (double)entry.DamageGiven / hitsMax)));
                }
            }
        }

        public void AwardXp(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            Xp = (int)Math.Min(int.MaxValue, (long)_xp + amount);
            TryEvolve();
        }

        /// <summary>What stops it entering a stage it has the XP for, or null. Gates are on the stage being entered.</summary>
        public string BlockFor(EvoStage next)
        {
            if (OwnerLoops < next.MinOwnerLoops)
            {
                return $"owner at loop {next.MinOwnerLoops}";
            }

            if (next.BondTrial && !_bondTrialDone)
            {
                return "the Bond Trial";
            }

            if (next.HasPick && PickAt(next.Number) == 0)
            {
                return "the owner's pick";
            }

            return null;
        }

        /// <summary>Steps up as far as XP and gates allow. Returns the stages gained.</summary>
        public int TryEvolve()
        {
            var gained = 0;

            while (!Deleted && Line?.NextEnabled(_stage) is { } next && _xp >= EvoPetSettings.XpToEnter(next.Number))
            {
                var block = BlockFor(next);

                if (block != null)
                {
                    TellBlocked(next);
                    break;
                }

                EvolveTo(next.Number);
                gained++;
            }

            return gained;
        }

        private void TellBlocked(EvoStage next)
        {
            var owner = ControlMaster;

            if (owner == null || _toldAbout == next.Number)
            {
                return;
            }

            _toldAbout = next.Number;

            if (OwnerLoops < next.MinOwnerLoops)
            {
                var n = next.MinOwnerLoops;
                owner.SendMessage(EchoHue, string.Format(EvoPetData.NeedsLoopLine, n, n == 1 ? "" : "s"));
            }
            else if (next.BondTrial && !_bondTrialDone)
            {
                owner.SendMessage(EchoHue, EvoPetData.NeedsBondTrialLine);
            }
            else if (next.HasPick)
            {
                owner.SendMessage(EchoHue, EvoPetData.PickLine);
                owner.SendGump(new EvoPickGump(this, next.Number));
            }
        }

        /// <summary>The owner chooses the body for a stage that offers a pick. Once: a pick is never changed after.</summary>
        public bool ChoosePick(Mobile from, int stageNumber, int body)
        {
            var stage = Line?.Stage(stageNumber);

            if (stage is not { HasPick: true } || from != ControlMaster || PickAt(stageNumber) != 0 ||
                Array.IndexOf(stage.Bodies, body) < 0)
            {
                return false;
            }

            _picks[stageNumber] = body;
            this.MarkDirty();
            TryEvolve();
            return true;
        }

        private void EvolveTo(int stageNumber)
        {
            Stage = stageNumber;
            _toldAbout = 0;
            ApplyStage();

            FixedParticles(0x375A, 9, 20, 5027, EffectLayer.Waist); // pinned ArchProtection.cs:100-101
            PlaySound(0x1F7);

            var line = stageNumber < EvoPetData.StepUpLines.Length ? EvoPetData.StepUpLines[stageNumber] : "";
            if (line.Length > 0)
            {
                ControlMaster?.SendMessage(EchoHue, line);
            }
        }

        // --- staff ---------------------------------------------------------------------------------------------------

        /// <summary>Staff: puts it at a stage, ignoring XP and gates (its XP is raised to the stage's). A stage with a pick
        /// not yet made takes the given body, or the stage's first.</summary>
        public bool StaffSetStage(int stageNumber, int body = 0)
        {
            var stage = Line?.Stage(stageNumber);

            if (stage == null || stage.ArtPending)
            {
                return false;
            }

            if (stage.HasPick)
            {
                _picks[stageNumber] = Array.IndexOf(stage.Bodies, body) >= 0 ? body : PickAt(stageNumber) != 0 ? PickAt(stageNumber) : stage.Bodies[0];
            }

            Xp = Math.Max(_xp, EvoPetSettings.XpToEnter(stageNumber));
            Stage = stageNumber;
            _toldAbout = 0;
            ApplyStage();
            return true;
        }

        /// <summary>Staff: adds (or, negative, removes) XP and steps up as far as gates allow. Returns stages gained.</summary>
        public int StaffAddXp(int amount)
        {
            Xp = (int)Math.Clamp((long)_xp + amount, 0, int.MaxValue);
            _toldAbout = 0;
            return TryEvolve();
        }

        /// <summary>Staff: the pick for a stage, changed or set (players cannot change theirs).</summary>
        public bool StaffSetPick(int stageNumber, int body)
        {
            var stage = Line?.Stage(stageNumber);

            if (stage is not { HasPick: true } || Array.IndexOf(stage.Bodies, body) < 0)
            {
                return false;
            }

            _picks[stageNumber] = body;
            this.MarkDirty();

            if (_stage == stageNumber)
            {
                Body = body;
            }

            return true;
        }

        // --- hue ------------------------------------------------------------------------------------------------------

        public bool ChooseHue(Mobile from, int hue)
        {
            if (_hueChosen || from != ControlMaster || Array.FindIndex(EvoPetData.Hues, h => h.Hue == hue) < 0)
            {
                return false;
            }

            Hue = hue;
            HueChosen = true;
            return true;
        }

        public override void OnDoubleClick(Mobile from)
        {
            // The owner who closed the hue picker without choosing gets it back here.
            if (from == ControlMaster && !_hueChosen && !IsDeadBondedPet)
            {
                from.SendGump(new EvoHueGump(this));
                return;
            }

            // So does an owner who closed the pick: it waits for them.
            if (from == ControlMaster && !IsDeadBondedPet && Line?.NextEnabled(_stage) is { HasPick: true } next &&
                PickAt(next.Number) == 0 && _xp >= EvoPetSettings.XpToEnter(next.Number) && BlockFor(next) == "the owner's pick")
            {
                from.SendGump(new EvoPickGump(this, next.Number));
                return;
            }

            base.OnDoubleClick(from);
        }

        // --- getting one ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Gives a player an evo pet of a line at stage 1, tamed and bonded to them, at their feet, and opens the hue
        /// picker. The test path ([EvoPet give) and, later, the tutorial's Farmhand call this. Null if the line is unknown
        /// or the player has no free follower slot (pinned's own message says so).
        /// </summary>
        public static EvoPet GivePet(Mobile to, string line)
        {
            if (to?.Map == null || EvoPetData.Line(line) == null)
            {
                return null;
            }

            var pet = new EvoPet(line);

            // The taming success path's own steps (pinned AnimalTaming.cs:440-446), bonded at once (F-35: arrives bonded).
            if (!pet.SetControlMaster(to))
            {
                pet.Delete();
                return null;
            }

            pet.Owners.Add(to);
            pet.IsBonded = true;
            pet.ControlTarget = to;
            pet.ControlOrder = OrderType.Follow;
            pet.MoveToWorld(to.Location, to.Map);

            to.SendGump(new EvoHueGump(pet));
            return pet;
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            var line = Line;

            if (line == null)
            {
                return; // a line removed from the data: leave it as saved for staff to see
            }

            if (_picks == null || _picks.Length < line.Stages.Length + 1)
            {
                var picks = new int[line.Stages.Length + 1];
                _picks?.CopyTo(picks, 0);
                _picks = picks;
            }
        }
    }

    /// <summary>
    /// The UOP-era bodies (mobtypes.txt flag 0x10000, frames in AnimationFrame*.uop) number their actions in the monster
    /// layout: 4 and 5 attack, 10 get hit, no eat or fidget the server can reach. Pinned sends animal actions (swing
    /// 5/6, hurt 7, feed 3, idle 3/9/10, anger 10); TazUO plays the action number as the group (notes section 5).
    /// </summary>
    public static class EvoAnimations
    {
        public static readonly int[] UopBodies =
            { 739, 832, 1254, 1309, 1410, 1416, 1423, 1426, 1546, 1547, 1548, 1549, 1551, 1552, 1573 };

        public static bool IsUopBody(int body) => Array.IndexOf(UopBodies, body) >= 0;

        /// <summary>The action to send instead, or -1 to send nothing.</summary>
        public static int Remap(int action) =>
            action switch
            {
                5 or 6 => Utility.Random(4, 2), // attack: 4 is an attack on every one of them, 5 on most (others map it to 4)
                7      => 10,                   // get hit
                12     => 4,                    // a breath: 1309 has no group 12
                3 or 9 or 10 => -1,             // feed, idle, anger: no group reachable; the client plays its own idles
                _      => action
            };
    }
}
