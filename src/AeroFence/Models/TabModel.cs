using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AeroFence.Models;

/// <summary>
/// Layout placement mode for items within a tab.
/// </summary>
public enum TabLayoutMode
{
    FreeForm = 0,
    AutoGrid = 1
}

/// <summary>
/// Represents a tab container inside a fence that holds items.
/// Observable so tab name, icon glyph, and edit states update the UI in real-time.
/// </summary>
public partial class TabModel : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _name = "New Tab";

    [ObservableProperty]
    private string? _iconGlyph = "📁";

    public TabLayoutMode LayoutMode { get; set; } = TabLayoutMode.FreeForm;
    public List<ItemModel> Items { get; set; } = new();

    // Folder Portal support on individual tabs
    [ObservableProperty]
    private bool _isFolderPortal = false;

    [ObservableProperty]
    private string? _folderPath;

    [JsonIgnore]
    [ObservableProperty]
    private bool _isEditing = false;

    /// <summary>
    /// Intelligently matches keywords in tab title to appropriate Fluent emoji glyphs.
    /// </summary>
    public static string DetectSmartIcon(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "📁";
        string lower = name.ToLowerInvariant();

        // Games / Gaming
        if (lower.Contains("game") || lower.Contains("play") || lower.Contains("steam") || lower.Contains("rpg") || lower.Contains("fps") || lower.Contains("gta"))
            return "🎮";

        // Programming / Code / Development
        if (lower.Contains("code") || lower.Contains("coding") || lower.Contains("dev") || lower.Contains("program") || lower.Contains("script") || lower.Contains("git") || lower.Contains("repo") || lower.Contains("terminal") || lower.Contains("cmd") || lower.Contains("vs"))
            return "💻";

        // Tools & Utilities
        if (lower.Contains("tool") || lower.Contains("util") || lower.Contains("setting") || lower.Contains("tweak") || lower.Contains("fix") || lower.Contains("optimi"))
            return "🛠️";

        // Creative / Design / Graphics
        if (lower.Contains("design") || lower.Contains("art") || lower.Contains("draw") || lower.Contains("photo") || lower.Contains("figma") || lower.Contains("adobe") || lower.Contains("ui") || lower.Contains("ux") || lower.Contains("canva"))
            return "🎨";

        // Music & Sound
        if (lower.Contains("music") || lower.Contains("audio") || lower.Contains("song") || lower.Contains("sound") || lower.Contains("spotify") || lower.Contains("lagu"))
            return "🎵";

        // Video / Movies / Streaming
        if (lower.Contains("video") || lower.Contains("movie") || lower.Contains("film") || lower.Contains("stream") || lower.Contains("youtube") || lower.Contains("media") || lower.Contains("netflix"))
            return "🎬";

        // Web / Internet / Browsers
        if (lower.Contains("web") || lower.Contains("browse") || lower.Contains("internet") || lower.Contains("net") || lower.Contains("chrome") || lower.Contains("edge") || lower.Contains("firefox"))
            return "🌐";

        // Office / Work / Productivity
        if (lower.Contains("work") || lower.Contains("office") || lower.Contains("doc") || lower.Contains("kerja") || lower.Contains("task") || lower.Contains("job") || lower.Contains("excel") || lower.Contains("word") || lower.Contains("notion"))
            return "💼";

        // Study / Books / Education
        if (lower.Contains("book") || lower.Contains("study") || lower.Contains("read") || lower.Contains("kuliah") || lower.Contains("belajar") || lower.Contains("sekolah") || lower.Contains("buku"))
            return "📚";

        // Chat & Social
        if (lower.Contains("chat") || lower.Contains("social") || lower.Contains("discord") || lower.Contains("msg") || lower.Contains("wa") || lower.Contains("telegram") || lower.Contains("pesan"))
            return "💬";

        // Speed / Boost / Launchers
        if (lower.Contains("speed") || lower.Contains("rocket") || lower.Contains("boost") || lower.Contains("fast") || lower.Contains("launch"))
            return "🚀";

        // Favorites / Important
        if (lower.Contains("fav") || lower.Contains("star") || lower.Contains("bintang") || lower.Contains("top") || lower.Contains("pinned"))
            return "⭐";

        return "📁";
    }
}
