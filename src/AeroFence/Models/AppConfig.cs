using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AeroFence.Models;

/// <summary>
/// Root configuration file schema for portable storage.
/// Observable so UI bindings automatically react to setting toggles.
/// </summary>
public partial class AppConfig : ObservableObject
{
    public string Version { get; set; } = "1.0.0";

    [ObservableProperty]
    private bool _enableAcrylic = true;

    [ObservableProperty]
    private bool _attachToDesktop = false;

    [ObservableProperty]
    private bool _startWithWindows = false;

    [ObservableProperty]
    private bool _enableChameleon = false;

    [ObservableProperty]
    private double _chameleonOpacity = 0.35;

    [ObservableProperty]
    private double _globalOpacity = 1.0;

    public List<FenceModel> Fences { get; set; } = new();
}
