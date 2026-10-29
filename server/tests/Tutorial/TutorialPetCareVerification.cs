// cc-P666 (P481 F6): pets on the isle, and every vet brings pets back. Notes: shard-migration
// notes/cc-P666-pet-protection-vet.md.
//
// Facts:
//   1. On the isle (a Fold map), a Young owner's pet ghost within 3 tiles of Tobin comes back at once: free, with every
//      skill, its body not waited for, and the necklace from its corpse back on it.
//   2. Off the isle, Tobin keeps today's rule: free while Young, but the body must fade first and the pet loses 0.2.
//   3. A stock vet (Veterinarian-resurrect-pets.patch) offers on the step in, freezes the owner while the offer is open,
//      and brings the pet back for ServUO's fee from the bank, with the 0.2 loss.
//   4. On the isle, an owner who is not Young pays ServUO's fee at Tobin and the pet loses 0.2.
//   5. Sera and Wren point a Young owner's pet ghost to Tobin, and finishing the farm says what to do when a pet dies.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialPetCareVerification
{
    private static readonly Point3D TownSpot = new(1740, 1740, 0);

    public TutorialPetCareVerification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
        ClusterFTutorial.Enabled = true;
        FoldMaps.Configure();
        TutorialCoreTrials.EnsureHooks();
        TutorialFarm.EnsureHooks();
    }

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p666{Guid.NewGuid():N}"[..16], "p666-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<IEntity> Litter = new();

        public Player(bool young, Map map, Point3D at, int bankGold = 0)
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Owner", RawStr = 60, RawDex = 60, RawInt = 60 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(at, map);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
            Pm.Young = young;
            if (bankGold > 0)
            {
                Banker.Deposit(Pm, bankGold);
            }
        }

        public T Add<T>(T e) where T : IEntity
        {
            Litter.Add(e);
            return e;
        }

        public void Dispose()
        {
            TutorialCoreTrials.Cancel(Pm);
            ClusterFFanfare.FlushAll();
            foreach (var e in Litter)
            {
                e.Delete();
            }

            foreach (var m in Pm.AllFollowers?.ToList() ?? new List<Mobile>())
            {
                m.Delete();
            }

            Pm.NetState = null;
            Ns.Mobile = null;
            Ns.Dispose();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static Point3D Off(Point3D p, int dx) => new(p.X + dx, p.Y, p.Z);

    private static RelayInfo Press(int button, params int[] switches) => new(button, switches, default, default, default);

    private static int Bank(PlayerMobile pm) => Banker.GetBalance(pm);

    private static int Mark(NetState ns) => ns.SendBuffer.GetReadSpan().Length;

    private static bool SentText(NetState ns, int from, string text)
    {
        var span = ns.SendBuffer.GetReadSpan()[from..];
        return span.IndexOf(Encoding.BigEndianUnicode.GetBytes(text)) >= 0 || span.IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;
    }

    private static int[] SkillBases(BaseCreature bc) => Enumerable.Range(0, bc.Skills.Length).Select(i => bc.Skills[i].BaseFixedPoint).ToArray();

    // Each skill after `Base -= 0.2`, as pinned's Skill.Base setter stores it (truncated to tenths, never below 0).
    private static int[] AfterLoss(int[] bases) => bases.Select(b => Math.Clamp((int)((b / 10.0 - 0.2) * 10.0), 0, 0xFFFF)).ToArray();

    // ServUO Veterinarian.GetResurrectionFee: 100 + 1.1041 ^ MinTameSkill, from 100 to 30,000 (the horse: 29.1).
    private static int ServUOFee(BaseCreature pet) => Math.Clamp((int)(100 + Math.Pow(1.1041, pet.MinTameSkill)), 100, 30000);

    /// <summary>The owner's bonded horse, wearing a necklace, killed at `at`. Its corpse is left unless `fade`.</summary>
    private static (Horse Pet, GoldNecklace Necklace) DeadHorse(Player p, Point3D at, bool fade)
    {
        var pet = p.Add(new Horse());
        pet.MoveToWorld(at, p.Pm.Map);
        pet.SetControlMaster(p.Pm);
        pet.IsBonded = true;
        var necklace = p.Add(new GoldNecklace());
        pet.AddItem(necklace);
        pet.Kill();
        Assert.True(pet.IsDeadBondedPet);
        if (pet.Corpse is { } corpse)
        {
            p.Litter.Add(corpse);
        }

        if (fade)
        {
            pet.Corpse?.Delete();
        }

        return (pet, necklace);
    }

    private static TutorialVeterinarian Tobin(Player p, Point3D at)
    {
        var vet = p.Add(new TutorialVeterinarian { Name = "Tobin" });
        vet.MoveToWorld(at, p.Pm.Map);
        return vet;
    }

    // ---------------------------------------------------------------- 1

    [Fact]
    public void OnTheIsleAYoungOwnersPetComesBackAtTobinFreeWholeAndWithItsThings()
    {
        var vetSpot = new Point3D(TutorialFarm.FarmVetSpot, 0);
        using var p = new Player(young: true, TutorialWorld.Isle, Off(vetSpot, -6), bankGold: 1000);
        var vet = Tobin(p, vetSpot);
        var (pet, necklace) = DeadHorse(p, Off(vetSpot, -8), fade: false);
        var corpse = pet.Corpse;
        Assert.NotNull(corpse);
        Assert.Equal(corpse, necklace.Parent);
        var skills = SkillBases(pet);

        // The ghost follows its owner up to Tobin.
        var from = pet.Location;
        pet.MoveToWorld(Off(vetSpot, -2), vet.Map);
        var before = Mark(p.Ns);
        vet.OnMovement(pet, from);

        Assert.False(pet.IsDeadBondedPet);
        Assert.Equal(skills, SkillBases(pet));
        Assert.Equal(1000, Bank(p.Pm));
        Assert.True(corpse!.Deleted);
        Assert.Equal(pet, necklace.Parent);
        Assert.Null(p.Pm.FindGump<TutorialVetResurrectGump>());
        Assert.True(SentText(p.Ns, before, TutorialPetCare.IsleRevivedLine));
    }

    // ---------------------------------------------------------------- 2

    [Fact]
    public void OffTheIsleTobinWaitsForTheBodyAndTakesTheSkillLossFreeWhileYoung()
    {
        using var p = new Player(young: true, Map.Trammel, TownSpot);
        var vet = Tobin(p, Off(TownSpot, 1));
        var (pet, _) = DeadHorse(p, Off(TownSpot, 2), fade: false);
        var skills = SkillBases(pet);

        vet.OnMovement(pet, Off(TownSpot, 12));
        Assert.True(pet.IsDeadBondedPet); // no revival at once off the isle

        Assert.True(vet.Offer(p.Pm));
        var offer = p.Pm.FindGump<TutorialVetResurrectGump>()!;
        offer.OnResponse(p.Ns, Press(1));
        Assert.True(pet.IsDeadBondedPet); // its body has not faded yet

        pet.Corpse!.Delete();
        offer.OnResponse(p.Ns, Press(1));
        Assert.False(pet.IsDeadBondedPet);
        Assert.Equal(0, Bank(p.Pm));
        Assert.Equal(AfterLoss(skills), SkillBases(pet));
    }

    // ---------------------------------------------------------------- 3

    [Fact]
    public void AStockVetOffersFreezesAndBringsThePetBackForServUOsFee()
    {
        using var p = new Player(young: false, Map.Trammel, TownSpot, bankGold: 50000);
        var vet = p.Add(new Veterinarian());
        vet.MoveToWorld(Off(TownSpot, 2), Map.Trammel);
        var (pet, _) = DeadHorse(p, Off(TownSpot, -1), fade: true);
        var skills = SkillBases(pet);
        var fee = ServUOFee(pet);
        Assert.Equal(29.1, pet.MinTameSkill, 1);

        vet.OnMovement(p.Pm, Off(TownSpot, -10));
        var offer = p.Pm.FindGump<VetResurrectGump>();
        Assert.NotNull(offer);
        Assert.True(p.Pm.Frozen);

        offer!.OnResponse(p.Ns, Press(VetResurrectGump.OkButton, 0));
        Assert.False(p.Pm.Frozen);
        Assert.False(pet.IsDeadBondedPet);
        Assert.Equal(50000 - fee, Bank(p.Pm));
        Assert.Equal(AfterLoss(skills), SkillBases(pet));
    }

    // ---------------------------------------------------------------- 4

    [Fact]
    public void OnTheIsleAnOwnerWhoIsNotYoungPaysAtTobinAndThePetLosesSkill()
    {
        var vetSpot = new Point3D(TutorialFarm.FarmVetSpot, 0);
        using var p = new Player(young: false, TutorialWorld.Isle, Off(vetSpot, -1), bankGold: 50000);
        var vet = Tobin(p, vetSpot);
        var (pet, _) = DeadHorse(p, Off(vetSpot, -2), fade: true);
        var skills = SkillBases(pet);
        var fee = ServUOFee(pet);

        vet.OnMovement(pet, Off(vetSpot, -12));
        Assert.True(pet.IsDeadBondedPet); // the isle's free revival is for Young owners

        Assert.True(vet.Offer(p.Pm));
        p.Pm.FindGump<TutorialVetResurrectGump>()!.OnResponse(p.Ns, Press(1));
        Assert.False(pet.IsDeadBondedPet);
        Assert.Equal(50000 - fee, Bank(p.Pm));
        Assert.Equal(AfterLoss(skills), SkillBases(pet));
    }

    // ---------------------------------------------------------------- 5

    [Fact]
    public void SeraAndWrenPointAPetGhostToTobinAndTheFarmSaysWhatToDo()
    {
        var vetSpot = new Point3D(TutorialFarm.FarmVetSpot, 0);
        using var p = new Player(young: true, TutorialWorld.Isle, Off(vetSpot, -10));
        TutorialTestStart.Begin(p.Pm);
        p.Pm.Young = true;
        p.Pm.MoveToWorld(Off(vetSpot, -10), TutorialWorld.Isle);
        Tobin(p, vetSpot);
        var sera = p.Add(new TutorialHealer());
        sera.MoveToWorld(Off(vetSpot, -12), TutorialWorld.Isle);
        var wren = p.Add(new TutorialFarmhand());
        var (pet, _) = DeadHorse(p, Off(vetSpot, -9), fade: false);

        Assert.Equal(TutorialFarm.HealerGhostPetYoungLine, TutorialFarm.GhostPetLineFor(sera, pet));
        Assert.Equal(TutorialFarm.GhostPetYoungLine, TutorialFarm.GhostPetLineFor(wren, pet));
        Assert.Contains("Tobin", TutorialFarm.HealerGhostPetYoungLine);
        Assert.Contains("free", TutorialFarm.GhostPetYoungLine);

        // Sera says it when the ghost steps near her.
        var before = Mark(p.Ns);
        var from = pet.Location;
        pet.MoveToWorld(Off(vetSpot, -11), TutorialWorld.Isle);
        sera.OnMovement(pet, from);
        Assert.True(SentText(p.Ns, before, TutorialFarm.HealerGhostPetYoungLine));

        // An owner who is not Young hears to wait for the body.
        p.Pm.Young = false;
        Assert.Equal(TutorialFarm.HealerGhostPetLine, TutorialFarm.GhostPetLineFor(sera, pet));
        Assert.Equal(TutorialFarm.GhostPetLine, TutorialFarm.GhostPetLineFor(wren, pet));

        // Finishing the farm says the pet lesson line.
        var record = ClusterFTutorial.RecordOf(p.Pm)!;
        record.PetClaimed = true;
        record.StationsDone.UnionWith([TutorialFarm.PestsKey, TutorialFarm.FollowKey, TutorialFarm.GuardKey]); // cc-P726
        before = Mark(p.Ns);
        Assert.True(TutorialFarm.CheckDone(p.Pm));
        Assert.True(SentText(p.Ns, before, TutorialPetCare.FarmPetLessonLine));
        Assert.Contains("vet", TutorialPetCare.FarmPetLessonLine);
    }
}
