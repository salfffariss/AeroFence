namespace AeroFence.Models;

/// <summary>
/// Represents a fence window container with its position, tabs, and dimensions.
/// </summary>
public class FenceModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Workspace";
    public double X { get; set; } = 150;
    public double Y { get; set; } = 150;
    public double Width { get; set; } = 540;
    public double Height { get; set; } = 380;
    public bool IsRolledUp { get; set; } = false;
    public int ActiveTabIndex { get; set; } = 0;
    public IconSizeOption IconSize { get; set; } = IconSizeOption.Medium;
    public List<TabModel> Tabs { get; set; } = new();

    // Folder Portal properties
    public bool IsFolderPortal { get; set; } = false;
    public string? FolderPath { get; set; }
}
