namespace EnGlam;

/// <summary>Alle Versionen mit Datum des Release-Tags, neueste zuerst.</summary>
public static class EnGlamChangelog
{
    public static readonly IReadOnlyList<ChangelogEntry> Entries =
    [
        new("0.2.3.1", "2026-10-28",
        [
            new("The description in the About tab now wraps correctly.",
                "Die Beschreibung im About-Tab bricht jetzt richtig um."),
        ]),
        new("0.2.3.0", "2026-10-28",
        [
            new("A welcome window appears after you install EnGlam, with the first steps and the key settings.",
                "Nach der Installation erscheint ein Willkommensfenster mit den ersten Schritten und den wichtigsten Einstellungen."),
            new("A changelog window appears after every update and lists all versions; new ones are marked.",
                "Nach jedem Update erscheint ein Changelog-Fenster mit allen Versionen; neue sind markiert."),
            new("Both windows can be opened again from the About tab.",
                "Beide Fenster lassen sich im About-Tab erneut öffnen."),
            new("The language switch moved from the title bar into the header band, away from the window buttons.",
                "Der Sprachschalter sitzt jetzt im Kopfband statt in der Titelleiste, weg von den Fensterknöpfen."),
        ]),
        new("0.2.2.0", "2026-10-28",
        [
            new("EnGlam is available in German and English. A language button in the title bar switches instantly.",
                "EnGlam gibt es auf Deutsch und Englisch. Ein Sprach-Knopf in der Titelleiste wechselt sofort."),
            new("It starts in Dalamud's language until you choose one yourself.",
                "Es startet in der Sprache von Dalamud, bis du selbst eine wählst."),
            new("The tab bar switches to icons when the window is too narrow.",
                "Die Tab-Leiste schaltet auf Icons um, wenn das Fenster zu schmal ist."),
        ]),
        new("0.2.1.0", "2026-10-28",
        [
            new("New About tab with description, commands and license.",
                "Neuer About-Tab mit Beschreibung, Befehlen und Lizenz."),
            new("Plugins from Dalamud's main repository install without adding a custom repository.",
                "Plugins aus Dalamuds Haupt-Repository werden installiert, ohne ein eigenes Repository einzutragen."),
            new("A Diagnostics switch in Settings shows the draw time of the window.",
                "Ein Diagnose-Schalter in den Einstellungen zeigt die Zeichenzeit des Fensters."),
            new("Nothing is drawn during loading screens.",
                "In Ladebildschirmen wird nichts gezeichnet."),
        ]),
        new("0.2.0.0", "2026-10-27",
        [
            new("Search finds items by their English name as well.",
                "Die Suche findet Items auch über den englischen Namen."),
            new("Import a glamour from copied Eorzea Collection page source, a list of names or an inspected character, with a preview.",
                "Importiere ein Glamour aus kopiertem Quelltext einer Eorzea-Collection-Seite, aus einer Namensliste oder aus einem inspizierten Charakter, mit Vorschau."),
            new("Items without an AutoDuty path are marked \"farm yourself\".",
                "Items ohne AutoDuty-Pfad sind mit \"selbst farmen\" markiert."),
            new("Runs you do by hand are counted in the history and finished items are ticked off.",
                "Runs, die du von Hand machst, zählen im Verlauf mit, und fertige Items werden abgehakt."),
        ]),
        new("0.1.0.0", "2026-10-27",
        [
            new("Pick glamour items and EnGlam has AutoDuty run the duties that drop them, one run at a time, until each item is in your inventory.",
                "Wähl Glamour-Items, und EnGlam lässt AutoDuty die Duties laufen, in denen sie droppen, Run für Run, bis jedes Item in deinem Inventar liegt."),
            new("Rolls Need on the items you are after and can Pass on everything else.",
                "Würfelt Need auf die gesuchten Items und kann auf alles andere passen."),
            new("Farm list with favorites, a history tab and a Dependencies tab that installs missing plugins with one click.",
                "Farmliste mit Favoriten, ein Verlauf-Tab und ein Abhängigkeiten-Tab, der fehlende Plugins mit einem Klick installiert."),
        ]),
    ];
}
