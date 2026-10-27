namespace EnGlam.Core.Settings;

/// <summary>Ursprünglicher Wert einer Einstellung in einem fremden Plugin.</summary>
public sealed record SavedSetting(string Owner, string Key, string Value);

/// <summary>
/// Ändert Einstellungen fremder Plugins so, dass sie sich zurücksetzen lassen.
/// Der ursprüngliche Wert wird vor der ersten Änderung gespeichert und bleibt gespeichert,
/// bis das Zurückschreiben gelungen ist; so überlebt er auch einen Absturz.
/// </summary>
/// <param name="store">Liste aus der Plugin-Konfiguration.</param>
/// <param name="save">Speichert die Konfiguration.</param>
public sealed class SettingsGuard(List<SavedSetting> store, Action save)
{
    public IReadOnlyList<SavedSetting> Pending => store;

    /// <summary>Merkt den aktuellen Wert, falls noch nicht gemerkt, und schreibt dann den neuen.</summary>
    public void Set(string owner, string key, string value, Func<string> readCurrent, Action<string> write)
    {
        if (!store.Any(s => s.Owner == owner && s.Key == key))
        {
            store.Add(new SavedSetting(owner, key, readCurrent()));
            save();
        }

        write(value);
    }

    /// <summary>
    /// Schreibt alle gemerkten Werte in umgekehrter Reihenfolge zurück. Einträge, deren Zurückschreiben
    /// scheitert, bleiben für den nächsten Versuch stehen. Gibt die Anzahl der zurückgesetzten Werte zurück.
    /// </summary>
    /// <param name="write">Schreibt einen Wert zurück; false, wenn das nicht ging.</param>
    public int RestoreAll(Func<SavedSetting, bool> write)
    {
        var restored = 0;

        for (var i = store.Count - 1; i >= 0; i--)
        {
            if (!write(store[i]))
                continue;

            store.RemoveAt(i);
            restored++;
        }

        if (restored > 0)
            save();

        return restored;
    }
}
