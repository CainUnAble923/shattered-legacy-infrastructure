// cc-P65, F-35 Part B: the staff test path for evo pets. Nothing in the world spawns one; until the tutorial's Farmhand
// does (it will call EvoPet.GivePet, the same as "give" here), staff hand them out and walk them through their stages.

using Server.Commands;
using Server.Mobiles;
using Server.Targeting;

namespace Server
{
    public static class EvoPetCommands
    {
        public static void Configure()
        {
            CommandSystem.Register("EvoPet", AccessLevel.GameMaster, OnCommand);
        }

        [Usage("EvoPet give <dog|cat|fox|bird> | stage <n> [body] | xp <amount> | bond <on|off> | loops <n|-1> | pick <stage> <body> | info")]
        [Description("Evo pet test tools: give a player an evo pet at stage 1 (tamed, bonded, hue picker), or target an evo pet to set its stage, add XP, mark the Bond Trial, stand in for its owner's loop count, change a pick, or read its state.")]
        [ShardCommand(CommandCategory.DevTool, Summary = "Gives and adjusts evo pets for testing.")]
        private static void OnCommand(CommandEventArgs e)
        {
            var from = e.Mobile;
            var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "";

            switch (verb)
            {
                case "give" when e.Length > 1 && EvoPetData.Line(e.GetString(1)) != null:
                    {
                        var line = e.GetString(1);
                        from.SendMessage($"Target the player to give an evo {line}.");
                        from.Target = new GiveTarget(line);
                        return;
                    }
                case "stage" when e.Length > 1:
                    {
                        var n = e.GetInt32(1);
                        var body = e.Length > 2 ? e.GetInt32(2) : 0;
                        Pet(from, p => p.StaffSetStage(n, body) ? $"Now stage {n}: {p.EvoStatus}" : $"Stage {n} does not exist or its art is pending.");
                        return;
                    }
                case "xp" when e.Length > 1:
                    {
                        var xp = e.GetInt32(1);
                        Pet(from, p =>
                            {
                                var gained = p.StaffAddXp(xp);
                                return $"{(gained > 0 ? $"Evolved {gained} stage(s). " : "")}{p.EvoStatus}";
                            }
                        );
                        return;
                    }
                case "bond" when e.Length > 1:
                    {
                        var on = e.GetString(1).ToLowerInvariant() is "on" or "true" or "1";
                        Pet(from, p =>
                            {
                                p.BondTrialDone = on;
                                p.TryEvolve();
                                return $"Bond Trial {(on ? "marked done" : "cleared")}. {p.EvoStatus}";
                            }
                        );
                        return;
                    }
                case "loops" when e.Length > 1:
                    {
                        var loops = e.GetInt32(1);
                        Pet(from, p =>
                            {
                                p.TestOwnerLoops = loops < 0 ? -1 : loops;
                                p.TryEvolve();
                                return $"Owner's loops read as {p.OwnerLoops}. {p.EvoStatus}";
                            }
                        );
                        return;
                    }
                case "pick" when e.Length > 2:
                    {
                        var stage = e.GetInt32(1);
                        var body = e.GetInt32(2);
                        Pet(from, p => p.StaffSetPick(stage, body) ? $"Stage {stage} pick is now {body}." : "No such pick.");
                        return;
                    }
                case "info":
                    {
                        Pet(from, p => $"{p.Line?.Name} line, {p.EvoStatus}, body {(int)p.Body}, hue 0x{p.Hue:X}, owner loops {p.OwnerLoops}, bond trial {(p.BondTrialDone ? "done" : "not done")}");
                        return;
                    }
            }

            from.SendMessage("Usage: [EvoPet give <dog|cat|fox|bird> | stage <n> [body] | xp <amount> | bond <on|off> | loops <n|-1> | pick <stage> <body> | info");
        }

        private static void Pet(Mobile from, System.Func<EvoPet, string> act)
        {
            from.SendMessage("Target an evo pet.");
            from.Target = new PetTarget(act);
        }

        private class PetTarget : Target
        {
            private readonly System.Func<EvoPet, string> _act;

            public PetTarget(System.Func<EvoPet, string> act) : base(-1, false, TargetFlags.None) => _act = act;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (targeted is EvoPet pet)
                {
                    from.SendMessage(_act(pet));
                }
                else
                {
                    from.SendMessage("That is not an evo pet.");
                }
            }
        }

        private class GiveTarget : Target
        {
            private readonly string _line;

            public GiveTarget(string line) : base(-1, false, TargetFlags.None) => _line = line;

            protected override void OnTarget(Mobile from, object targeted)
            {
                if (targeted is not PlayerMobile pm)
                {
                    from.SendMessage("Target a player.");
                    return;
                }

                var pet = EvoPet.GivePet(pm, _line);
                from.SendMessage(pet == null ? "Not given (no free follower slot?)." : $"Gave {pm.Name} {pet.Name}.");
            }
        }
    }
}
