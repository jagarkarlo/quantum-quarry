using System;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

static class Program
{
    static int assertions;

    static void Equal<T>(T expected, T actual, string name)
    {
        assertions++;
        if (!Equals(expected, actual))
            throw new Exception($"{name}: expected {expected}, got {actual}");
    }

    static void Main()
    {
        var pressure = new QuarryPressure();
        Equal(0, pressure.Tier, "initial tier");
        Equal(500, pressure.NextThreshold, "initial threshold");
        Equal(499, pressure.Collect(499, false), "base reward");
        Equal(0, pressure.Tier, "below threshold");
        Equal(1, pressure.Collect(1, false), "crossing uses advertised reward");
        Equal(1, pressure.Tier, "exact first threshold");
        Equal(125, pressure.PreviewReward(100, false), "tier one preview");
        Equal("x1.25", pressure.RewardLabel(false), "exact reward label");
        Equal("x2.5", pressure.RewardLabel(true), "exact critical reward label");
        Equal(250, pressure.PreviewReward(100, true), "critical stacks with pressure");
        Equal(1250, pressure.Collect(1000, false), "tier one collection");
        Equal(2, pressure.Tier, "exact second threshold");
        Equal(true, pressure.HasHazardPulses, "pulse threshold");
        Equal(2250, pressure.Collect(1500, false), "tier two collection");
        Equal(3, pressure.Tier, "exact third threshold");
        Equal(175, pressure.RewardPercent, "maximum multiplier");
        Equal(0, pressure.NextThreshold, "maximum threshold");
        Equal(4000, pressure.Bank(), "bank exact accumulated rewards");
        Equal(0, pressure.Tier, "bank resets tier");
        Equal(0, pressure.PendingCoins, "bank clears pending");
        Equal(0, pressure.Bank(), "bank idempotent");
        Equal(0, pressure.Collect(-100, false), "negative pickup ignored");
        Equal(0, pressure.CarriedOre, "negative pickup cannot lower pressure");

        pressure.Collect(500, true);
        Equal(500, pressure.CarriedOre, "critical does not accelerate pressure");
        var restored = new QuarryPressure(pressure.Seed, pressure.CarriedOre, pressure.PendingCoins);
        Equal(pressure.Tier, restored.Tier, "restore tier");
        Equal(pressure.PendingCoins, restored.PendingCoins, "restore pending");
        Equal(pressure.HasSwiftEnemies, restored.HasSwiftEnemies, "restore seeded modifier");
        pressure.Discard();
        Equal(0, pressure.PendingCoins, "death discards pending");
        Equal(0, pressure.Tier, "death resets pressure");
        var invalid = new QuarryPressure(-1, -1, -1);
        Equal(0, invalid.CarriedOre, "negative save ore");
        Equal(0, invalid.PendingCoins, "negative save coins");
        var capped = new QuarryPressure(int.MaxValue, int.MaxValue, int.MaxValue - 1);
        Equal(1, capped.Collect(int.MaxValue, true), "reward overflow cap");
        Equal(int.MaxValue, capped.PendingCoins, "pending cannot overflow");
        Equal(int.MaxValue, capped.CarriedOre, "ore cannot overflow");
        Equal(0, capped.Collect(100, false), "full pending wallet");
        Equal(QuarryPressure.PulsePhase.Dormant, pressure.GetPulsePhase(5.5f), "low pressure dormant");
        Equal(QuarryPressure.PulsePhase.Dormant, new QuarryPressure(7319, 1499).GetPulsePhase(5.5f),
            "last ore before arming remains harmless");
        var pulses = new QuarryPressure(7319, 1500);
        Equal(QuarryPressure.PulsePhase.Idle, pulses.GetPulsePhase(0f), "activation grace");
        Equal(QuarryPressure.PulsePhase.Idle, pulses.GetPulsePhase(3.49f), "before warning");
        Equal(QuarryPressure.PulsePhase.Warning, pulses.GetPulsePhase(3.5f), "warning boundary");
        Equal(QuarryPressure.PulsePhase.Warning, pulses.GetPulsePhase(4.99f), "warning is safe");
        Equal(QuarryPressure.PulsePhase.Active, pulses.GetPulsePhase(5f), "active boundary");
        Equal(QuarryPressure.PulsePhase.Active, pulses.GetPulsePhase(5.99f), "active window");
        Equal(QuarryPressure.PulsePhase.Idle, pulses.GetPulsePhase(6f), "cycle resets");
        Equal(QuarryPressure.PulsePhase.Active, pulses.GetPulsePhase(11f), "cycle repeats");
        Equal(QuarryPressure.PulsePhase.Idle, pulses.GetPulsePhase(-1f), "negative clock safe");
        Equal(QuarryPressure.PulsePhase.Dormant, pulses.GetPulsePhase(float.NaN), "invalid clock safe");
        Equal(QuarryPressure.PulsePhase.Dormant, pulses.GetPulsePhase(float.PositiveInfinity), "infinite clock safe");
        pulses.Bank();
        Equal(QuarryPressure.PulsePhase.Dormant, pulses.GetPulsePhase(5f), "bank disables pulses");
        Equal(false, pulses.MeetsGateRequirement(1), "tier 0 fails tier 1 gate");
        Equal("LOCK T1", pulses.GetGateLabel(1), "tier 0 gate label");
        Equal("LOCK T1", pulses.GetGateLabel(0), "gate clamps minimum tier");
        Equal("LOCK T3", pulses.GetGateLabel(9), "gate clamps maximum tier");
        var gateTier1 = new QuarryPressure(7319, 500);
        Equal(true, gateTier1.MeetsGateRequirement(1), "tier 1 opens tier 1 gate");
        Equal(false, gateTier1.MeetsGateRequirement(2), "tier 1 fails tier 2 gate");
        Equal("OPEN", gateTier1.GetGateLabel(1), "open gate label");
        Equal("LOCK T2", gateTier1.GetGateLabel(2), "locked tier 2 gate label");
        var gateTier2 = new QuarryPressure(7319, 1500);
        Equal(true, gateTier2.MeetsGateRequirement(2), "tier 2 opens tier 2 gate");
        Equal("OPEN", gateTier2.GetGateLabel(2), "tier 2 gate open label");
        for (int seed = -10; seed <= 10; seed++)
        {
            for (int tierOre = 0; tierOre <= 3000; tierOre += 500)
            {
                var first = new QuarryPressure(seed, tierOre);
                var second = new QuarryPressure(seed, tierOre);
                Equal(first.HasSwiftEnemies, second.HasSwiftEnemies, "reproducible modifier");
                Equal(first.ChaseMultiplier, second.ChaseMultiplier, "reproducible speed");
            }
        }
        foreach (string[] artwork in new[]
        {
            QuarryPressureArt.Checkpoint, QuarryPressureArt.Vent, QuarryPressureArt.Crate, QuarryPressureArt.Gate,
            QuarryPressureArt.JumpPad, QuarryPressureArt.Sentry, QuarryPressureArt.ShieldedEnemy
        })
        {
            Equal(16, artwork.Length, "artwork height");
            foreach (string row in artwork)
            {
                Equal(16, row.Length, "artwork width");
                foreach (char pixel in row)
                    Equal(true, QuarryPressureArt.Palette.Contains(pixel), "valid artwork palette");
            }
        }
        foreach (bool surface in new[] { false, true })
            for (int frame = 0; frame < QuarryLavaArt.FrameCount; frame++)
            {
                bool validPalette = true;
                bool opaqueBody = true;
                bool loops = true;
                bool animated = false;
                for (int y = 0; y < QuarryLavaArt.Size; y++)
                    for (int x = 0; x < QuarryLavaArt.Size; x++)
                    {
                        char pixel = QuarryLavaArt.Pixel(x, y, frame, surface);
                        validPalette &= QuarryLavaArt.Palette.Contains(pixel);
                        opaqueBody &= surface || pixel != '.';
                        loops &= pixel == QuarryLavaArt.Pixel(x, y, frame + QuarryLavaArt.FrameCount, surface);
                        animated |= pixel != QuarryLavaArt.Pixel(x, y, frame + 1, surface);
                    }
                Equal(true, validPalette, "lava uses the original warm palette");
                Equal(true, opaqueBody, "lava body has no transparent holes");
                Equal(true, loops, "lava animation loops without a phase discontinuity");
                Equal(true, animated, "each lava frame changes the flow");
            }
        for (int level = 1; level <= 6; level++)
        {
            Equal(LiquidKind.Lava, LiquidRules.ClassifyTile("LavaSurface", level), "explicit lava surface");
            Equal(LiquidKind.Lava, LiquidRules.ClassifyTile("LavaBody", level), "explicit lava body");
            Equal(level == 6 ? LiquidKind.Lava : LiquidKind.Water,
                LiquidRules.ClassifyTile("SPA_Rock_Grass_Water_28", level), "legacy surface remains compatible");
            Equal(level == 6 ? LiquidKind.Lava : LiquidKind.Water,
                LiquidRules.ClassifyTile("SPA_Rock_Grass_Water_29", level), "legacy body remains compatible");
        }
        Equal(true, LiquidRules.IsLiquidTile("LavaSurface"), "authored lava is recognized as liquid");
        Equal(false, LiquidRules.IsLiquidTile("LavaSurfacePreview"), "lava names match exactly");
        Equal(LiquidKind.None, LiquidRules.ClassifyTile(null, 6), "empty cell is not lava");
        Equal("level-4", LevelMedals.NormalizeLevelId("Level 4"), "normalize campaign scene name");
        Equal("level-2", LevelMedals.NormalizeLevelId("level-2"), "normalize stable level id");
        Equal(null, LevelMedals.NormalizeLevelId("Store"), "non-gameplay scene has no level id");
        Equal(null, LevelMedals.NormalizeLevelId("Level selector"), "selector scene has no level id");
        var l4Result = LevelMedals.Evaluate("Level 4", 60000, 1600, 100, 0);
        Equal(MedalTier.Gold, l4Result.TimeMedal, "level 4 gold time");
        Equal(MedalTier.Gold, l4Result.OreMedal, "level 4 gold ore");
        Equal(MedalTier.Gold, l4Result.SurvivalMedal, "level 4 gold survival");
        Equal(MedalTier.Gold, l4Result.OverallMedal, "overall gold when all gold");
        var pb = LevelMedals.MergePersonalBest(default, l4Result);
        Equal(true, pb.Completed, "personal best marks completed");
        Equal(60000, pb.BestTimeMs, "initial best time");
        Equal(1600, pb.BestBankedOre, "initial best ore");
        Equal(0, pb.FewestHits, "initial fewest hits");
        Equal("T:G O:G S:G", LevelMedals.FormatShortBadge(pb), "short medal badge");
        var slowerMoreOre = LevelMedals.Evaluate("Level 4", 90000, 2000, 0, 2);
        Equal(MedalTier.Silver, slowerMoreOre.TimeMedal, "silver time");
        Equal(MedalTier.Bronze, slowerMoreOre.SurvivalMedal, "bronze survival");
        pb = LevelMedals.MergePersonalBest(pb, slowerMoreOre);
        Equal(60000, pb.BestTimeMs, "merge keeps faster time");
        Equal(2000, pb.BestBankedOre, "merge updates higher banked ore");
        Equal(0, pb.FewestHits, "merge keeps fewer hits");
        Equal(MedalTier.Gold, pb.BestTimeMedal, "merge retains gold time medal");
        Equal("42.5s", LevelMedals.FormatDuration(42500), "sub-minute duration format");
        Equal("1:05.2s", LevelMedals.FormatDuration(65200), "minute duration format");
        CustomLevelDocument starter = CustomLevelDocument.CreateStarterDraft();
        Equal(0, starter.Validate().Count, "starter custom level draft is valid");
        Equal("ground", starter.GetTile(2, 1), "starter ground tile");
        Equal("ladder", starter.GetTile(6, 3), "starter ladder tile");
        Equal("bounce", starter.GetTile(13, 2), "starter bounce mushroom tile");
        Equal("coin_100", starter.GetObject(4, 2), "starter coin object");
        Equal("red_enemy", starter.GetObject(10, 6), "starter red enemy object");
        Equal(true, starter.SetTile(-8, -3, "lava"), "paint lava tile at negative campaign coordinate");
        Equal(true, starter.SetTile(-8, -3, "bg"), "background tile coexists with foreground tile");
        Equal("lava", starter.GetTile(-8, -3), "foreground tile returned over background");
        Equal(true, starter.SetObject(9, 2, "platform"), "place moving platform object");
        Equal(true, starter.SetObject(11, 2, "coin_200"), "place 200-ore coin object");
        Equal(false, starter.SetObject(starter.spawnX, starter.spawnY, "coin_100"), "cannot place object on spawn");
        Equal(false, starter.SetObject(3, 3, "malicious_prefab"), "reject non-allowlisted object id");
        Equal(false, starter.SetTile(3, 3, "unknown_tile"), "reject non-allowlisted tile id");
        string json = starter.ToJson();
        CustomLevelDocument roundTrip = CustomLevelDocument.FromJson(json);
        Equal(0, roundTrip.Validate().Count, "round-trip custom level is valid");
        Equal(starter.tiles.Count, roundTrip.tiles.Count, "round-trip preserves tile count");
        Equal(starter.objects.Count, roundTrip.objects.Count, "round-trip preserves object count");
        Equal("lava", roundTrip.GetTile(-8, -3), "round-trip preserves negative-coord lava");
        Equal("platform", roundTrip.GetObject(9, 2), "round-trip preserves moving platform");
        int groundedY;
        float groundedTop;
        Equal(true, starter.TryResolveGroundedCell(10, 8, "enemy", out groundedY, out groundedTop), "enemy snaps down onto platform at y=5");
        Equal(6, groundedY, "enemy resolved cell y is 6 above platform y=5");
        Equal(false, starter.TryResolveGroundedCell(25, 14, "enemy", out groundedY, out groundedTop), "enemy rejected in mid-air with no floor within 10 cells");
        Equal(false, starter.TryResolveGroundedCell(10, 5, "enemy", out groundedY, out groundedTop), "enemy rejected inside solid tile");
        starter.ClearCell(10, 5);
        Equal("red_enemy", starter.GetObject(10, 2), "removing support tile under red_enemy at (10,6) drops it onto lower floor at (10,2)");
        var invalidDoc = new CustomLevelDocument { formatVersion = 99, width = 999, spawnX = 5, spawnY = 5, exitX = 5, exitY = 5 };
        Equal(true, invalidDoc.Validate().Count >= 3, "invalid document reports format, bounds, and overlap errors");
        Console.WriteLine($"Quarry Pressure/liquid rules and custom artwork: {assertions} assertions passed.");
        SessionTests.Run();
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        int sourceCount = 0;
        foreach (string directory in new[] { "Assets/Scripts", "Assets/Editor" })
        {
            foreach (string source in Directory.GetFiles(Path.Combine(root, directory), "*.cs"))
            {
                string text = File.ReadAllText(source);
                foreach (string[] symbols in new[] { Array.Empty<string>(), new[] { "QUARRY_VALIDATION" } })
                {
                    var tree = CSharpSyntaxTree.ParseText(text,
                        new CSharpParseOptions(LanguageVersion.CSharp9, preprocessorSymbols: symbols), source);
                    foreach (Diagnostic diagnostic in tree.GetDiagnostics())
                        if (diagnostic.Severity == DiagnosticSeverity.Error)
                            throw new Exception(diagnostic.ToString());
                }
                sourceCount++;
            }
        }
        Console.WriteLine($"C# 9 syntax: {sourceCount} Unity source files passed in normal and validation configurations (not Unity API compilation).");
    }
}