using System.Globalization;
using System.IO;
using System.Text;

namespace Jotter;

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
