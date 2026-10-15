$code = @"
using System;
using System.Runtime.InteropServices;
public class ShellRefresh {
    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);
}
"@
Add-Type -TypeDefinition $code
[ShellRefresh]::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
Write-Output "Windows Shell Icon Cache invalidated!"
