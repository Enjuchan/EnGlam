using EnGlam.Core.Duties;

namespace EnGlam.Core.Tests;

public class DutyModeRulesTests
{
    private static DutyInfo Duty(
        uint contentType = 2,
        uint territory = 9999,
        uint ex = 0,
        uint dawnRow = 0,
        int participable = 0,
        int trustOrdinal = -1) =>
        new(1, territory, 1, "Test", contentType, ex, 15, dawnRow, participable, trustOrdinal);

    [Fact]
    public void Dungeon_IsRegular()
    {
        Assert.Equal(DutyMode.Regular, DutyModeRules.ModesFor(Duty()));
    }

    [Fact]
    public void UnknownContentType_HasNoMode()
    {
        Assert.Equal(DutyMode.None, DutyModeRules.ModesFor(Duty(contentType: 9)));
    }

    [Fact]
    public void SquadronTerritory_AddsSquadron()
    {
        Assert.Equal(DutyMode.Regular | DutyMode.Squadron, DutyModeRules.ModesFor(Duty(territory: 1039)));
    }

    [Fact]
    public void DawnContentWithSeveralParticipants_AddsSupport()
    {
        var modes = DutyModeRules.ModesFor(Duty(ex: 0, dawnRow: 201, participable: 2));

        Assert.True(modes.HasFlag(DutyMode.Support));
    }

    [Fact]
    public void DawnContentWithOneParticipant_IsNoSupport()
    {
        var modes = DutyModeRules.ModesFor(Duty(ex: 0, dawnRow: 201, participable: 1));

        Assert.False(modes.HasFlag(DutyMode.Support));
    }

    [Fact]
    public void DawnRowBelowExpansionStart_IsNoSupport()
    {
        // ARR beginnt in DawnContent bei Zeile 200; darunter liegt kein ARR-Inhalt.
        var modes = DutyModeRules.ModesFor(Duty(ex: 0, dawnRow: 150, participable: 2));

        Assert.False(modes.HasFlag(DutyMode.Support));
    }

    [Theory]
    [InlineData(3u, 0, true)]
    [InlineData(4u, 10, false)]
    [InlineData(4u, 11, true)]
    [InlineData(5u, 22, true)]
    [InlineData(2u, 5, false)]
    public void Trust_DependsOnOrdinalAndExpansion(uint ex, int ordinal, bool expected)
    {
        var modes = DutyModeRules.ModesFor(Duty(ex: ex, trustOrdinal: ordinal));

        Assert.Equal(expected, modes.HasFlag(DutyMode.Trust));
    }

    [Theory]
    [InlineData(2u, Party.DutyFinder, DutyMode.Regular)]
    [InlineData(4u, Party.DutyFinder, DutyMode.Trial)]
    [InlineData(5u, Party.DutyFinder, DutyMode.Raid)]
    [InlineData(4u, Party.Trust, DutyMode.None)]
    public void EffectiveMode_FollowsContentForDutyFinder(uint contentType, Party party, DutyMode expected)
    {
        Assert.Equal(expected, DutyModeRules.EffectiveMode(Duty(contentType: contentType), party));
    }

    [Fact]
    public void EffectiveMode_SquadronDungeon()
    {
        Assert.Equal(DutyMode.Squadron, DutyModeRules.EffectiveMode(Duty(territory: 1039), Party.Squadron));
    }

    [Fact]
    public void VariantOnlyForKnownTerritories()
    {
        Assert.Equal(DutyMode.Variant, DutyModeRules.ModesFor(Duty(contentType: 30, territory: 1069)));
        Assert.Equal(DutyMode.None, DutyModeRules.ModesFor(Duty(contentType: 30, territory: 5000)));
    }
}
