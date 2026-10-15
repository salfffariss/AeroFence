using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AeroFence.Models;

namespace AeroFence.Services.Storage;

/// <summary>
/// Portable JSON implementation of IStorageService.
/// Operates directly on local JSON files with zero Windows Registry modifications.
/// Implements safe atomic file writes to prevent data corruption.
/// </summary>
public class JsonStorageService : IStorageService
{
    private readonly string _configFilePath;
    private readonly JsonSerializerOptions _jsonOptions;

    public JsonStorageService(string? customPath = null)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            _configFilePath = customPath;
        }
        else
        {
            // Portable directory relative to application executable
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string dataDir = Path.Combine(baseDir, "Data");
            _configFilePath = Path.Combine(dataDir, "fences.json");
        }

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<AppConfig> LoadConfigAsync()
    {
        return await Task.Run(() => LoadConfig());
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        await Task.Run(() => SaveConfig(config));
    }

    public AppConfig LoadConfig()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                var defaultConfig = CreateDefaultConfig();
                SaveConfig(defaultConfig);
                return defaultConfig;
            }

            string json = File.ReadAllText(_configFilePath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);
            return config ?? CreateDefaultConfig();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JsonStorageService] Error loading config: {ex.Message}");
            return CreateDefaultConfig();
        }
    }

    public void SaveConfig(AppConfig config)
    {
        try
        {
            string? dir = Path.GetDirectoryName(_configFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(config, _jsonOptions);

            // Safe Atomic Write: write to temporary file first, then overwrite target
            string tempFile = _configFilePath + ".tmp";
            File.WriteAllText(tempFile, json);
            File.Move(tempFile, _configFilePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[JsonStorageService] Error saving config: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates clean initial seed data for first-time application launch.
    /// </summary>
    public AppConfig CreateDefaultConfig()
    {
        var fence = new FenceModel
        {
            Id = "fence_main",
            Title = "Workspace",
            X = 120,
            Y = 100,
            Width = 560,
            Height = 380,
            IsRolledUp = false,
            ActiveTabIndex = 0
        };

        var defaultTab = new TabModel
        {
            Id = "tab_main",
            Name = "General",
            IconGlyph = "📁",
            LayoutMode = TabLayoutMode.FreeForm,
            Items = new List<ItemModel>()
        };

        fence.Tabs.Add(defaultTab);

        return new AppConfig
        {
            Version = "1.0.0",
            EnableAcrylic = true,
            AttachToDesktop = false,
            Fences = new List<FenceModel> { fence }
        };
    }
}
