using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Textures;
using Dalamud.Plugin.Services;

namespace EnGlam.Ui;

/// <summary>Bausteine im Stil der En-Plugins, gezeichnet mit ImGui.</summary>
public static class Widgets
{
    private static ITextureProvider? textures;

    public static void Init(ITextureProvider textureProvider) => textures = textureProvider;

    /// <summary>Überschrift mit kurzer Linie davor und Linie bis zum Rand.</summary>
    public static void SectionHeader(string label)
    {
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var text = label.ToUpperInvariant();
        var size = ImGui.CalcTextSize(text);
        var y = pos.Y + size.Y / 2;
        var line = Theme.U32(Theme.Border);

        draw.AddLine(new Vector2(pos.X, y), new Vector2(pos.X + 14, y), line, 1f);
        draw.AddText(new Vector2(pos.X + 22, pos.Y), Theme.U32(Theme.AccentLight), text);
        draw.AddLine(new Vector2(pos.X + 30 + size.X, y), new Vector2(pos.X + width, y), line, 1f);
        ImGui.Dummy(new Vector2(width, size.Y + 4));
    }

    /// <summary>Abgerundete Markierung mit Rahmen, optional mit Punkt davor.</summary>
    public static void Pill(string text, Vector4 border, Vector4 color, Vector4? dot = null)
    {
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var textSize = ImGui.CalcTextSize(text);
        var dotSpace = dot is null ? 0f : 11f;
        var size = new Vector2(textSize.X + 18 + dotSpace, textSize.Y + 6);

        draw.AddRect(pos, pos + size, Theme.U32(border), size.Y / 2, ImDrawFlags.None, 1f);
        if (dot is { } d)
            draw.AddCircleFilled(new Vector2(pos.X + 12, pos.Y + size.Y / 2), 3f, Theme.U32(d));
        draw.AddText(new Vector2(pos.X + 9 + dotSpace, pos.Y + 3), Theme.U32(color), text);

        ImGui.Dummy(size);
    }

    /// <summary>Statusmarkierung: grüner, gelber oder roter Punkt mit Namen.</summary>
    public static void StatusPill(string text, Vector4 dot) => Pill(text, Theme.Border, Theme.Text, dot);

    /// <summary>Breite einer Markierung, zum rechtsbündigen Ausrichten.</summary>
    public static float PillWidth(string text, bool withDot) => ImGui.CalcTextSize(text).X + 18 + (withDot ? 11f : 0f);

    /// <summary>Schalter statt Checkbox. Gibt zurück, ob sich der Wert geändert hat.</summary>
    public static bool Toggle(string id, ref bool value)
    {
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(30, 16);
        var clicked = ImGui.InvisibleButton(id, size);
        if (clicked)
            value = !value;

        var hovered = ImGui.IsItemHovered();
        var track = value ? Theme.Accent : (hovered ? Theme.Border : Theme.Hex(0x3A2B37));
        draw.AddRectFilled(pos, pos + size, Theme.U32(track), size.Y / 2);

        var knobX = value ? pos.X + size.X - size.Y / 2 : pos.X + size.Y / 2;
        draw.AddCircleFilled(new Vector2(knobX, pos.Y + size.Y / 2), 5.5f, Theme.U32(value ? Theme.OnAccent : Theme.Muted));
        return clicked;
    }

    /// <summary>Zeile mit Beschriftung links und Schalter rechts.</summary>
    public static bool ToggleRow(string label, ref bool value, float width, string? tooltip = null)
    {
        var start = ImGui.GetCursorScreenPos();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(label);
        if (tooltip is not null && ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);

        // Bildschirmkoordinaten statt SameLine-Versatz: In Tabellenspalten bezieht sich SameLine auf die Spalte,
        // GetCursorPosX aber auf das Fenster, und beides zusammen schob den Schalter aus dem sichtbaren Bereich.
        ImGui.SetCursorScreenPos(new Vector2(start.X + width - 30, start.Y + (ImGui.GetFrameHeight() - 16) / 2));
        return Toggle($"##{label}", ref value);
    }

    /// <summary>Umschaltleiste mit gleich breiten Feldern. Gibt zurück, ob sich die Auswahl geändert hat.</summary>
    public static bool Segmented<T>(string id, IReadOnlyList<T> values, Func<T, string> label, ref T selected, float width)
        where T : struct, Enum
    {
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var height = ImGui.GetFrameHeight();
        var cell = width / values.Count;
        var changed = false;

        draw.AddRect(pos, pos + new Vector2(width, height), Theme.U32(Theme.Border), Theme.Rounding, ImDrawFlags.None, 1f);

        for (var i = 0; i < values.Count; i++)
        {
            var min = pos + new Vector2(cell * i, 0);
            var max = min + new Vector2(cell, height);
            var isSelected = EqualityComparer<T>.Default.Equals(values[i], selected);

            ImGui.SetCursorScreenPos(min);
            if (ImGui.InvisibleButton($"{id}{i}", new Vector2(cell, height)) && !isSelected)
            {
                selected = values[i];
                changed = true;
                isSelected = true;
            }

            var hovered = ImGui.IsItemHovered();
            if (isSelected)
            {
                var flags = i == 0 ? ImDrawFlags.RoundCornersLeft : i == values.Count - 1 ? ImDrawFlags.RoundCornersRight : ImDrawFlags.RoundCornersNone;
                draw.AddRectFilled(min, max, Theme.U32(Theme.Accent), Theme.Rounding, flags);
            }
            else if (hovered)
            {
                draw.AddRectFilled(min + Vector2.One, max - Vector2.One, Theme.U32(Theme.Hover), Theme.Rounding);
            }

            var text = label(values[i]);
            var textSize = ImGui.CalcTextSize(text);
            draw.AddText(min + new Vector2((cell - textSize.X) / 2, (height - textSize.Y) / 2),
                Theme.U32(isSelected ? Theme.OnAccent : Theme.Muted), text);
        }

        ImGui.SetCursorScreenPos(pos);
        ImGui.Dummy(new Vector2(width, height));
        return changed;
    }

    /// <summary>
    /// Tabs als Pillen mit Icon im Stil der Status-Markierungen: der aktive Tab gefüllt in Akzentfarbe,
    /// die anderen mit Rahmen. Gibt zurück, ob ein anderer Tab gewählt wurde.
    /// </summary>
    public static bool PillTabs(string id, IReadOnlyList<(FontAwesomeIcon Icon, string Label)> tabs, ref int selected)
    {
        var draw = ImGui.GetWindowDrawList();
        var start = ImGui.GetCursorScreenPos();
        var height = ImGui.GetFrameHeight();
        var x = start.X;
        var changed = false;

        for (var i = 0; i < tabs.Count; i++)
        {
            var (icon, label) = tabs[i];
            var iconText = icon.ToIconString();

            ImGui.PushFont(UiBuilder.IconFont);
            var iconSize = ImGui.CalcTextSize(iconText);
            ImGui.PopFont();

            var textSize = ImGui.CalcTextSize(label);
            var size = new Vector2(iconSize.X + textSize.X + 32, height);
            var min = new Vector2(x, start.Y);
            var max = min + size;

            ImGui.SetCursorScreenPos(min);
            if (ImGui.InvisibleButton($"{id}{i}", size) && selected != i)
            {
                selected = i;
                changed = true;
            }

            var active = selected == i;
            var hovered = ImGui.IsItemHovered();

            if (active)
                draw.AddRectFilled(min, max, Theme.U32(Theme.Accent), height / 2);
            else
            {
                if (hovered)
                    draw.AddRectFilled(min, max, Theme.U32(Theme.Hover), height / 2);
                draw.AddRect(min, max, Theme.U32(hovered ? Theme.AccentLight : Theme.Border), height / 2, ImDrawFlags.None, 1f);
            }

            var fg = Theme.U32(active ? Theme.OnAccent : hovered ? Theme.Text : Theme.Muted);
            ImGui.PushFont(UiBuilder.IconFont);
            draw.AddText(new Vector2(min.X + 12, min.Y + (height - iconSize.Y) / 2), active ? fg : Theme.U32(Theme.AccentLight), iconText);
            ImGui.PopFont();
            draw.AddText(new Vector2(min.X + 18 + iconSize.X, min.Y + (height - textSize.Y) / 2), fg, label);

            x += size.X + 8;
        }

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(new Vector2(x - start.X, height));
        return changed;
    }

    /// <summary>Gefüllter Akzent-Knopf mit Icon; pro Ansicht nur einer.</summary>
    public static bool PrimaryButton(string label, FontAwesomeIcon icon, bool enabled = true)
    {
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var iconText = icon.ToIconString();

        ImGui.PushFont(UiBuilder.IconFont);
        var iconSize = ImGui.CalcTextSize(iconText);
        ImGui.PopFont();

        var textSize = ImGui.CalcTextSize(label);
        var size = new Vector2(iconSize.X + textSize.X + 30, ImGui.GetFrameHeight());

        var clicked = ImGui.InvisibleButton($"##{label}", size) && enabled;
        var hovered = ImGui.IsItemHovered() && enabled;

        var fill = !enabled ? Theme.Hover : hovered ? Theme.AccentLight : Theme.Accent;
        var fg = enabled ? Theme.OnAccent : Theme.Muted;
        draw.AddRectFilled(pos, pos + size, Theme.U32(fill), Theme.Rounding);

        var y = pos.Y + (size.Y - textSize.Y) / 2;
        ImGui.PushFont(UiBuilder.IconFont);
        draw.AddText(new Vector2(pos.X + 12, pos.Y + (size.Y - iconSize.Y) / 2), Theme.U32(fg), iconText);
        ImGui.PopFont();
        draw.AddText(new Vector2(pos.X + 18 + iconSize.X, y), Theme.U32(fg), label);

        return clicked;
    }

    /// <summary>Breite eines <see cref="PrimaryButton"/>, zum rechtsbündigen Ausrichten.</summary>
    public static float PrimaryButtonWidth(string label, FontAwesomeIcon icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var iconWidth = ImGui.CalcTextSize(icon.ToIconString()).X;
        ImGui.PopFont();
        return iconWidth + ImGui.CalcTextSize(label).X + 30;
    }

    /// <summary>Kleiner Knopf nur mit Icon, ohne Hintergrund.</summary>
    public static bool IconButton(string id, FontAwesomeIcon icon, string? tooltip = null)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0, 0, 0, 0));
        var clicked = ImGui.Button($"{icon.ToIconString()}##{id}");
        ImGui.PopStyleColor();
        ImGui.PopFont();

        if (tooltip is not null && ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);

        return clicked;
    }

    public static void Icon(FontAwesomeIcon icon, Vector4 color)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(color, icon.ToIconString());
        ImGui.PopFont();
    }

    /// <summary>Spiel-Icon mit abgerundetem Rahmen; ohne Icon ein leeres Feld.</summary>
    public static void GameIcon(uint iconId, float size)
    {
        GameIconAt(ImGui.GetCursorScreenPos(), iconId, size);
        ImGui.Dummy(new Vector2(size, size));
    }

    /// <summary>Wie <see cref="GameIcon"/>, aber an fester Position, ohne den Cursor zu bewegen.</summary>
    public static void GameIconAt(Vector2 pos, uint iconId, float size)
    {
        var draw = ImGui.GetWindowDrawList();
        var max = pos + new Vector2(size, size);

        draw.AddRectFilled(pos, max, Theme.U32(Theme.Hover), 6f);

        if (iconId != 0 && textures is not null)
        {
            var wrap = textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrEmpty();
            draw.AddImageRounded(wrap.Handle, pos + Vector2.One, max - Vector2.One, Vector2.Zero, Vector2.One, 0xFFFFFFFF, 5f);
        }

        draw.AddRect(pos, max, Theme.U32(Theme.Border), 6f, ImDrawFlags.None, 1f);
    }

    /// <summary>Dünner Fortschrittsbalken in Akzentfarbe.</summary>
    public static void ProgressBar(float fraction, float width)
    {
        var draw = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var size = new Vector2(width, 5);

        draw.AddRectFilled(pos, pos + size, Theme.U32(Theme.Hover), 3f);
        var filled = Math.Clamp(fraction, 0f, 1f) * width;
        if (filled > 0)
            draw.AddRectFilled(pos, pos + new Vector2(filled, size.Y), Theme.U32(Theme.Accent), 3f);

        ImGui.Dummy(size);
    }

    /// <summary>
    /// Karte mit Hintergrund und Rahmen um beliebigen Inhalt. Der Hintergrund wird nach dem Inhalt in eine
    /// tiefere Ebene gezeichnet, weil die Höhe erst dann bekannt ist. Gibt zurück, ob die Karte angeklickt wurde.
    /// </summary>
    /// <param name="content">Zeichnet den Inhalt; bekommt die nutzbare Innenbreite.</param>
    public static bool Card(string id, Action<float> content, bool selected = false, float padding = 12f)
    {
        var draw = ImGui.GetWindowDrawList();
        var width = ImGui.GetContentRegionAvail().X;
        var start = ImGui.GetCursorScreenPos();

        draw.ChannelsSplit(2);
        draw.ChannelsSetCurrent(1);

        ImGui.SetCursorScreenPos(start + new Vector2(padding, padding));
        ImGui.BeginGroup();
        ImGui.PushID(id);
        ImGui.PushItemWidth(width - padding * 2);
        content(width - padding * 2);
        ImGui.PopItemWidth();
        ImGui.PopID();
        ImGui.EndGroup();

        var end = new Vector2(start.X + width, ImGui.GetItemRectMax().Y + padding);

        draw.ChannelsSetCurrent(0);
        var hovered = ImGui.IsMouseHoveringRect(start, end);
        draw.AddRectFilled(start, end, Theme.U32(hovered || selected ? Theme.Hex(0x221A25) : Theme.Card), Theme.CardRounding);
        draw.AddRect(start, end, Theme.U32(selected ? Theme.Accent : Theme.CardBorder), Theme.CardRounding, ImDrawFlags.None, 1f);
        draw.ChannelsMerge();

        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(end - start);

        return hovered && ImGui.IsMouseClicked(ImGuiMouseButton.Left) && !ImGui.IsAnyItemHovered();
    }
}
