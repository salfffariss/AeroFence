$sh = New-Object -ComObject WScript.Shell
$sc = $sh.CreateShortcut("C:\Users\Public\Desktop\哔哩哔哩.lnk")
Write-Output "TargetPath: $($sc.TargetPath)"
Write-Output "IconLocation: $($sc.IconLocation)"
Write-Output "Arguments: $($sc.Arguments)"
Write-Output "WorkingDirectory: $($sc.WorkingDirectory)"
