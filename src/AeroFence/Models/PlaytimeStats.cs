using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AeroFence.Models;

/// <summary>
/// Tracks execution metrics and history for an application or game.
/// Formats durations cleanly for internationalization (i18n).
/// </summary>
public partial class PlaytimeStats : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedPlaytime))]
    [NotifyPropertyChangedFor(nameof(FormattedPlaytimeId))]
    private long _totalPlaytimeSeconds = 0;

    [ObservableProperty]
    private DateTime? _lastPlayed = null;

    [ObservableProperty]
    private int _launchCount = 0;

    /// <summary>
    /// Clean duration string without prepended verbs (e.g. "1.5 hrs", "20 mins", "Never")
    /// </summary>
    public string FormattedPlaytime
    {
        get
        {
            if (TotalPlaytimeSeconds <= 0) return "Never";
            var span = TimeSpan.FromSeconds(TotalPlaytimeSeconds);
            if (span.TotalHours >= 1)
                return $"{span.TotalHours:0.#} hrs";
            return $"{span.Minutes} mins";
        }
    }

    /// <summary>
    /// Indonesian localization helper if switched to ID mode.
    /// </summary>
    public string FormattedPlaytimeId
    {
        get
        {
            if (TotalPlaytimeSeconds <= 0) return "Belum pernah";
            var span = TimeSpan.FromSeconds(TotalPlaytimeSeconds);
            if (span.TotalHours >= 1)
                return $"{span.TotalHours:0.#} jam";
            return $"{span.Minutes} menit";
        }
    }
}
