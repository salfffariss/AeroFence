using AeroFence.Models;

namespace AeroFence.Services.Storage;

/// <summary>
/// Abstraction contract for loading and saving application configuration.
/// Implements Dependency Inversion Principle (SOLID).
/// </summary>
public interface IStorageService
{
    Task<AppConfig> LoadConfigAsync();
    Task SaveConfigAsync(AppConfig config);
    AppConfig LoadConfig();
    void SaveConfig(AppConfig config);
}
