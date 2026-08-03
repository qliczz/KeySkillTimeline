# 关键技能时间轴（Dalamud）

面向《最终幻想 XIV》“妖星乱舞绝境战”的白魔法师技能计划插件。默认计划来自 FF Logs 报告 `BfwHNxhPJRKy76cV` 的 fight 5，并在每项技能前 4 秒提醒。

注意：Dalamud 的 ImGui 窗口绘制在游戏画面上，它不是可拖到游戏画面之外的原生 Windows 窗口。本版本满足“替代 cactbot 的 Dalamud 可视化时间轴”；若必须在另一块屏幕或游戏窗口外显示，需要再加一个桌面伴侣进程，不能只靠常规 Dalamud 窗口完成。

## 功能

- 独立、可拖动和缩放的 Dalamud ImGui 时间轴窗口
- 当前/下一技能大字提醒、倒计时和未来项目表格
- Windows 中文 TTS、Dalamud 通知；均可独立开关
- 每项技能独立开关、显示名、颜色、提前秒数
- 时间点新增、删除、禁用、改时间、改技能和备注
- 战斗开始自动启动；首领读条 Action ID 自动校时
- 手动暂停、重置、±1 秒修正
- JSON 计划导入/导出以及一键恢复默认计划

## 命令

`/kst` 打开/关闭主窗口。

可用参数：`config`、`start`、`reset`、`pause`、`test`、`+1`、`-1`。

## 本地安装测试

1. 解压 `latest.zip` 到一个固定目录。
2. 游戏中打开 Dalamud 设置 → `Experimental` → `Dev Plugin Locations`。
3. 添加解压后的目录或其中的 `KeySkillTimeline.dll`。
4. 在开发插件列表中加载 `关键技能时间轴`，输入 `/kst` 打开。

这是本地开发包，不是 Dalamud 官方仓库已审核插件。

## 重要边界

默认时间点复现的是一份实际过本日志里的白魔施法计划，不代表唯一正确打法。队伍减伤分配、击杀时间或策略改变时，应在“计划编辑”页调整。

插件只使用 Dalamud 公共 API 读取当前区域、职业、战斗状态和首领读条。它不发送按键、不自动施法，也不写游戏内存。

ACT 路径与本插件无关：Dalamud 插件由 XIVLauncher/Dalamud 加载，不能放进 ACT 的 `Plugins` 目录。发布包位于 `src/KeySkillTimeline/bin/Release/KeySkillTimeline/latest.zip`。

## 构建

需要 Dalamud API 15 的开发库和 .NET 10 SDK：

```powershell
$env:DALAMUD_HOME='你的 Dalamud Hooks/dev 目录'
dotnet build .\src\KeySkillTimeline\KeySkillTimeline.csproj -c Release
dotnet run --project .\tests\KeySkillTimeline.Validation\KeySkillTimeline.Validation.csproj -c Release
```
