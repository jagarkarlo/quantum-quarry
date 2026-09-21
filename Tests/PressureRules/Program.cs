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
        foreach (string[] artwork in new[] { QuarryPressureArt.Checkpoint, QuarryPressureArt.Vent })
        {
            Equal(16, artwork.Length, "artwork height");
            foreach (string row in artwork)
            {
                Equal(16, row.Length, "artwork width");
                foreach (char pixel in row)
                    Equal(true, QuarryPressureArt.Palette.Contains(pixel), "valid artwork palette");
            }
        }
        Console.WriteLine($"Quarry Pressure rules and custom artwork: {assertions} assertions passed.");
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