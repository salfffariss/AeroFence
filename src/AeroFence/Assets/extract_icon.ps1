Add-Type -AssemblyName System.Drawing
$icon = [System.Drawing.Icon]::ExtractAssociatedIcon("E:\RE-Anything\AeroFence_Setup.exe")
if ($icon -ne $null) {
    $bmp = $icon.ToBitmap()
    $bmp.Save("E:\RE-Anything\AeroFence\Assets\extracted_setup_icon.png", [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output "Saved extracted icon to extracted_setup_icon.png!"
} else {
    Write-Output "Icon was null!"
}
