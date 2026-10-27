using EnGlam.Core.Import;

namespace EnGlam.Core.Tests;

public class GlamourImportTests
{
    private static readonly ImportItem Helm = new(100, "Edenschwur-Helm", "Edengrace Helm of Maiming", 40332);
    private static readonly ImportItem Coat = new(200, "Griffith-Mantel", "Hawk's Coat", 40500);
    private static readonly ImportItem TwinA = new(300, "Zwilling", "Twin Ring", 40600);
    private static readonly ImportItem TwinB = new(301, "Zwilling", "Twin Ring", 40601);
    private static readonly ImportItem SameIconA = new(400, "Gleich A", "Same Icon A", 40700);
    private static readonly ImportItem SameIconB = new(401, "Gleich B", "Same Icon B", 40700);

    private static GlamourImport Importer(Func<uint, bool>? preferred = null) =>
        new([Helm, Coat, TwinA, TwinB, SameIconA, SameIconB], preferred);

    [Fact]
    public void Parse_HeaderFormat_ReadsIconAndName()
    {
        var entries = GlamourImport.Parse("EnGlam import 1\r\n40332\tEdengrace Helm of Maiming\r\n40500\tHawk's Coat\r\n");

        Assert.Equal([new ImportEntry("Edengrace Helm of Maiming", 40332), new ImportEntry("Hawk's Coat", 40500)], entries);
    }

    [Fact]
    public void Parse_WithoutHeader_TreatsEveryLineAsName()
    {
        var entries = GlamourImport.Parse("Edengrace Helm of Maiming\n\n  Hawk's Coat  \n");

        Assert.Equal([new ImportEntry("Edengrace Helm of Maiming", 0), new ImportEntry("Hawk's Coat", 0)], entries);
    }

    [Fact]
    public void Parse_HeaderLineWithoutIcon_KeepsName()
    {
        var entries = GlamourImport.Parse("EnGlam import 1\nHawk's Coat");

        Assert.Equal([new ImportEntry("Hawk's Coat", 0)], entries);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n \r\n")]
    [InlineData("EnGlam import 1")]
    public void Parse_EmptyInput_GivesNoEntries(string? text)
    {
        Assert.Empty(GlamourImport.Parse(text));
    }

    [Fact]
    public void Match_EnglishName_IgnoresCase()
    {
        Assert.Equal(100u, Importer().Match(new ImportEntry("edengrace helm of MAIMING", 0)));
    }

    [Fact]
    public void Match_NameInGameLanguage_IsFound()
    {
        Assert.Equal(200u, Importer().Match(new ImportEntry("Griffith-Mantel", 0)));
    }

    [Fact]
    public void Match_TypographicApostrophe_IsFound()
    {
        Assert.Equal(200u, Importer().Match(new ImportEntry("Hawk’s Coat", 0)));
    }

    [Fact]
    public void Match_SameName_IconDecides()
    {
        Assert.Equal(301u, Importer().Match(new ImportEntry("Twin Ring", 40601)));
    }

    [Fact]
    public void Match_SameNameWithoutIcon_PrefersPreferredItem()
    {
        Assert.Equal(301u, Importer(id => id == 301).Match(new ImportEntry("Twin Ring", 0)));
    }

    [Fact]
    public void Match_SameNameWithoutPreference_TakesLowestId()
    {
        Assert.Equal(300u, Importer().Match(new ImportEntry("Twin Ring", 0)));
    }

    [Fact]
    public void Match_UnknownNameWithUniqueIcon_UsesIcon()
    {
        Assert.Equal(100u, Importer().Match(new ImportEntry("Renamed Helm", 40332)));
    }

    [Fact]
    public void Match_UnknownNameWithSharedIcon_IsNotFound()
    {
        Assert.Null(Importer().Match(new ImportEntry("Renamed", 40700)));
    }

    [Fact]
    public void Match_UnknownNameWithoutIcon_IsNotFound()
    {
        Assert.Null(Importer().Match(new ImportEntry("Does Not Exist", 0)));
    }

    [Fact]
    public void MatchAll_KeepsNotFoundAndDropsDuplicates()
    {
        var results = Importer().MatchAll(
        [
            new ImportEntry("Edengrace Helm of Maiming", 40332),
            new ImportEntry("Does Not Exist", 0),
            new ImportEntry("Edenschwur-Helm", 0),
        ]);

        Assert.Equal([100u, null], results.Select(r => r.ItemId));
    }

    private const string PageSource = """
        <div class="gear-item">
          <a href="https://eu.finalfantasyxiv.com/lodestone/playguide/db/item/abc123/">
            <img class="gear-icon-image image" src="https://icons.eorzeacollection.com/040000/040332.png" alt="Edengrace Helm of Maiming">
          </a>
          <img alt="Hawk&#039;s Coat" src="https://icons.eorzeacollection.com/040000/040500.png" class="image gear-icon-image">
          <img class="avatar" src="https://example.com/user.png" alt="Some User">
          <img class="gear-icon-image-large" src="https://icons.eorzeacollection.com/040000/040999.png" alt="Not An Item">
          <img class="gear-icon-image" alt="Twin Ring">
        </div>
        """;

    [Fact]
    public void Parse_PageSource_ReadsGearImages()
    {
        var entries = GlamourImport.Parse(PageSource);

        Assert.Equal(
        [
            new ImportEntry("Edengrace Helm of Maiming", 40332),
            new ImportEntry("Hawk's Coat", 40500),
            new ImportEntry("Twin Ring", 0),
        ], entries);
    }

    [Fact]
    public void Parse_PageSourceWithoutGearImages_FallsBackToLines()
    {
        var entries = GlamourImport.Parse("<html><img class=\"avatar\" alt=\"x\"></html>");

        Assert.Single(entries);
    }

    [Fact]
    public void PageSource_MatchesItems()
    {
        var results = Importer().MatchAll(GlamourImport.Parse(PageSource));

        Assert.Equal([100u, 200u, 300u], results.Select(r => r.ItemId));
    }
}
