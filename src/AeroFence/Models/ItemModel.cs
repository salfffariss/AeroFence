using System;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AeroFence.Models;

/// <summary>
/// Represents an individual shortcut, executable, or file entry on the canvas.
/// Supports reactive selection and data binding via ObservableObject.
/// </summary>
public partial class ItemModel : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _targetPath = string.Empty;

    public string? Arguments { get; set; }
    public string? WorkingDirectory { get; set; }
    public string? CustomIconPath { get; set; }

    /// <summary>
    /// Relative X coordinate inside the tab canvas.
    /// </summary>
    public double X { get; set; } = 20;

    /// <summary>
    /// Relative Y coordinate inside the tab canvas.
    /// </summary>
    public double Y { get; set; } = 20;

    /// <summary>
    /// Transient UI selection state (ignored during JSON persistence).
    /// </summary>
    [JsonIgnore]
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Transient Type-to-Filter search state: dimmed if not matching query.
    /// </summary>
    [JsonIgnore]
    [ObservableProperty]
    private bool _isDimmed;

    /// <summary>
    /// Transient Type-to-Filter search state: highlighted with glow border if best match.
    /// </summary>
    [JsonIgnore]
    [ObservableProperty]
    private bool _isBestMatch;

    /// <summary>
    /// Transient execution state: true while the application or game process is currently running.
    /// </summary>
    [JsonIgnore]
    [ObservableProperty]
    private bool _isRunning;

    /// <summary>
    /// Transient inline rename state.
    /// </summary>
    [JsonIgnore]
    [ObservableProperty]
    private bool _isEditing;

    /// <summary>
    /// Transient drag tracking: true if dropped onto another card, canvas, or tab inside AeroFence.
    /// False if dropped outside the fence onto the Desktop.
    /// </summary>
    [JsonIgnore]
    public bool WasDroppedInternally { get; set; }

    /// <summary>
    /// Game / application tracking statistics.
    /// </summary>
    public PlaytimeStats Stats { get; set; } = new();
}
