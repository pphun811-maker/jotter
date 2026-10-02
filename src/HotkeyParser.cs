using System.Windows.Forms;

namespace Jotter;

public static class HotkeyParser
{
    public static (uint Mods, uint Vk)? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        uint mods = 0, vk = 0;
        foreach (var raw in text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            switch (raw.ToLowerInvariant())
            {
                case "ctrl" or "control":
                    mods |= 0x2;
                    break;
                case "alt":
                    mods |= 0x1;
                    break;
                case "shift":
                    mods |= 0x4;
                    break;
                case "win":
                    mods |= 0x8;
                    break;
                default:
                    if (!Enum.TryParse<Keys>(raw, ignoreCase: true, out var key)) return null;
                    vk = (uint)key;
                    break;
            }
        }
        if (vk == 0) return null;
        return (mods | 0x4000, vk); // 0x4000 = MOD_NOREPEAT，按住不触发多次
    }
}
