using KeySkillTimeline;

var skills = DancingMadPreset.CreateSkills();
var entries = DancingMadPreset.CreateEntries();

Check(skills.Count == 6, "应该有 6 个默认技能");
Check(skills.Select(x => x.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() == skills.Count, "技能 key 必须唯一");
Check(skills.All(x => Math.Abs(x.LeadSeconds - 4f) < 0.001f), "默认提前量必须为 4 秒");
Check(entries.Count == 65, "默认计划应该有 65 个时间点");
Check(entries.Select(x => x.Id).Distinct().Count() == entries.Count, "时间点 ID 必须唯一");
Check(entries.SequenceEqual(entries.OrderBy(x => x.TimeSeconds)), "时间点必须升序排列");
Check(entries.All(x => skills.Any(s => s.Key == x.SkillKey)), "每个时间点必须引用已知技能");
Check(entries.All(x => x.TimeSeconds >= 0 && x.TimeSeconds <= DancingMadPreset.DurationSeconds), "时间点必须在战斗范围内");
Check(DancingMadPreset.Phases.SequenceEqual(DancingMadPreset.Phases.OrderBy(x => x.TimeSeconds)), "阶段锚点必须升序排列");
Check(DancingMadPreset.SyncPoints.SequenceEqual(DancingMadPreset.SyncPoints.OrderBy(x => x.TimeSeconds)), "读条锚点必须升序排列");
Check(DancingMadPreset.SyncPoints.All(x => x.ActionId != 0), "读条 Action ID 不能为 0");

var firstAquaveil = entries.First(x => x.SkillKey == "aquaveil");
var aquaveil = skills.Single(x => x.Key == "aquaveil");
Check(Math.Abs((firstAquaveil.TimeSeconds - aquaveil.LeadSeconds) - 10.5f) < 0.001f, "首个水流幕应在 10.5 秒触发提醒");

Console.WriteLine($"Validation passed: {skills.Count} skills, {entries.Count} entries, {DancingMadPreset.SyncPoints.Length} sync anchors.");

static void Check(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
