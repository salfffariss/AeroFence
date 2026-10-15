Add-Type -AssemblyName System.Drawing

# 1. Icon from .lnk
$iconLnk = [System.Drawing.Icon]::ExtractAssociatedIcon("C:\Users\Public\Desktop\哔哩哔哩.lnk")
$iconLnk.ToBitmap().Save("E:\RE-Anything\AeroFence\Assets\icon_from_lnk.png", [System.Drawing.Imaging.ImageFormat]::Png)

# 2. Icon from resolved target .exe
$sh = New-Object -ComObject WScript.Shell
$sc = $sh.CreateShortcut("C:\Users\Public\Desktop\哔哩哔哩.lnk")
$target = $sc.TargetPath
Write-Output "Resolved target: $target"
if (Test-Path $target) {
    $iconExe = [System.Drawing.Icon]::ExtractAssociatedIcon($target)
    $iconExe.ToBitmap().Save("E:\RE-Anything\AeroFence\Assets\icon_from_exe.png", [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output "Saved both icons!"
}
