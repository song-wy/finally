using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DiabetesPatientApp.Models;
using DiabetesPatientApp.ViewModels;
using Microsoft.Extensions.Configuration;

namespace DiabetesPatientApp.Services
{
    /// <summary>
    /// 根据足部压力数据生成 AI 分析建议（日常行为、随访、减压鞋垫）
    /// </summary>
    public interface IFootPressureSuggestionService
    {
        Task<FootPressureSuggestionViewModel> GenerateSuggestionAsync(
            FootPressureRecord record,
            IEnumerable<FootPressureRecord>? recentRecords = null,
            CancellationToken cancellationToken = default);
    }

    public class FootPressureSuggestionService : IFootPressureSuggestionService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public FootPressureSuggestionService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        private static string ResolveChatCompletionsUrl(string? baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                return "https://api.openai.com/v1/chat/completions";

            var trimmed = baseUrl.Trim().TrimEnd('/');
            if (trimmed.Contains("/chat/completions", StringComparison.OrdinalIgnoreCase))
                return trimmed;

            if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                return trimmed + "/chat/completions";

            return trimmed + "/v1/chat/completions";
        }

        public async Task<FootPressureSuggestionViewModel> GenerateSuggestionAsync(
            FootPressureRecord record,
            IEnumerable<FootPressureRecord>? recentRecords = null,
            CancellationToken cancellationToken = default)
        {
            // 先生成一份规则版作为兜底（确保功能可用）
            var fallback = GenerateSuggestionFallback(record);

            var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
            var baseUrl = _configuration["OpenAI:BaseUrl"]?.Trim();
            var model = _configuration["OpenAI:Model"]?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            {
                return fallback;
            }

            // 足压建议是纯文本分析，DeepSeek 可用（不涉及图片输入）
            var user = record.User;
            var age = user?.DateOfBirth == null ? (int?)null : (int)((DateTime.Now - user.DateOfBirth.Value).TotalDays / 365.25);
            var leftStatus = record.LeftFootStatus ?? "未测量";
            var rightStatus = record.RightFootStatus ?? "未测量";
            var walkingMinutes = ExtractWalkingDurationMinutes(record.Notes);
            var maxRisk = GetMaxRiskLevel(leftStatus, rightStatus);

            var recentBlock = BuildRecentRecordsBlock(recentRecords);

            var prompt = $@"你是一位糖尿病足与足病护理专家。请根据以下患者足部压力监测结果，生成一份中文“足部压力建议单”。要求：内容专业、可执行、避免夸大；若数据不足请给出合理提示。输出必须是严格 JSON（不要 markdown 代码块，不要额外说明），字段如下：
{{
  ""daily_behavior_advice"": ""日常行为建议（条目化，包含减压/步行/鞋袜/足部检查等）"",
  ""follow_up_advice"": ""随访建议（建议复查频率、何时就医等）"",
  ""insole_advice"": ""减压鞋垫/鞋具建议（是否建议定制、注意事项）"",
  ""risk_pattern_advice"": ""足部压力过载/步态/行走分析与建议（可为空字符串）""
}}

【患者信息】
- 姓名：{(string.IsNullOrWhiteSpace(user?.FullName) ? (user?.Username ?? "—") : user!.FullName)}
- 年龄：{(age?.ToString() ?? "—")} 岁

【足部压力数据】
- 左脚压力：{(record.LeftFootPressure?.ToString("F2") ?? "—")} kPa；风险等级：{leftStatus}
- 右脚压力：{(record.RightFootPressure?.ToString("F2") ?? "—")} kPa；风险等级：{rightStatus}
- 综合风险：{maxRisk}
- 行走时长：{(walkingMinutes.HasValue ? walkingMinutes.Value + " 分钟" : "未提供")}
- 备注：{(string.IsNullOrWhiteSpace(record.Notes) ? "无" : record.Notes)}

【近期足压记录明细（用于个性化差异化分析）】
{recentBlock}

注意：建议中要包含“若出现足部破溃/红肿热痛/渗出异味/发热等立即就医”的提示。";

            // 强制差异化：要求引用具体记录并做对比
            prompt += @"

额外要求：
1) 建议必须“因数据而异”，不要输出通用模板句。
2) risk_pattern_advice 必须引用至少 2 条【近期足压记录明细】中的具体条目（包含日期与左右数值或风险等级），并说明“风险是否有上升/下降/波动”。
3) 若【近期足压记录明细】不足 2 条，则明确说明“记录不足，建议增加监测频次”。";

            var url = ResolveChatCompletionsUrl(baseUrl);
            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                max_tokens = 1200,
                temperature = 0.6
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Authorization", "Bearer " + apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            HttpResponseMessage res;
            try
            {
                res = await _httpClient.SendAsync(req, cancellationToken);
            }
            catch
            {
                return fallback;
            }

            if (!res.IsSuccessStatusCode)
                return fallback;

            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            try
            {
                using var doc = JsonDocument.Parse(json);
                var content = doc.RootElement
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString();
                if (string.IsNullOrWhiteSpace(content))
                    return fallback;

                var cleaned = content.Trim();
                var start = cleaned.IndexOf('{');
                var end = cleaned.LastIndexOf('}');
                if (start >= 0 && end > start)
                    cleaned = cleaned.Substring(start, end - start + 1);

                using var payload = JsonDocument.Parse(cleaned);
                var root = payload.RootElement;

                fallback.DailyBehaviorAdvice = root.TryGetProperty("daily_behavior_advice", out var v1) ? (v1.GetString() ?? fallback.DailyBehaviorAdvice) : fallback.DailyBehaviorAdvice;
                fallback.FollowUpAdvice = root.TryGetProperty("follow_up_advice", out var v2) ? (v2.GetString() ?? fallback.FollowUpAdvice) : fallback.FollowUpAdvice;
                fallback.InsoleAdvice = root.TryGetProperty("insole_advice", out var v3) ? (v3.GetString() ?? fallback.InsoleAdvice) : fallback.InsoleAdvice;
                fallback.RiskPatternAdvice = root.TryGetProperty("risk_pattern_advice", out var v4) ? (v4.GetString() ?? fallback.RiskPatternAdvice) : fallback.RiskPatternAdvice;
                fallback.GeneratedAt = DateTime.Now;
                return fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static string BuildRecentRecordsBlock(IEnumerable<FootPressureRecord>? recentRecords)
        {
            if (recentRecords == null) return "（无近期记录）";

            var lines = recentRecords
                .Where(r => r != null)
                .OrderByDescending(r => r.RecordDate)
                .ThenByDescending(r => r.RecordTime)
                .Take(10)
                .Select(r =>
                {
                    var time = $"{r.RecordTime.Hours:D2}:{r.RecordTime.Minutes:D2}";
                    var left = $"{(r.LeftFootPressure?.ToString("F2") ?? "—")}kPa/{(r.LeftFootStatus ?? "—")}";
                    var right = $"{(r.RightFootPressure?.ToString("F2") ?? "—")}kPa/{(r.RightFootStatus ?? "—")}";
                    var walking = ExtractWalkingDurationMinutes(r.Notes);
                    var walkingText = walking.HasValue ? $"{walking.Value}分钟" : "未提供";
                    return $"{r.RecordDate:yyyy-MM-dd} {time}｜左 {left}｜右 {right}｜行走 {walkingText}";
                })
                .ToList();

            return lines.Count == 0 ? "（无近期记录）" : string.Join("\n", lines);
        }

        private static FootPressureSuggestionViewModel GenerateSuggestionFallback(FootPressureRecord record)
        {
            var user = record.User;
            var age = user?.DateOfBirth == null ? (int?)null : (int)((DateTime.Now - user.DateOfBirth.Value).TotalDays / 365.25);
            var left = record.LeftFootStatus ?? "未测量";
            var right = record.RightFootStatus ?? "未测量";
            var maxRisk = GetMaxRiskLevel(left, right);
            var walkingDuration = ExtractWalkingDurationMinutes(record.Notes);

            var vm = new FootPressureSuggestionViewModel
            {
                RecordId = record.FootPressureId,
                PatientName = user?.FullName,
                Age = age,
                LeftStatus = record.LeftFootStatus,
                RightStatus = record.RightFootStatus,
                LeftPressure = record.LeftFootPressure,
                RightPressure = record.RightFootPressure,
                DailyBehaviorAdvice = GenerateDailyBehaviorAdvice(maxRisk, left, right),
                FollowUpAdvice = GenerateFollowUpAdvice(maxRisk, left, right),
                InsoleAdvice = GenerateInsoleAdvice(maxRisk, record.LeftFootPressure, record.RightFootPressure),
                RiskPatternAdvice = GenerateRiskPatternAdvice(maxRisk, left, right, record.LeftFootPressure, record.RightFootPressure, walkingDuration),
                GeneratedAt = DateTime.Now
            };
            return vm;
        }

        private static string GetMaxRiskLevel(string left, string right)
        {
            var order = new[] { "低风险", "中风险", "高风险", "极高风险" };
            int li = Array.IndexOf(order, left);
            int ri = Array.IndexOf(order, right);
            if (li < 0) li = -1;
            if (ri < 0) ri = -1;
            int max = Math.Max(li, ri);
            return max >= 0 ? order[max] : "未测量";
        }

        private static string GenerateDailyBehaviorAdvice(string maxRisk, string left, string right)
        {
            var sb = new StringBuilder();
            switch (maxRisk)
            {
                case "低风险":
                    sb.AppendLine("· 当前足部压力处于低风险范围，请继续保持良好习惯。");
                    sb.AppendLine("· 每日保持适量步行，避免长时间静坐或久站。");
                    sb.AppendLine("· 穿宽松、透气的鞋袜，避免赤足行走于硬地面。");
                    sb.AppendLine("· 控制体重、稳定血糖，有助于维持足部健康。");
                    break;
                case "中风险":
                    sb.AppendLine("· 足部压力已达中风险，需加强日常防护。");
                    sb.AppendLine("· 减少长时间站立与行走，每30分钟适当休息并抬高双足。");
                    sb.AppendLine("· 选择鞋底有一定缓冲的鞋子，避免穿高跟鞋或过紧的鞋。");
                    sb.AppendLine("· 每日检查足底是否有红肿、破皮，发现异常及时就医。");
                    sb.AppendLine("· 保持足部清洁干燥，规律监测血糖。");
                    break;
                case "高风险":
                    sb.AppendLine("· 足部压力处于高风险，请高度重视日常行为管理。");
                    sb.AppendLine("· 尽量避免长时间负重行走，必要时使用助行器。");
                    sb.AppendLine("· 务必穿戴具有减压效果的鞋垫与合脚的防护鞋。");
                    sb.AppendLine("· 每日多次短时休息并抬高下肢，促进血液循环。");
                    sb.AppendLine("· 严格控糖、戒烟限酒，配合医生进行足部护理。");
                    break;
                case "极高风险":
                    sb.AppendLine("· 足部压力为极高风险，需立即采取综合干预。");
                    sb.AppendLine("· 减少不必要的行走与站立，以休息与抬高患肢为主。");
                    sb.AppendLine("· 必须使用专业减压鞋垫及定制/防护鞋，避免足底高压区继续受压。");
                    sb.AppendLine("· 每日由本人或家属检查足部，出现伤口、变色、发热等立即就诊。");
                    sb.AppendLine("· 严格遵医嘱控糖、用药，并定期到专科随访。");
                    break;
                default:
                    sb.AppendLine("· 建议完善足部压力测量后，再根据结果制定日常行为建议。");
                    sb.AppendLine("· 日常注意足部清洁、保湿与检查，避免外伤与感染。");
                    break;
            }
            return sb.ToString().TrimEnd();
        }

        private static string GenerateFollowUpAdvice(string maxRisk, string left, string right)
        {
            var sb = new StringBuilder();
            switch (maxRisk)
            {
                case "低风险":
                    sb.AppendLine("· 建议每3～6个月复查一次足部压力及足部外观。");
                    sb.AppendLine("· 年度体检时包含足部专科检查。");
                    break;
                case "中风险":
                    sb.AppendLine("· 建议每1～2个月复查足部压力，并做足部外观与神经血管评估。");
                    sb.AppendLine("· 若合并血糖控制不佳或既往足病史，可缩短至每月一次。");
                    break;
                case "高风险":
                    sb.AppendLine("· 建议每2～4周随访一次，复查足部压力与足部情况。");
                    sb.AppendLine("· 由专科医生评估是否需要定制鞋垫或矫形鞋。");
                    sb.AppendLine("· 加强血糖与并发症监测。");
                    break;
                case "极高风险":
                    sb.AppendLine("· 建议每1～2周专科随访，密切监测足部压力与皮肤状况。");
                    sb.AppendLine("· 尽快完成减压鞋垫/定制鞋的配置与适应性训练。");
                    sb.AppendLine("· 出现任何破溃、感染征象须立即就诊，必要时住院治疗。");
                    break;
                default:
                    sb.AppendLine("· 完成足部压力测量后，根据医生建议安排随访频率。");
                    break;
            }
            return sb.ToString().TrimEnd();
        }

        private static string GenerateInsoleAdvice(string maxRisk, decimal? leftPressure, decimal? rightPressure)
        {
            var sb = new StringBuilder();
            switch (maxRisk)
            {
                case "低风险":
                    sb.AppendLine("· 可选通用型缓冲鞋垫，以舒适、透气为主，减轻日常行走时的压力。");
                    sb.AppendLine("· 无需定制，选择支撑适中、材质柔软的成品鞋垫即可。");
                    break;
                case "中风险":
                    sb.AppendLine("· 建议使用具有足弓支撑与后跟减压的糖尿病防护鞋垫。");
                    sb.AppendLine("· 可选用根据足型分类的成品减压鞋垫，或经足底压力检测后选型。");
                    sb.AppendLine("· 避免过硬、过薄或无支撑的鞋垫。");
                    break;
                case "高风险":
                    sb.AppendLine("· 强烈建议使用专业减压鞋垫，优先考虑根据足底压力分布定制的型号。");
                    sb.AppendLine("· 鞋垫应具备前掌、足弓、后跟分区减压与稳定支撑。");
                    sb.AppendLine("· 配合防护鞋使用，并定期（如每3～6个月）重新评估压力与鞋垫磨损情况。");
                    break;
                case "极高风险":
                    sb.AppendLine("· 必须使用定制减压鞋垫及专业防护/定制鞋，以最大程度降低足底高压区压力。");
                    sb.AppendLine("· 建议在具备足踝专科的医疗机构完成压力检测与取型定制。");
                    sb.AppendLine("· 鞋垫与鞋需一起更换与随访，避免局部压力集中导致溃疡。");
                    break;
                default:
                    sb.AppendLine("· 完成足部压力检测后，由医生根据压力分布与风险等级推荐鞋垫类型与是否需定制。");
                    break;
            }
            return sb.ToString().TrimEnd();
        }

        private static int? ExtractWalkingDurationMinutes(string? notes)
        {
            if (string.IsNullOrWhiteSpace(notes)) return null;
            var m = Regex.Match(notes, @"行走时长：\s*(\d+)\s*分钟");
            if (m.Success && int.TryParse(m.Groups[1].Value, out var minutes))
                return minutes;
            return null;
        }

        private static string GenerateRiskPatternAdvice(
            string maxRisk,
            string leftStatus,
            string rightStatus,
            decimal? leftPressure,
            decimal? rightPressure,
            int? walkingMinutes)
        {
            var sb = new StringBuilder();

            // 1. 足部压力是否过载
            var isOverload = maxRisk == "高风险" || maxRisk == "极高风险";
            sb.AppendLine($"· 足部压力过载：{(isOverload ? "是" : "否")}。");
            if (isOverload)
            {
                sb.AppendLine("  建议尽量减少长时间站立与行走，重点减压高压区域，可通过专业减压鞋垫、减压鞋及适当休息来分散压力。");
            }
            else
            {
                sb.AppendLine("  当前整体压力水平尚可，仍需保持合适鞋袜与规律活动，避免体重明显上升导致压力增加。");
            }

            // 2. 是否存在步态异常（左右风险等级差异明显视为可疑）
            var order = new[] { "低风险", "中风险", "高风险", "极高风险" };
            int li = Array.IndexOf(order, leftStatus);
            int ri = Array.IndexOf(order, rightStatus);
            bool possibleGaitAbnormal = li >= 0 && ri >= 0 && Math.Abs(li - ri) >= 2;
            sb.AppendLine($"· 步态异常风险：{(possibleGaitAbnormal ? "可能存在" : "暂未见明显异常")}。");
            if (possibleGaitAbnormal)
            {
                sb.AppendLine("  左右足部风险等级差异较大，提示可能存在受力不均或步态异常，建议由康复/足病专科评估步态，并考虑物理治疗或鞋垫调整。");
            }
            else
            {
                sb.AppendLine("  左右足部风险等级相对接近，若无明显疼痛或跛行，可继续观察；如主观感觉行走不稳或单侧疼痛，仍建议就诊评估步态。");
            }

            // 3. 行走时间是否超时（示例阈值：> 60 分钟视为超时）
            if (walkingMinutes.HasValue)
            {
                var isOvertime = walkingMinutes.Value > 60;
                sb.AppendLine($"· 行走时长：本次记录前约行走 {walkingMinutes.Value} 分钟，行走超时：{(isOvertime ? "是" : "否")}。");
                if (isOvertime)
                {
                    sb.AppendLine("  行走时间偏长，建议将一次连续行走控制在 30～45 分钟内，并中间安排坐下或抬高下肢休息，避免长期足底高压。");
                }
                else
                {
                    sb.AppendLine("  行走时间在可接受范围内，如存在高风险/极高风险，仍建议缩短单次行走时长、增加休息频率。");
                }
            }
            else
            {
                sb.AppendLine("· 行走时长：本次记录未提供具体行走时间，若感觉行走后足部明显酸胀或疼痛，建议减少单次连续行走时间并增加休息。");
            }

            return sb.ToString().TrimEnd();
        }
    }
}
