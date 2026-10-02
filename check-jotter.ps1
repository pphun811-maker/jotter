param([uint32]$TargetPid = 0)
if ($TargetPid -eq 0) {
    $proc = Get-Process -Name Jotter -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $proc) { Write-Output "NO-JOTTER-PROCESS"; exit 1 }
    $TargetPid = [uint32]$proc.Id
}
Write-Output ("Checking PID " + $TargetPid)
Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WinEnum {
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lp);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lp);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
    public static uint Target;
    public static StringBuilder Out = new StringBuilder();
    public static bool Callback(IntPtr h, IntPtr lp) {
        uint p;
        GetWindowThreadProcessId(h, out p);
        if (p == Target) {
            StringBuilder t = new StringBuilder(256);
            GetWindowText(h, t, 256);
            RECT r;
            GetWindowRect(h, out r);
            Out.AppendFormat("hwnd=0x{0:X} visible={1} title='{2}' rect=({3},{4})-({5},{6})\r\n",
                h.ToInt64(), IsWindowVisible(h), t.ToString(), r.L, r.T, r.R, r.B);
        }
        return true;
    }
    public static string Dump(uint pid) {
        Target = pid; Out = new StringBuilder();
        EnumWindows(new EnumWindowsProc(Callback), IntPtr.Zero);
        return Out.ToString();
    }
}
"@
[WinEnum]::Dump($TargetPid)
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class ScrInfo {
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int nIndex);
}
"@
$w = [ScrInfo]::GetSystemMetrics(0); $h = [ScrInfo]::GetSystemMetrics(1)
$wh = [ScrInfo]::GetSystemMetrics(17)
Write-Output ("Screen: " + $w + "x" + $h + "  WorkAreaHeight: " + $wh)
