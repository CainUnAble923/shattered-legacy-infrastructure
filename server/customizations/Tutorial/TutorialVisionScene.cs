// cc-P665 (P481 F5): the loop vision as a staged scene, about 45 seconds, for one player at the standing stones of their
// lesson map (TutorialWorld.MapOf: the Fold today, their private copy once P664 lands). Notes: shard-migration
// notes/cc-P665-vision-scene.md. Storyboard: P481 section 6; corrections: P481 12.3 and P605 risks 6 and 7.
//
// It plays in place at the stones (P673 1.4 keeps a chamber optional, and a private copy is already private), so no
// chamber pool. What it does, and to whom:
//   - Frozen for the length of the scene, released at the end (Frozen is not saved: Mobile.cs keeps it in memory only).
//   - Light per player: FoldRegion.AlterLightLevel asks LightOf, and each change is pushed with CheckLightLevels(true).
//     A client's own light setting can override this (P605 risk 7), so the subtitles carry the story on their own.
//   - Fades: NetState.SendScreenEffect. TazUO reads the packet and draws nothing ("Effect not implemented",
//     ClassicUO.Client/Network/PacketHandlers/GraphicEffect.cs); other clients may honour it. The light still dims.
//   - Every effect and sound goes to the player's NetState only (pinned Effects sends to everyone in range).
//   - Subtitles: a borderless gump low in the middle of the screen, light text on its own dark band, so it reads whether
//     the world is dark, bright, or lit by the client's own setting.
//   - The figures (TutorialVisionFigure) are plain mobiles: the echo copies the player's body, hair and worn look as
//     plain items, nothing else (no Mirror Image Clone: it copies skills, follows, times out and registers itself).
//     Figures and props are deleted at the end, and delete themselves if a save ever loads them.
//   - Recovery: logout, disconnect, death, a move off the map, or a cancelled lesson ends the scene at once: the player
//     is put back at the stones, unfrozen, light restored, figures gone, and the lesson can be played again. A restart
//     mid-scene is covered by TutorialVisionStore, which saves who was in a scene and what it placed; Initialize (after
//     World.Load) and the player's next login finish the recovery.
//   - It changes no skill, level, loop or Young status: it never calls ClusterFLoops, and nothing here writes skills.
//   - Panels: two full-screen gump IDs are reserved in the art registry (PanelBrokenSword, PanelCrest) for Chase's later
//     drawings. They show only when ShowPanels is on (clusterf.tutorial.visionPanels, off), so the scene runs without them.

#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

// cc-P721 (Chase 10-28 09:22, "vision is cool, it's dark at beginning, we need actor to swing sword at gem"): the
// opening is lit from its second beat (Lit, and two braziers by the Gem), the Stranger walks up to the Gem and swings
// at it (a one-handed attack animation facing it, a sword hit, a burst on the Gem) before it breaks, and every beat is
// later, so each line stays up longer: 45 s became 61 s (+36%). The beats' seconds are named in Beats.

namespace Server.Tutorial;

public static class TutorialVisionScene
{
    /// <summary>cc-P721: each beat's second (P665's in brackets).</summary>
    public static class Beats
    {
        public const int Start = 0;       // [0] fade to black, "A vision."
        public const int Lights = 2;      // [3] lit: the Gem, the braziers, the Stranger at a distance
        public const int Walk = 4;        // the Stranger walks to the Gem: 4, 5, 6
        public const int Face = 7;        // turns to it
        public const int Strike = 8;      // [8] the swing, the hit, the bolt, "The sword strikes the Gem."
        public const int Burst = 9;       // [9]
        public const int Shards = 16;     // [12] the shards and the broken sword
        public const int Flash = 23;      // [17]
        public const int Fold = 24;       // [18]
        public const int Echo = 28;       // [21]
        public const int EchoSays = 30;   // [22]
        public const int EchoFade = 39;   // [29]
        public const int Hooded = 41;     // [30]
        public const int HoodedSays = 42; // [31]
        public const int Keep = 47;       // [34]
        public const int FadeOut = 52;    // [38]
        public const int Gate = 57;       // [42]
    }

    /// <summary>The scene's length in seconds: the last beat releases the player. cc-P721: 61 (P665: 45).</summary>
    public const int Length = 61;

    /// <summary>Full-screen panel 1, the broken sword (registry.csv, a placeholder until Chase draws it).</summary>
    public const int PanelBrokenSword = 23209;

    /// <summary>Full-screen panel 2, the League crest (registry.csv, a placeholder until Chase draws it).</summary>
    public const int PanelCrest = 23210;

    /// <summary>Show the two panels (clusterf.tutorial.visionPanels). Off until Chase's drawings are in the package.</summary>
    public static bool ShowPanels { get; set; }

    // Light levels (0 is full day, 30 near black: LightCycle's scale).
    public const int Black = 30;
    public const int Dim = 12;   // cc-P721: was 18, too dark to follow
    public const int Lit = 6;    // cc-P721: the opening, so the Gem and the Stranger read from the first beat
    public const int Flash = 0;
    public const int BrazierId = 0x0E31; // a lit brazier
    public const int SwordHitSound = 0x237; // the longsword's hit (Longsword.DefHitSound)
    public const int SlashAnimation = 9;    // a one-handed slash (WeaponAnimation.Slash1H)

    public const int GemHue = 0x08AF;      // Celestial (ResourceInfo.cs)
    public const int DarkHue = 0x455;      // near-black cloth
    public const int GemId = 0x1EA7;       // "worldgem bit"
    public const int PedestalId = 0x1223;  // "pedestal", stone
    public const int SwordId = 0x0F61;     // longsword
    public const int SwordTipId = 0x0EC1;  // "broken knife": the snapped tip
    public const int ShardsId = 0x5738;    // "crystal shards"
    public const int RobeId = 0x1F03;
    public const int HoodId = 0x2684;      // "hooded shroud"

    /// <summary>The subtitles, in order (P481 section 6). Speech lines are also said above the speaker's head.</summary>
    public static readonly string[] Subtitles =
    [
        "A vision.",
        "Long ago, the Stranger came for the Gem of Immortality.",
        "The sword strikes the Gem. The shards fly.",
        "The blade breaks with it.",
        "You fall, and the world folds shut behind you.",
        EchoLine,
        FigureLine,
        "What you keep: the League, your recipes, your achievements, your guild ranks.",
        "This was only a vision. Nothing about you has changed. Yet.",
        "Take the moongate at the end of the dock."
    ];

    public const string EchoLine =
        "You wake where you started, remembering. That is a loop. Your level goes back to 1; your skills go with it.";

    public const string FigureLine =
        "Some of us remember. We keep the League, and we are looking for the pieces.";

    public sealed class Scene(PlayerMobile player, Map map, Action<PlayerMobile>? finished, Action<PlayerMobile>? ended)
    {
        public PlayerMobile Player { get; } = player;
        public Map Map { get; } = map;
        public Action<PlayerMobile>? Finished { get; } = finished;
        public Action<PlayerMobile>? Ended { get; } = ended;
        public Point3D Origin;
        public int Second;
        public int? Light;
        public string? Subtitle;
        public readonly List<IEntity> Placed = [];
        public TutorialVisionFigure? Stranger, Echo, Hooded;
        public Item? Gem, Sword;
        public TimerExecutionToken Timer;
    }

    private static readonly Dictionary<Serial, Scene> Active = new();

    public static void Configure() =>
        ShowPanels = ServerConfiguration.GetOrUpdateSetting("clusterf.tutorial.visionPanels", false);

    /// <summary>After World.Load: finishes the recovery of anyone a restart caught mid-scene (TutorialVisionStore).</summary>
    public static void Initialize() => TutorialVisionStore.RecoverAll();

    public static Scene? Current(Mobile m) => m != null ? Active.GetValueOrDefault(m.Serial) : null;

    public static bool IsPlaying(Mobile m) => Current(m) != null;

    /// <summary>The scene's light for <paramref name="m"/>, or null when not in a scene (FoldRegion.AlterLightLevel).</summary>
    public static int? LightOf(Mobile m) => Current(m)?.Light;

    /// <summary>FoldRegion's hook: inside a scene the world is as dark (or as bright) as the beat, and no personal light.</summary>
    public static void AlterLight(Mobile m, ref int global, ref int personal)
    {
        if (LightOf(m) is { } level)
        {
            global = level;
            personal = 0;
        }
    }

    /// <summary>The stones on the player's lesson map, where the scene is played and where recovery puts them.</summary>
    public static Point3D Stones(Map map) => TutorialWorld.Ground(map, TutorialWorld.Stones);

    /// <summary>
    /// Starts the scene for <paramref name="pm"/> where they stand. <paramref name="finished"/> runs after the last beat;
    /// <paramref name="ended"/> runs when it ends early. False when it cannot start (no client, dead, already playing).
    /// </summary>
    public static bool Play(PlayerMobile pm, Action<PlayerMobile>? finished = null, Action<PlayerMobile>? ended = null)
    {
        if (pm.Deleted || pm.NetState == null || !pm.Alive || pm.Map == null || pm.Map == Map.Internal || IsPlaying(pm))
        {
            return false;
        }

        TutorialWake.Stand(pm, true); // cc-P721: never both at once (cc-P740: at once, no slow get-up)
        // cc-P721: the windows that sit over the middle of the screen would hide the scene (the P721 capture: the lesson
        // page and the patch notes covered the Gem and the Stranger), so they close as it starts.
        pm.CloseGump<TutorialTrialGump>();
        pm.CloseGump<TutorialRecruiterGump>();
        pm.CloseGump<Server.BulletinGump>();
        var scene = new Scene(pm, pm.Map, finished, ended) { Origin = pm.Location };
        Active[pm.Serial] = scene;
        pm.Frozen = true;
        Beat(scene);
        Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), () => Tick(scene), out scene.Timer);
        return true;
    }

    private static void Tick(Scene scene)
    {
        var pm = scene.Player;
        if (Current(pm) != scene)
        {
            scene.Timer.Cancel();
            return;
        }

        if (pm.Deleted || pm.NetState == null || !pm.Alive || pm.Map != scene.Map)
        {
            End(pm, false);
            return;
        }

        scene.Second++;
        Beat(scene);
    }

    /// <summary>
    /// Ends <paramref name="pm"/>'s scene: figures and props gone, unfrozen, light restored, back at the stones, the
    /// subtitles closed. <paramref name="completed"/> says whether it ran to the end (then Finished runs, else Ended).
    /// </summary>
    public static void End(PlayerMobile pm, bool completed)
    {
        if (!Active.Remove(pm.Serial, out var scene))
        {
            return;
        }

        scene.Timer.Cancel();
        foreach (var e in scene.Placed)
        {
            if (e is Item { Deleted: false } item) { item.Delete(); }
            else if (e is Mobile { Deleted: false } m) { m.Delete(); }
        }

        scene.Placed.Clear();
        Restore(pm, scene.Map);
        if (completed) { scene.Finished?.Invoke(pm); }
        else
        {
            if (pm.NetState != null) { ClusterFTutorial.SystemLine(pm, "The vision fades. Double-click the lesson's signpost by the stones to see it again."); }
            scene.Ended?.Invoke(pm);
        }
    }

    /// <summary>Unfrozen, light recomputed, subtitles closed, and back at the stones if they are on that map.</summary>
    public static void Restore(PlayerMobile pm, Map? map)
    {
        pm.Frozen = false;
        pm.CloseGump<TutorialVisionSubtitleGump>();
        pm.CloseGump<TutorialVisionPanelGump>();
        if (map != null && map != Map.Internal && pm.Map == map && !pm.Deleted)
        {
            var stones = Stones(map);
            if (!pm.InRange(stones, 4)) { pm.MoveToWorld(stones, map); }
        }
        else if (map != null && pm.LogoutMap == map)
        {
            pm.LogoutLocation = Stones(map);
        }

        pm.NetState?.SendScreenEffect(ScreenEffectType.FadeIn);
        pm.CheckLightLevels(true);
    }

    /// <summary>
    /// A test seam for a process ending mid-scene: the scene is forgotten and the world is left as the save would hold it
    /// (the player still frozen, figures and props still placed). Never called by the server.
    /// </summary>
    public static void AbandonForRestart(PlayerMobile pm)
    {
        if (Active.Remove(pm.Serial, out var scene)) { scene.Timer.Cancel(); }
    }

    /// <summary>Every serial the scenes have in the world now (figures and props), for the save.</summary>
    public static IEnumerable<(Serial Player, int MapIndex, Serial[] Placed)> Snapshot() =>
        Active.Values.Select(s => (s.Player.Serial, s.Map.MapIndex, s.Placed.Select(e => e.Serial).ToArray()));

    // ---------------------------------------------------------------- the beats (P481 section 6)

    private static void Beat(Scene s)
    {
        var pm = s.Player;
        switch (s.Second)
        {
            case Beats.Start: // Touch the stones: frozen, fade to black.
                Screen(s, ScreenEffectType.FadeOut);
                SetLight(s, Black);
                TargetSparkle(s, pm, 0x376A);
                Sound(s, 0x1FE);
                Subtitle(s, 0);
                break;
            case Beats.Lights: // Lit: the Gem on a plinth between two braziers, the Stranger at a distance with a longsword.
                Screen(s, ScreenEffectType.FadeIn);
                SetLight(s, Lit);
                var pedestal = Place(s, new TutorialVisionProp(PedestalId), At(s, 2, -2));
                s.Gem = Place(s, new TutorialVisionProp(GemId) { Hue = GemHue, Name = "the Gem of Immortality" },
                    new Point3D(pedestal.X, pedestal.Y, pedestal.Z + TileData.ItemTable[PedestalId].CalcHeight));
                Place(s, new TutorialVisionProp(BrazierId) { Name = "a brazier", Light = LightType.Circle300 }, At(s, 1, -3));
                Place(s, new TutorialVisionProp(BrazierId) { Name = "a brazier", Light = LightType.Circle300 }, At(s, 3, -3));
                s.Stranger = Figure(s, TutorialVisionFigure.Stranger(), At(s, 6, -1), Direction.West);
                Sparkle(s, s.Gem, 0x376A, GemHue);
                Sound(s, 0x20F);
                Subtitle(s, 1);
                break;
            case Beats.Walk:
            case Beats.Walk + 1:
            case Beats.Walk + 2: // the Stranger walks up to the Gem, a tile a second
                if (s.Stranger is { Deleted: false } walker)
                {
                    var to = At(s, 6 - (s.Second - Beats.Walk + 1), -1);
                    walker.Direction = Direction.West | Direction.Running;
                    walker.MoveToWorld(to, s.Map);
                }

                if (s.Gem != null && s.Second != Beats.Walk + 1) { Sparkle(s, s.Gem, 0x376A, GemHue); }
                break;
            case Beats.Face: // facing the Gem (up and to the left of it), the sword raised
                if (s.Stranger is { Deleted: false } facer) { facer.Direction = Direction.Up; }
                break;
            case Beats.Strike: // The strike: a real swing at the Gem, the hit, the bolt.
                if (s.Stranger is { Deleted: false } striker)
                {
                    striker.Direction = Direction.Up;
                    striker.Animate(SlashAnimation, 7, 1, true, false, 0);
                }

                Sound(s, SwordHitSound);
                if (s.Gem != null) { Bolt(s, s.Gem); }
                Sound(s, 0x29);
                Subtitle(s, 2);
                break;
            case Beats.Burst:
                if (s.Gem != null) { Sparkle(s, s.Gem, 0x36BD, 0, 20); }
                Sound(s, 0x307);
                break;
            case Beats.Burst + 2: // a second blow, so it is seen
                if (s.Stranger is { Deleted: false } again) { again.Animate(SlashAnimation, 7, 1, true, false, 0); }
                Sound(s, SwordHitSound);
                if (s.Gem != null) { Sparkle(s, s.Gem, 0x3709, GemHue, 20); }
                break;
            case Beats.Shards: // The shards and the broken sword.
                if (s.Gem is { } gem)
                {
                    foreach (var (dx, dy) in ShardOffsets)
                    {
                        var to = Place(s, new TutorialVisionProp(ShardsId) { Name = "a shard of the Gem" }, At(s, 2 + dx, -2 + dy));
                        Moving(s, gem, to, 0x3779, GemHue);
                    }

                    gem.Delete();
                    s.Gem = null;
                }

                if (s.Stranger?.FindItemOnLayer(Layer.OneHanded) is { } blade) { blade.Delete(); }
                s.Sword = Place(s, new TutorialVisionProp(SwordId) { Name = "the broken sword", Hue = DarkHue }, At(s, 3, 0));
                Place(s, new TutorialVisionProp(SwordTipId) { Name = "the sword's broken point", Hue = DarkHue }, At(s, 4, 1));
                Sound(s, 0x211);
                Subtitle(s, 3);
                break;
            case Beats.Flash: // White flash, then black: the world folds.
                Screen(s, ScreenEffectType.LightFlash);
                SetLight(s, Flash);
                Sound(s, 0x220);
                Subtitle(s, 4);
                Panel(s, PanelBrokenSword);
                break;
            case Beats.Fold:
                Screen(s, ScreenEffectType.FadeOut);
                SetLight(s, Black);
                RemoveAllPlaced(s);
                break;
            case Beats.Echo: // Dim light: the player's own echo appears and speaks.
                Screen(s, ScreenEffectType.FadeIn);
                ClosePanel(s);
                SetLight(s, Dim);
                s.Echo = Figure(s, TutorialVisionFigure.EchoOf(pm), At(s, 1, 1), Direction.North);
                Sparkle(s, s.Echo, 0x376A);
                Sound(s, 0x44D);
                break;
            case Beats.EchoSays:
                Say(s, s.Echo, EchoLine);
                Subtitle(s, 5);
                break;
            case Beats.EchoFade:
                if (s.Echo != null) { Sparkle(s, s.Echo, 0x374A); }
                Sound(s, 0x3C4);
                break;
            case Beats.Hooded: // A hooded figure at the edge of the light, and the broken sword in a ring of stones (the crest).
                RemoveAllPlaced(s);
                s.Hooded = Figure(s, TutorialVisionFigure.Hooded(), At(s, -3, -3), Direction.Down);
                s.Sword = Place(s, new TutorialVisionProp(SwordId) { Name = "the broken sword", Hue = DarkHue }, At(s, -2, -1));
                Sound(s, 0x212);
                Panel(s, PanelCrest);
                break;
            case Beats.HoodedSays:
                Say(s, s.Hooded, FigureLine);
                Subtitle(s, 6);
                break;
            case Beats.Keep:
                ClosePanel(s);
                Subtitle(s, 7);
                break;
            case Beats.FadeOut: // Fade to black; back to the stones.
                Screen(s, ScreenEffectType.FadeOut);
                SetLight(s, Black);
                RemoveAllPlaced(s);
                TargetSparkle(s, pm, 0x376A);
                Sound(s, 0x1FE);
                Subtitle(s, 8);
                break;
            case Beats.Gate:
                Subtitle(s, 9);
                break;
            case Length: // Light restored, released.
                End(pm, true);
                break;
        }
    }

    private static readonly (int Dx, int Dy)[] ShardOffsets = [(-2, -1), (2, -1), (-1, 2), (2, 2), (0, -3)];

    private static Point3D At(Scene s, int dx, int dy) =>
        TutorialWorld.Ground(s.Map, new Point2D(s.Origin.X + dx, s.Origin.Y + dy));

    private static T Place<T>(Scene s, T item, Point3D at) where T : Item
    {
        item.MoveToWorld(at, s.Map);
        s.Placed.Add(item);
        return item;
    }

    private static TutorialVisionFigure Figure(Scene s, TutorialVisionFigure figure, Point3D at, Direction facing)
    {
        figure.Direction = facing;
        figure.MoveToWorld(at, s.Map);
        s.Placed.Add(figure);
        return figure;
    }

    private static void RemoveAllPlaced(Scene s)
    {
        foreach (var e in s.Placed)
        {
            if (e is Item { Deleted: false } item) { item.Delete(); }
            else if (e is Mobile { Deleted: false } m) { m.Delete(); }
        }

        s.Placed.Clear();
        s.Gem = s.Sword = null;
        s.Stranger = s.Echo = s.Hooded = null;
    }

    private static void SetLight(Scene s, int level)
    {
        s.Light = level;
        s.Player.CheckLightLevels(true);
    }

    private static void Subtitle(Scene s, int line)
    {
        s.Subtitle = Subtitles[line];
        s.Player.CloseGump<TutorialVisionSubtitleGump>();
        s.Player.SendGump(new TutorialVisionSubtitleGump(s.Subtitle));
    }

    private static void Panel(Scene s, int gumpId)
    {
        if (!ShowPanels) { return; }
        s.Player.CloseGump<TutorialVisionPanelGump>();
        s.Player.SendGump(new TutorialVisionPanelGump(gumpId));
    }

    private static void ClosePanel(Scene s) => s.Player.CloseGump<TutorialVisionPanelGump>();

    /// <summary>Overhead to the one player (SayTo is PrivateOverheadMessage to their NetState).</summary>
    private static void Say(Scene s, Mobile? speaker, string text)
    {
        if (speaker is { Deleted: false } && s.Player.NetState != null) { speaker.SayTo(s.Player, text); }
    }

    // ---------------------------------------------------------------- one NetState only

    private static void Screen(Scene s, ScreenEffectType type) => s.Player.NetState?.SendScreenEffect(type);

    private static void Sound(Scene s, int soundId) => s.Player.NetState?.SendSoundEffect(soundId, s.Player.Location);

    /// <summary>An effect at a spot (pinned Effects.SendLocationParticles, to one client: particles or the plain effect).</summary>
    private static void Sparkle(Scene s, IEntity at, int itemId, int hue = 0, int duration = 30)
    {
        var ns = s.Player.NetState;
        if (ns == null) { return; }
        var spot = new Entity(Serial.Zero, at.Location, s.Map);
        if (Effects.SendParticlesTo(ns))
        {
            var p = stackalloc byte[OutgoingEffectPackets.ParticleEffectLength].InitializePacket();
            OutgoingEffectPackets.CreateLocationParticleEffect(p, spot, itemId, 10, duration, hue, 0, 5029, 0);
            ns.Send(p);
        }
        else
        {
            var p = stackalloc byte[OutgoingEffectPackets.HuedEffectLength].InitializePacket();
            OutgoingEffectPackets.CreateLocationHuedEffect(p, spot.Location, itemId, 10, duration, hue, 0);
            ns.Send(p);
        }
    }

    /// <summary>An effect on a mobile (pinned FixedParticles, to one client).</summary>
    private static void TargetSparkle(Scene s, Mobile on, int itemId)
    {
        var ns = s.Player.NetState;
        if (ns == null) { return; }
        if (Effects.SendParticlesTo(ns))
        {
            var p = stackalloc byte[OutgoingEffectPackets.ParticleEffectLength].InitializePacket();
            OutgoingEffectPackets.CreateTargetParticleEffect(p, on, itemId, 10, 30, 0, 0, 5008, (int)EffectLayer.Waist, 0);
            ns.Send(p);
        }
        else
        {
            var p = stackalloc byte[OutgoingEffectPackets.HuedEffectLength].InitializePacket();
            OutgoingEffectPackets.CreateTargetHuedEffect(p, on, itemId, 10, 30, 0, 0);
            ns.Send(p);
        }
    }

    private static void Bolt(Scene s, IEntity at)
    {
        var ns = s.Player.NetState;
        if (ns == null) { return; }
        var p = stackalloc byte[OutgoingEffectPackets.BoltEffectLength].InitializePacket();
        OutgoingEffectPackets.CreateBoltEffect(p, at, 0);
        ns.Send(p);
    }

    private static void Moving(Scene s, IEntity from, IEntity to, int itemId, int hue)
    {
        var ns = s.Player.NetState;
        if (ns == null) { return; }
        if (Effects.SendParticlesTo(ns))
        {
            var p = stackalloc byte[OutgoingEffectPackets.ParticleEffectLength].InitializePacket();
            OutgoingEffectPackets.CreateMovingParticleEffect(p, from, to, itemId, 5, 0, false, false, hue, 0, 9501, 1, 0,
                EffectLayer.Waist, 0);
            ns.Send(p);
        }
        else
        {
            var p = stackalloc byte[OutgoingEffectPackets.HuedEffectLength].InitializePacket();
            OutgoingEffectPackets.CreateMovingHuedEffect(p, from, to, itemId, 5, 0, false, false, hue, 0);
            ns.Send(p);
        }
    }

    // ---------------------------------------------------------------- recovery

    /// <summary>
    /// A login: anyone a restart caught mid-scene is recovered (TutorialVisionStore), and a scene still listed for this
    /// player (a reconnect inside the same run) is ended.
    /// </summary>
    [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
    public static void OnLogin(PlayerMobile pm)
    {
        End(pm, false);
        TutorialVisionStore.Recover(pm);
    }
}

/// <summary>The subtitle band: no border, its own dark band, light centred text (TutorialStyle colours).</summary>
public sealed class TutorialVisionSubtitleGump : Gump
{
    public const int Width = 600;
    public const int Height = 64;

    public string Text { get; }

    public TutorialVisionSubtitleGump(string text) : base(100, 420)
    {
        Text = text;
        Closable = false;
        Draggable = false;
        Resizable = false;
        AddAlphaRegion(0, 0, Width, Height);
        AddHtml(12, 10, Width - 24, Height - 16, $"<CENTER>{TutorialStyle.Html(text)}</CENTER>", false, false);
    }
}

/// <summary>A full-screen still (a reserved gump ID: TutorialVisionScene.PanelBrokenSword or PanelCrest).</summary>
public sealed class TutorialVisionPanelGump : Gump
{
    public int GumpId { get; }

    public TutorialVisionPanelGump(int gumpId) : base(0, 0)
    {
        GumpId = gumpId;
        Closable = false;
        Draggable = false;
        Resizable = false;
        AddImage(0, 0, gumpId);
    }
}

/// <summary>
/// Saved recovery (P605 risk 6): who was in a scene when the world saved, on which map, and what it had placed. Read
/// back at load; TutorialVisionScene.Initialize (after World.Load) and the player's login finish it.
/// </summary>
public class TutorialVisionStore : GenericPersistence
{
    public const int CurrentVersion = 0;

    private static TutorialVisionStore? _instance;

    /// <summary>Scenes read from the save and not yet recovered: player, map index, placed serials.</summary>
    private static readonly Dictionary<Serial, (int MapIndex, Serial[] Placed)> Pending = new();

    public TutorialVisionStore() : base("TutorialVision", 10)
    {
    }

    public static void Configure() => _instance ??= new TutorialVisionStore();

    public static int PendingCount => Pending.Count;

    public static bool IsPending(Serial player) => Pending.ContainsKey(player);

    public override void Serialize(IGenericWriter writer) => WriteTo(writer);

    public override void Deserialize(IGenericReader reader) => ReadFrom(reader);

    // Static, so a fact can round-trip the state without a second persistence (a Persistence registers itself).
    public static void WriteTo(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);
        var all = new Dictionary<Serial, (int MapIndex, Serial[] Placed)>(Pending);
        foreach (var (player, mapIndex, placed) in TutorialVisionScene.Snapshot())
        {
            all[player] = (mapIndex, placed);
        }

        writer.WriteEncodedInt(all.Count);
        foreach (var (player, (mapIndex, placed)) in all)
        {
            writer.Write(player);
            writer.WriteEncodedInt(mapIndex);
            writer.WriteEncodedInt(placed.Length);
            foreach (var serial in placed)
            {
                writer.Write(serial);
            }
        }
    }

    public static void ReadFrom(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"TutorialVisionStore version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        Pending.Clear();
        var count = reader.ReadEncodedInt();
        for (var i = 0; i < count; i++)
        {
            var player = reader.ReadSerial();
            var mapIndex = reader.ReadEncodedInt();
            var placed = new Serial[reader.ReadEncodedInt()];
            for (var k = 0; k < placed.Length; k++)
            {
                placed[k] = reader.ReadSerial();
            }

            Pending[player] = (mapIndex, placed);
        }
    }

    /// <summary>Recovers every pending scene whose player exists (startup, after World.Load).</summary>
    public static void RecoverAll()
    {
        foreach (var serial in Pending.Keys.ToList())
        {
            if (World.FindMobile(serial) is PlayerMobile pm) { Recover(pm); }
            else { Clean(serial); }
        }
    }

    /// <summary>Puts <paramref name="pm"/> back at the stones, unfrozen, light restored, and removes what the scene placed.</summary>
    public static void Recover(PlayerMobile pm)
    {
        if (!Pending.ContainsKey(pm.Serial)) { return; }
        var map = Map.Maps[Pending[pm.Serial].MapIndex];
        Clean(pm.Serial);
        TutorialVisionScene.Restore(pm, map);
    }

    private static void Clean(Serial player)
    {
        if (!Pending.Remove(player, out var scene)) { return; }
        foreach (var serial in scene.Placed)
        {
            var e = World.FindEntity(serial);
            if (e is TutorialVisionProp { Deleted: false } prop) { prop.Delete(); }
            else if (e is TutorialVisionFigure { Deleted: false } figure) { figure.Delete(); }
        }
    }
}

/// <summary>A figure in the vision: a plain mobile with no AI, skills or follow state. Deleted at the end; never kept by a save.</summary>
[SerializationGenerator(0, false)]
public partial class TutorialVisionFigure : Mobile
{
    public TutorialVisionFigure(string name)
    {
        Name = name;
        Body = 0x190;
        Blessed = true;
        Player = false;
    }

    public static TutorialVisionFigure Stranger()
    {
        var f = new TutorialVisionFigure("the Stranger") { Hue = 0x83EA };
        f.AddItem(new TutorialVisionProp(TutorialVisionScene.RobeId) { Hue = TutorialVisionScene.DarkHue, Layer = Layer.OuterTorso });
        f.AddItem(new TutorialVisionProp(TutorialVisionScene.SwordId) { Layer = Layer.OneHanded });
        return f;
    }

    public static TutorialVisionFigure Hooded()
    {
        var f = new TutorialVisionFigure("a hooded figure") { Hue = 0x83EA };
        f.AddItem(new TutorialVisionProp(TutorialVisionScene.HoodId) { Hue = TutorialVisionScene.DarkHue, Layer = Layer.OuterTorso });
        return f;
    }

    /// <summary>The player's echo: their body, hue, hair and what they wear, as plain items. Nothing else is copied.</summary>
    public static TutorialVisionFigure EchoOf(Mobile m)
    {
        var f = new TutorialVisionFigure(m.Name ?? "your echo")
        {
            Body = m.Body,
            Hue = m.Hue,
            Female = m.Female,
            HairItemID = m.HairItemID,
            HairHue = m.HairHue,
            FacialHairItemID = m.FacialHairItemID,
            FacialHairHue = m.FacialHairHue
        };

        foreach (var item in m.Items)
        {
            if (Shown(item.Layer))
            {
                f.AddItem(new TutorialVisionProp(item.ItemID) { Hue = item.Hue, Layer = item.Layer, Name = item.Name });
            }
        }

        return f;
    }

    /// <summary>The worn layers a viewer sees: hands to inner legs, without the backpack (hair is the mobile's own).</summary>
    public static bool Shown(Layer layer) =>
        layer is >= Layer.OneHanded and <= Layer.InnerLegs && layer is not (Layer.Backpack or Layer.Hair or Layer.FacialHair);

    [AfterDeserialization]
    private void AfterDeserialization() => Timer.DelayCall(Delete);
}

/// <summary>A prop in the vision (the gem, the pedestal, the shards, the sword, a figure's clothes). Never kept by a save.</summary>
[SerializationGenerator(0, false)]
public partial class TutorialVisionProp : Item
{
    public TutorialVisionProp(int itemId) : base(itemId)
    {
        Movable = false;
    }

    [AfterDeserialization]
    private void AfterDeserialization() => Timer.DelayCall(Delete);
}
