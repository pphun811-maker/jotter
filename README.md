# Jotter 速记

Windows 常驻的键盘流速记工具：全局热键一键唤出小输入框，写下就走，自动按 ISO 周（以及一份总文档）存成 Markdown。纯本地，无联网、无账号。

![Jotter 弹窗](design/ui.png)

## 特性

- **全局热键 `Ctrl+Alt+N`**（可配置）随时唤出/收起，焦点自动归还原程序
- **按周归档 + 总文档双写**：写入 `Documents\Jotter\2026-W40.md`，同时同步进 `Jotter.md`；总文档误删会在下次写入时自动从周文档重建
- **条目带时间戳**：`**2026-10-01 周四 03:44**` 加粗一行 + 内容，多行原样保留
- **软件内查看与管理笔记**：托盘菜单或弹窗顶栏「查 看」打开阅读视图——左侧按周选文档（总文档默认全量汇总），每条速记一张卡片，**悬浮即显示复制 / 修改 / 删除**；删除和修改会同时同步周文档与总文档，不会出现"手工删了又复原"的情况；每次打开都重新读盘
- **一体化深色菜单与确认框**：托盘右键菜单、输入框右键菜单、删除确认框与主 UI 同一套设计语言（圆角、细边、灰阶），确认框无提示音
- **草稿保护**：热键收起不丢草稿，`Esc` 才丢弃；写盘失败内容留在输入框
- **边缘拖拽调大小**：拖窗口四边/四角即可，下次唤出自动回到默认尺寸与右下角位置，不留记忆
- **单实例 + 托盘常驻**：托盘菜单可查看笔记、打开笔记文件夹、开关开机自启动
- **纯灰阶 UI**：中性灰阶、DWM 圆角、实时字数、细滚动条，150% 缩放下按物理像素精确右下角定位

## 按键

| 按键 | 作用 |
|---|---|
| `Ctrl+Alt+N` | 唤出 / 收起（收起保留草稿） |
| `Shift+Enter` 或 `Ctrl+Enter` | 发送（输入法激活时兜底可用） |
| `Enter` | 换行 |
| `Esc` | 丢弃草稿并关闭 |
| 空内容发送 | 等于关闭 |

## 安装

**方式一：下载成品**（Releases 页）—— 需要 [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)。把 `Jotter.exe` 放到任意目录（建议 `%LOCALAPPDATA%\Programs\Jotter\`）直接运行。

**方式二：源码构建**（需要 .NET 10 SDK）：

```bash
cd src
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o ../dist
```

开机自启动：运行 `set-autostart.ps1`，或在托盘菜单里勾选（写的是当前用户的 Run 键，不需要管理员）。

已装好 Jotter 后更新版本：运行 `update-jotter.ps1`（自动结束进程 → 复制 `dist\Jotter.exe` 到安装目录 → 重启）。

## 配置

程序目录下的 `settings.json`（首次运行自动生成），改完重启生效：

```jsonc
{
  "Hotkey": "Ctrl+Alt+N",          // 任意 Ctrl/Shift/Alt 组合
  "NotesFolder": null,             // 笔记目录，null = Documents\Jotter
  "WindowWidth": 480,              // 弹窗默认尺寸（DIP），拖拽改动不回写
  "WindowHeight": 240
}
```

## 笔记格式

```
Documents\Jotter\
├── 2026-W40.md    # 按 ISO 周一个文件
├── 2026-W41.md
└── Jotter.md      # 总文档：所有条目的汇总
```

```markdown
# 2026 年第 40 周速记

**2026-10-01 周四 02:41**
第一条速记内容
多行也是原样保留
```

## 隐私

笔记只写本地 Markdown 文件，程序没有任何网络行为。仓库内的截图为示意内容。

## License

[MIT](LICENSE)
