using System.Globalization;
using System.IO;
using System.Text;

namespace Jotter;

public sealed class NoteEntry
{
    public string Stamp { get; init; } = "";   // "2026-10-01 周四 02:41"（不含星号）
    public string Body { get; init; } = "";    // 正文（多行以 \n 连接，两端已 Trim）
}

public static class NoteStore
{
    private const string MasterFile = "Jotter.md";
    private static readonly string[] WeekChars = { "日", "一", "二", "三", "四", "五", "六" };

    /// <summary>按 ISO 周写入条目，同时同步一份到总文档，返回周文件路径。</summary>
    public static string Append(JotterConfig cfg, string text)
    {
        var now = DateTime.Now;
        int year = ISOWeek.GetYear(now);
        int week = ISOWeek.GetWeekOfYear(now);

        var folder = cfg.EffectiveNotesFolder;
        Directory.CreateDirectory(folder);
        var weekFile = Path.Combine(folder, $"{year}-W{week:D2}.md");
        var master = Path.Combine(folder, MasterFile);

        EnsureMasterBuilt(folder, master);

        var sb = new StringBuilder();
        if (!File.Exists(weekFile))
            sb.AppendLine($"# {year} 年第 {week} 周速记").AppendLine();

        AppendEntry(sb, now, text);
        File.AppendAllText(weekFile, sb.ToString(), new UTF8Encoding(false));
        File.AppendAllText(master, sb.ToString(), new UTF8Encoding(false));
        return weekFile;
    }

    private static void AppendEntry(StringBuilder sb, DateTime now, string text)
    {
        sb.Append("**")
          .Append(now.ToString("yyyy-MM-dd"))
          .Append(" 周").Append(WeekChars[(int)now.DayOfWeek])
          .Append(now.ToString(" HH:mm"))
          .AppendLine("**");
        sb.AppendLine(text.Trim());
        sb.AppendLine();
    }

    /// <summary>
    /// 列出可查看的笔记文档：总文档在前，周文档按文件名倒序（最新一周在最上）。
    /// 目录不存在时返回空列表。
    /// </summary>
    public static IReadOnlyList<string> ListNoteFiles(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<string>();
        var files = new List<string>();
        var master = Path.Combine(folder, MasterFile);
        if (File.Exists(master)) files.Add(master);
        files.AddRange(Directory.GetFiles(folder, "*-W*.md")
            .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase));
        return files;
    }

    /// <summary>
    /// 解析文档为条目列表：**...** 整行开新条目，其后直到下一个条目头/# 行/EOF 为正文。
    /// 返回文档标题（# 行，总文档没有则 null）。
    /// </summary>
    public static (string? Title, List<NoteEntry> Entries) ParseDocument(string[] lines)
    {
        string? title = null;
        var entries = new List<NoteEntry>();
        int i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            if (line.StartsWith("# "))
            {
                title = line[2..].Trim();
                i++;
            }
            else if (IsEntryHead(line))
            {
                var body = new List<string>();
                i++;
                // 遇 # 行必须停：总文档里各周条目之间夹着周标题行，不停会吞进上一条的正文，
                // 导致那条的修改/删除按正文匹配永远失配（用户实测"删了又复原/改不动"的根因）
                while (i < lines.Length && !IsEntryHead(lines[i]) && !lines[i].StartsWith("# "))
                {
                    body.Add(lines[i]);
                    i++;
                }
                entries.Add(new NoteEntry
                {
                    Stamp = line[2..^2],
                    Body = string.Join("\n", body).Trim(),
                });
            }
            else
            {
                i++; // 空行或散行（旧格式残留）跳过
            }
        }
        return (title, entries);
    }

    private static bool IsEntryHead(string line)
        => line.StartsWith("**") && line.EndsWith("**") && line.Length > 4;

    /// <summary>删除一条速记：周文档与总文档里凡是 (stamp, body) 匹配的块都移除。</summary>
    public static void DeleteEntryEverywhere(string folder, string stamp, string body)
    {
        foreach (var f in EnumerateNoteFiles(folder))
            MutateFile(f, stamp, body, null);
    }

    /// <summary>修改一条速记的正文（时间戳保持创建时间），周文档与总文档同步更新。</summary>
    public static void UpdateEntryEverywhere(string folder, string stamp, string body, string newBody)
    {
        foreach (var f in EnumerateNoteFiles(folder))
            MutateFile(f, stamp, body, newBody);
    }

    private static IEnumerable<string> EnumerateNoteFiles(string folder)
    {
        if (!Directory.Exists(folder)) yield break;
        foreach (var f in Directory.GetFiles(folder, "*-W*.md")) yield return f;
        var master = Path.Combine(folder, MasterFile);
        if (File.Exists(master)) yield return master;
    }

    /// <summary>
    /// 在单文件内定位 (stamp, body) 匹配的条目块：newBody=null 删除整块（含尾随空行），
    /// 否则原位替换正文行。文件不动（无匹配）返回 false。
    /// </summary>
    private static bool MutateFile(string file, string stamp, string body, string? newBody)
    {
        if (!File.Exists(file)) return false;
        var lines = File.ReadAllLines(file).ToList();
        var stampLine = $"**{stamp}**";

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i] != stampLine) continue;
            int j = i + 1;
            while (j < lines.Count && !IsEntryHead(lines[j]) && !lines[j].StartsWith("# ")) j++;
            var blockBody = string.Join("\n", lines.Skip(i + 1).Take(j - i - 1)).Trim();
            if (blockBody != body.Trim()) continue;

            if (newBody == null)
            {
                int end = j;
                while (end < lines.Count && lines[end].Trim().Length == 0) end++; // 块尾空行一起带走
                lines.RemoveRange(i, end - i);
            }
            else
            {
                var newLines = newBody.Trim().Replace("\r\n", "\n").Split('\n');
                lines.RemoveRange(i + 1, j - i - 1);
                lines.InsertRange(i + 1, newLines);
            }
            File.WriteAllLines(file, lines, new UTF8Encoding(false));
            return true;
        }
        return false;
    }

    /// <summary>总文档被删过就重建：按文件名顺序把各周条目汇总（跳过周标题行）。</summary>
    private static void EnsureMasterBuilt(string folder, string master)
    {
        if (File.Exists(master)) return;
        var weeks = Directory.GetFiles(folder, "*-W*.md");
        if (weeks.Length == 0) return;

        var sb = new StringBuilder();
        foreach (var w in weeks.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var body = string.Join(Environment.NewLine,
                File.ReadAllLines(w).Where(l => !l.StartsWith("# ")));
            body = body.TrimStart('\r', '\n');
            if (body.Length == 0) continue;
            sb.Append(body);
            if (!body.EndsWith(Environment.NewLine)) sb.AppendLine();
            sb.AppendLine();
        }
        File.WriteAllText(master, sb.ToString(), new UTF8Encoding(false));
    }
}
