#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;
using Crusaders30XX.ECS.DataOriented.Resources;

namespace Crusaders30XX.ECS.DataOriented.Authoring.Text;

/// <summary>Compact managed-content IDs used by the first fixture-specific ECS-052 snapshots.</summary>
public static class SnapshotTextContentIds
{
    public static readonly StringId Paused = new(53001);
    public static readonly StringId AudioAndHaptics = new(53002);
    public static readonly StringId Music = new(53003);
    public static readonly StringId FiftyPercent = new(53004);
    public static readonly StringId Sfx = new(53005);
    public static readonly StringId Rumble = new(53006);
    public static readonly StringId On = new(53007);
    public static readonly StringId Off = new(53008);
    public static readonly StringId AbandonClimb = new(53009);
    public static readonly StringId Escape = new(53010);
    public static readonly StringId Resume = new(53011);
    public static readonly StringId KeyboardHotKeyHints = new(53012);
    public static readonly StringId XboxHotKeyHints = new(53013);
    public static readonly StringId PlayStationHotKeyHints = new(53014);
    public static readonly StringId Enter = new(53015);
    public static readonly StringId Space = new(53016);
    public static readonly StringId SampleAction = new(53017);
    public static readonly StringId B = new(53018);
    public static readonly StringId X = new(53019);
    public static readonly StringId Y = new(53020);
    public static readonly StringId Lb = new(53021);
    public static readonly StringId Rb = new(53022);
    public static readonly StringId L1 = new(53023);
    public static readonly StringId R1 = new(53024);
    public static readonly StringId Options = new(53025);
    public static readonly StringId Create = new(53026);
    public static readonly StringId BulwarkPlate = new(53027);
    public static readonly StringId GainTwoAegis = new(53028);
    public static readonly StringId FreeAction = new(53029);
    public static readonly StringId KnightlyGrieves = new(53030);
    public static readonly StringId Block = new(53031);
    public static readonly StringId Two = new(53032);
    public static readonly StringId KnightlyFlavorLineOne = new(53033);
    public static readonly StringId KnightlyFlavorLineTwo = new(53034);
    public static readonly StringId KnightlyFlavorLineThree = new(53035);
    public static readonly StringId KnightlyFlavorLineFour = new(53036);
    public static readonly StringId GuardianStayClose = new(53037);
    public static readonly StringId GuardianHonestStrike = new(53038);
    public static readonly StringId GuardianSaintMichael = new(53039);
    public static readonly StringId GuardianBigFoot = new(53040);
    public static readonly StringId One = new(53041);
    public static readonly StringId Three = new(53042);
    public static readonly StringId Five = new(53043);
    public static readonly StringId Six = new(53044);
    public static readonly StringId Nine = new(53045);
    public static readonly StringId Hp = new(53046);
    public static readonly StringId EighteenOfTwenty = new(53047);
    public static readonly StringId ThreeOfTwenty = new(53048);
    public static readonly StringId OneTwentyOfOneFifty = new(53049);
    public static readonly StringId ThirtyTwoOfFifty = new(53050);
    public static readonly StringId Cour = new(53051);
    public static readonly StringId Temp = new(53052);
    public static readonly StringId Ap = new(53053);
    public static readonly StringId PledgeAvailable = new(53054);
    public static readonly StringId Twelve = new(53055);
    public static readonly StringId OneTwoThree = new(53056);
    public static readonly StringId TwoAegis = new(53057);
    public static readonly StringId OneArmor = new(53058);
    public static readonly StringId TenPower = new(53059);
    public static readonly StringId TwentyFiveThorns = new(53060);
}

/// <summary>Unscaled host-font styles; fixture authoring owns each legacy per-label scale.</summary>
public static class SnapshotTextStyleIds
{
    public static readonly TextStyleId HudExact = new(101);
    public static readonly TextStyleId DisplayExact = new(102);
    public static readonly TextStyleId HudBoldItalicExact = new(103);
}

/// <summary>Managed immutable text/font metadata addressed only by compact IDs from ECS packets.</summary>
public sealed class StaticTextPresentationCatalog : ITextPresentationCatalog
{
    private readonly NewWorldSnapshotFixture[] fixtures;

    public StaticTextPresentationCatalog(NewWorldSnapshotFixtureHost? snapshotRegistry = null)
    {
        ReadOnlySpan<NewWorldSnapshotFixture> registered =
            (snapshotRegistry ?? new NewWorldSnapshotFixtureHost()).Registered;
        fixtures = new NewWorldSnapshotFixture[registered.Length];
        registered.CopyTo(fixtures);
    }

    public bool TryResolve(StringId id, out string? text)
    {
        text = id.Value switch
        {
            51001 => "CRUSADERS 30XX",
            51002 => "THE CLIMB",
            51003 => "WAY STATION",
            51004 => "ACHIEVEMENTS",
            51011 => "HEALTH",
            51012 => "ACTION POINTS",
            51013 => "COURAGE",
            51014 => "TEMPERANCE",
            51015 => "TEST FIGHT",
            53001 => "Paused",
            53002 => "Audio and haptics",
            53003 => "Music",
            53004 => "50%",
            53005 => "SFX",
            53006 => "Rumble",
            53007 => "ON",
            53008 => "OFF",
            53009 => "Abandon Climb",
            53010 => "ESC",
            53011 => "Resume",
            53012 => "Keyboard hotkey hints",
            53013 => "Xbox hotkey hints",
            53014 => "PlayStation hotkey hints",
            53015 => "ENTER",
            53016 => "SPACE",
            53017 => "Sample action",
            53018 => "B",
            53019 => "X",
            53020 => "Y",
            53021 => "LB",
            53022 => "RB",
            53023 => "L1",
            53024 => "R1",
            53025 => "OPT",
            53026 => "CREATE",
            53027 => "Bulwark Plate",
            53028 => "Gain 2 aegis.",
            53029 => "FREE ACTION",
            53030 => "Knightly Grieves",
            53031 => "BLOCK",
            53032 => "2",
            53033 => "Standard issue of the",
            53034 => "order. Built to hold the",
            53035 => "line when the march",
            53036 => "grows long.",
            53037 => "Stay close. We have\nthis!",
            53038 => "A good honest strike!",
            53039 => "Saint Michael, guard\nour flank!",
            53040 => "Big foot coming down!",
            53041 => "1",
            53042 => "3",
            53043 => "5",
            53044 => "6",
            53045 => "9",
            53046 => "HP",
            53047 => "18/20",
            53048 => "3/20",
            53049 => "120/150",
            53050 => "32/50",
            53051 => "COUR",
            53052 => "TEMP",
            53053 => "AP",
            53054 => "PLEDGE AVAILABLE",
            53055 => "12",
            53056 => "123",
            53057 => "2 Aegis",
            53058 => "1 Armor",
            53059 => "10 Power",
            53060 => "25 Thorns",
            _ => null,
        };
        if (text is not null) return true;

        int fixtureIndex = id.Value - TextContentIds.SnapshotBase;
        if ((uint)fixtureIndex < (uint)fixtures.Length)
        {
            text = fixtures[fixtureIndex].Id;
            return true;
        }
        return false;
    }

    public bool TryResolve(TextStyleId id, out TextStyleDefinition style)
    {
        style = id.Value switch
        {
            1 => new TextStyleDefinition(new FontAssetId(2), 0.42f, 0f),
            2 => new TextStyleDefinition(new FontAssetId(2), 0.26f, 0f),
            3 => new TextStyleDefinition(new FontAssetId(1), 0.11f, 0f),
            4 => new TextStyleDefinition(new FontAssetId(1), 0.13f, 0f),
            101 => new TextStyleDefinition(new FontAssetId(1), 1f, 0f),
            102 => new TextStyleDefinition(new FontAssetId(2), 1f, 0f),
            103 => new TextStyleDefinition(new FontAssetId(3), 1f, 0f),
            _ => default,
        };
        return !style.Font.IsNull;
    }
}
