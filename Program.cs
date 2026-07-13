using System;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.Diagnostics.Snapshots;
using Crusaders30XX.ECS.Data.Save;

ShaderRuntimeOptions.ConfigureFromArgs(args);
GpuProfilingRuntimeOptions.ConfigureFromArgs(args);
NewGameLaunchOptions.ConfigureFromArgs(args);
TutorialLaunchOptions.ConfigureFromArgs(args);
UnlockLaunchOptions.ConfigureFromArgs(args);
if (!ShaderRuntimeOptions.ShadersEnabled)
{
    Console.WriteLine("[Launch] GPU screen effects disabled (no-shaders)");
}
#if DEBUG
if (GpuProfilingRuntimeOptions.Enabled)
{
    Console.WriteLine("[Launch] Asynchronous GPU timing and rendering counters enabled (profile-gpu)");
}
#endif

var appArgs = TutorialLaunchOptions.StripLaunchFlag(
    NewGameLaunchOptions.StripLaunchFlag(
        UnlockLaunchOptions.StripLaunchFlag(
            GpuProfilingRuntimeOptions.StripLaunchFlag(ShaderRuntimeOptions.StripLaunchFlags(args)))));

DisplaySnapshotLaunchOptions snapshotOptions = null;
TestFightLaunchOptions testFightOptions = null;
CardListProfileLaunchOptions cardListProfileOptions = null;
try
{
    if (CardListProfileLaunchOptions.TryParse(appArgs, out var parsedCardListProfile))
    {
        if (NewGameLaunchOptions.DeleteSaveBeforeLaunch || UnlockLaunchOptions.UnlockAllCollectionItems)
        {
            throw new CardListProfileSetupException("The new and unlock flags cannot be combined with card-list-profile");
        }
        cardListProfileOptions = parsedCardListProfile;
        TutorialLaunchOptions.ForceSkip();
        Console.WriteLine("[Launch] Card-list profile: 60 cards, 3840x2160, 180 warm-up + 300 measured frames");
    }
    else if (TestFightLaunchOptions.TryParse(appArgs, out var parsedTestFight))
    {
        if (NewGameLaunchOptions.DeleteSaveBeforeLaunch || UnlockLaunchOptions.UnlockAllCollectionItems)
        {
            throw new TestFightSetupException(
                "The new and unlock flags cannot be combined with test-fight because test fights do not modify saves.");
        }
        testFightOptions = parsedTestFight;
        TutorialLaunchOptions.ForceSkip();
        Console.WriteLine(
            $"[Launch] Test fight: {testFightOptions.WeaponId} vs {testFightOptions.EnemyId} ({testFightOptions.Difficulty})");
    }
    else if (DisplaySnapshotLaunchOptions.TryParse(appArgs, out var parsed))
    {
        snapshotOptions = parsed;
    }
}
catch (Exception ex) when (ex is DisplaySnapshotSetupException or TestFightSetupException or CardListProfileSetupException)
{
    Console.Error.WriteLine($"[Launch] {ex.Message}");
    Environment.ExitCode = 1;
    return;
}

if (TutorialLaunchOptions.SkipTutorials)
{
    Console.WriteLine("[Launch] Tutorials disabled (skip-tutorials)");
}

if (NewGameLaunchOptions.DeleteSaveBeforeLaunch)
{
    SaveCache.DeleteSaveFilesIfPresent();
}

if (UnlockLaunchOptions.UnlockAllCollectionItems)
{
    SaveCache.UnlockAllCollectionItems();
    Console.WriteLine("[Launch] Collection unlocked (cards, medals, equipment)");
}

using var game = new Crusaders30XX.Game1(snapshotOptions, testFightOptions, cardListProfileOptions);
game.Run();
