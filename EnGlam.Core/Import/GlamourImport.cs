using System.Net;
using System.Text.RegularExpressions;

namespace EnGlam.Core.Import;

/// <summary>Ein Eintrag aus einer Importquelle.</summary>
/// <param name="Icon">Icon-Nummer aus dem Spiel, 0 wenn unbekannt.</param>
public readonly record struct ImportEntry(string Name, uint Icon);

/// <summary>Ein anlegbares Item, gegen das zugeordnet wird.</summary>
public readonly record struct ImportItem(uint Id, string Name, string EnglishName, uint Icon);

/// <param name="ItemId">Zugeordnetes Item, null wenn keines gefunden wurde.</param>
public readonly record struct ImportResult(ImportEntry Entry, uint? ItemId);

/// <summary>
/// Liest Glamour-Sets aus Text und ordnet die Einträge Items zu. Erkannt werden der kopierte Quelltext einer
/// Glamour-Seite von Eorzea Collection, das Format mit Kopfzeile und eine einfache Liste mit einem Namen pro Zeile.
/// </summary>
public sealed class GlamourImport
{
    public const string Header = "EnGlam import 1";

    private readonly Dictionary<string, List<ImportItem>> byEnglishName = new();
    private readonly Dictionary<string, List<ImportItem>> byName = new();
    private readonly Dictionary<uint, List<ImportItem>> byIcon = new();
    private readonly Func<uint, bool> preferred;

    /// <param name="preferred">Entscheidet zwischen gleichnamigen Items mit gleichem Icon, etwa „hat eine Drop-Quelle“.</param>
    public GlamourImport(IEnumerable<ImportItem> items, Func<uint, bool>? preferred = null)
    {
        this.preferred = preferred ?? (_ => false);

        foreach (var item in items)
        {
            Add(byEnglishName, Key(item.EnglishName), item);
            Add(byName, Key(item.Name), item);
            if (item.Icon != 0)
                Add(byIcon, item.Icon, item);
        }
    }

    public static IReadOnlyList<ImportEntry> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];

        if (text.Contains(GearIconClass, StringComparison.Ordinal))
            return ParsePageSource(text);

        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        if (lines.Count == 0)
            return [];

        if (!string.Equals(lines[0], Header, StringComparison.OrdinalIgnoreCase))
            return [.. lines.Select(l => new ImportEntry(l, 0))];

        return [.. lines.Skip(1).Select(ParseLine).Where(e => e.Name.Length > 0)];
    }

    /// <summary>
    /// Quelltext einer Glamour-Seite von Eorzea Collection: Jedes Item ist ein Bild der Klasse
    /// <c>gear-icon-image</c>, <c>alt</c> trägt den englischen Namen, <c>src</c> endet auf die Icon-Nummer.
    /// </summary>
    private static IReadOnlyList<ImportEntry> ParsePageSource(string html)
    {
        var entries = new List<ImportEntry>();
        foreach (System.Text.RegularExpressions.Match tag in GearImageTag.Matches(html))
        {
            if (!ClassAttribute.Match(tag.Value).Groups[1].Value.Split(' ').Contains(GearIconClass))
                continue;

            var name = WebUtility.HtmlDecode(AltAttribute.Match(tag.Value).Groups[1].Value).Trim();
            if (name.Length == 0)
                continue;

            var iconMatch = IconInSrc.Match(tag.Value);
            var icon = iconMatch.Success && uint.TryParse(iconMatch.Groups[1].Value, out var number) ? number : 0;
            entries.Add(new ImportEntry(name, icon));
        }

        return entries;
    }

    private const string GearIconClass = "gear-icon-image";
    private static readonly Regex GearImageTag = new(@"<img\b[^>]*gear-icon-image[^>]*>", RegexOptions.IgnoreCase);
    private static readonly Regex ClassAttribute = new(@"\bclass\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
    private static readonly Regex AltAttribute = new(@"\balt\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
    private static readonly Regex IconInSrc = new(@"\bsrc\s*=\s*""[^""]*?(\d+)\.png""", RegexOptions.IgnoreCase);

    private static ImportEntry ParseLine(string line)
    {
        var tab = line.IndexOf('\t');
        if (tab > 0 && uint.TryParse(line[..tab], out var icon))
            return new ImportEntry(line[(tab + 1)..].Trim(), icon);

        return new ImportEntry(line, 0);
    }

    /// <summary>Ordnet alle Einträge zu; ein Item, das mehrfach vorkommt, erscheint nur beim ersten Mal.</summary>
    public IReadOnlyList<ImportResult> MatchAll(IEnumerable<ImportEntry> entries)
    {
        var seen = new HashSet<uint>();
        var results = new List<ImportResult>();

        foreach (var entry in entries)
        {
            var id = Match(entry);
            if (id is { } found && !seen.Add(found))
                continue;

            results.Add(new ImportResult(entry, id));
        }

        return results;
    }

    public uint? Match(ImportEntry entry)
    {
        var key = Key(entry.Name);
        var candidates = byEnglishName.GetValueOrDefault(key) ?? byName.GetValueOrDefault(key);

        if (candidates is null)
            return entry.Icon != 0 && byIcon.TryGetValue(entry.Icon, out var sameIcon) && sameIcon.Count == 1
                ? sameIcon[0].Id
                : null;

        if (candidates.Count == 1)
            return candidates[0].Id;

        var withIcon = entry.Icon == 0 ? candidates : candidates.Where(c => c.Icon == entry.Icon).ToList();
        if (withIcon.Count == 0)
            withIcon = candidates;

        return withIcon
            .OrderByDescending(c => preferred(c.Id))
            .ThenBy(c => c.Id)
            .First().Id;
    }

    // Apostrophe werden vereinheitlicht, weil Webseiten und Spieldaten unterschiedliche verwenden können.
    private static string Key(string name) => name.Trim().Replace('’', '\'').ToLowerInvariant();

    private static void Add<TKey>(Dictionary<TKey, List<ImportItem>> map, TKey key, ImportItem item) where TKey : notnull
    {
        if (key is string { Length: 0 })
            return;

        if (!map.TryGetValue(key, out var list))
            map[key] = list = [];

        list.Add(item);
    }
}
