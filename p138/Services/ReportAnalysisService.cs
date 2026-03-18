namespace DiabetesPatientApp.Services
{
    /// <summary>
    /// 提供给 AI 的报告分析上下文（患者信息、血糖数据、饮食/用药/运动记录摘要）
    /// </summary>
    public class ReportAnalysisContext
    {
        public string? PatientName { get; set; }
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public int Days { get; set; }
        public int TotalCount { get; set; }
        public string FrequencyText { get; set; } = string.Empty;
        public int FastingCount { get; set; }
        public decimal FastingAvg { get; set; }
        public decimal FastingMin { get; set; }
        public decimal FastingMax { get; set; }
        public decimal FastingComplianceRate { get; set; }
        public int FastingHighCount { get; set; }
        public int FastingLowCount { get; set; }
        public int AfterMealCount { get; set; }
        public decimal AfterMealAvg { get; set; }
        public decimal AfterMealMin { get; set; }
        public decimal AfterMealMax { get; set; }
        public decimal AfterMealComplianceRate { get; set; }
        public int AfterMealHighCount { get; set; }
        public int AfterMealLowCount { get; set; }
        public int TotalHighCount { get; set; }
        public int TotalLowCount { get; set; }
        public List<string> SampleNotes { get; set; } = new();
        /// <summary>用于 AI 个性化分析的“原始记录摘要行”（日期/时间/类型/数值/状态/备注）</summary>
        public List<string> SampleRecordLines { get; set; } = new();
    }

    /// <summary>
    /// AI 返回的报告段落
    /// </summary>
    public class AiReportResult
    {
        public string TrendAnalysis { get; set; } = string.Empty;
        public string HyperglycemiaRiskText { get; set; } = string.Empty;
        public string HypoglycemiaRiskText { get; set; } = string.Empty;
        public string DietSuggestion { get; set; } = string.Empty;
        public string MedicationSuggestion { get; set; } = string.Empty;
        public string ExerciseSuggestion { get; set; } = string.Empty;
        public string MonitoringSuggestion { get; set; } = string.Empty;
    }

    public interface IReportAnalysisService
    {
        Task<AiReportResult?> GenerateAnalysisAsync(ReportAnalysisContext context, System.Threading.CancellationToken cancellationToken = default);
    }

    public class ReportAnalysisService : IReportAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public ReportAnalysisService(HttpClient httpClient, IConfiguration configuration)
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

        public async Task<AiReportResult?> GenerateAnalysisAsync(ReportAnalysisContext ctx, System.Threading.CancellationToken cancellationToken = default)
        {
            var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
            var baseUrl = _configuration["OpenAI:BaseUrl"]?.Trim();
            var model = _configuration["OpenAI:Model"]?.Trim();
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(model))
                return null;

            var notesBlock = ctx.SampleNotes.Count > 0
                ? string.Join("\n", ctx.SampleNotes.Take(10).Select((n, i) => $"{i + 1}. {n}"))
                : "（无饮食、用药或运动记录）";

            var recordsBlock = ctx.SampleRecordLines.Count > 0
                ? string.Join("\n", ctx.SampleRecordLines.Take(20).Select((n, i) => $"{i + 1}. {n}"))
                : "（无血糖记录明细）";

            var prompt = $@"你是一位糖尿病管理助手。请根据以下患者的血糖监测数据、以及其在新增血糖记录中填写的进食记录、用药记录、运动记录，生成一份血糖分析报告的相关段落。所有内容用中文，语气专业、简洁。

【患者与监测概况】
- 患者：{ctx.PatientName ?? "—"}，年龄：{ctx.Age?.ToString() ?? "—"}，性别：{ctx.Gender ?? "—"}
- 监测时段：最近{ctx.Days}天；总监测次数：{ctx.TotalCount}次；{ctx.FrequencyText}

【空腹血糖】
- 记录次数：{ctx.FastingCount}；平均值：{ctx.FastingAvg:F2} mmol/L，最低：{ctx.FastingMin:F2}，最高：{ctx.FastingMax:F2}
- 达标率（4.4～7.0 mmol/L）：{ctx.FastingComplianceRate:F0}%；偏高次数：{ctx.FastingHighCount}，偏低次数：{ctx.FastingLowCount}

【餐后血糖】
- 记录次数：{ctx.AfterMealCount}；平均值：{ctx.AfterMealAvg:F2} mmol/L，最低：{ctx.AfterMealMin:F2}，最高：{ctx.AfterMealMax:F2}
- 达标率（6.0～10.0 mmol/L）：{ctx.AfterMealComplianceRate:F0}%；偏高次数：{ctx.AfterMealHighCount}，偏低次数：{ctx.AfterMealLowCount}

【整体】
- 高血糖总次数：{ctx.TotalHighCount}；低血糖总次数：{ctx.TotalLowCount}

【血糖记录明细（用于个性化分析）】
{recordsBlock}

【患者填写的进食、用药与运动记录摘要】（每条可能包含：进食记录-主食/蛋白质/蔬菜/进食量/特殊情况，用药记录-用药剂量/用药说明，运动记录-运动类型/运动时长/运动说明）
{notesBlock}

请严格根据上述记录内容，结合血糖数据，按以下要求生成建议：
1) 内容必须“因数据而异”，不要输出通用模板句；避免“建议咨询医生”等空话。
2) 在 trend_analysis 中必须引用至少 3 条“血糖记录明细”里的具体条目（包含日期与数值，并说明空腹/餐后差异或波动）。
3) 饮食/用药/运动建议必须结合患者实际填写的对应内容（若缺失则提示如何补充记录）。
输出一段完整的 JSON（不要 markdown 代码块，不要多余说明），每个值为一段中文分析或建议文字：
""trend_analysis"": 对血糖趋势的简要分析（结合监测时段、空腹与餐后数据、达标情况）。
""hyperglycemia_risk"": 高血糖感染风险的评估与建议（一两句话）。
""hypoglycemia_risk"": 低血糖跌倒风险的评估与建议（一两句话）。
""diet_suggestion"": 饮食建议。必须结合患者填写的进食记录（主食、蛋白质、蔬菜、进食量、特殊情况）与血糖情况，具体说明：建议多吃哪类食物、少吃或避免哪类食物、每类食物或每餐进食量建议（如主食多少、蛋白质与蔬菜比例等）。若记录中有偏少/偏多/拒食等，要针对性地给出建议。
""medication_suggestion"": 用药建议。必须结合患者填写的用药记录（用药剂量、用药说明）与血糖控制情况，对用药剂量或药物类型提出具体建议（如是否需调整剂量、是否建议某类药物等）；同时注明本建议不能替代医嘱，具体用药须遵医嘱。
""exercise_suggestion"": 运动建议。必须结合患者填写的运动记录（运动类型、运动时长、运动说明）与血糖情况，具体说明：建议做哪种或哪几种运动、每次/每周建议运动时长（如每次多少分钟、每周几次或多少分钟），以及运动强度或注意事项；若当前有运动记录可评价是否适宜、是否需增减。
""monitoring_suggestion"": 监测次数与频率建议。";

            var url = ResolveChatCompletionsUrl(baseUrl);

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                max_tokens = 2000,
                temperature = 0.6
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Authorization", "Bearer " + apiKey);
            req.Content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8,
                "application/json");

            HttpResponseMessage res;
            try
            {
                res = await _httpClient.SendAsync(req, cancellationToken);
            }
            catch
            {
                return null;
            }

            if (!res.IsSuccessStatusCode)
                return null;

            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            if (string.IsNullOrWhiteSpace(content))
                return null;

            content = content.Trim();
            if (content.StartsWith("```"))
            {
                var start = content.IndexOf('{');
                var end = content.LastIndexOf('}');
                if (start >= 0 && end > start)
                    content = content.Substring(start, end - start + 1);
            }

            try
            {
                var root = System.Text.Json.JsonDocument.Parse(content).RootElement;
                return new AiReportResult
                {
                    TrendAnalysis = GetStr(root, "trend_analysis"),
                    HyperglycemiaRiskText = GetStr(root, "hyperglycemia_risk"),
                    HypoglycemiaRiskText = GetStr(root, "hypoglycemia_risk"),
                    DietSuggestion = GetStr(root, "diet_suggestion"),
                    MedicationSuggestion = GetStr(root, "medication_suggestion"),
                    ExerciseSuggestion = GetStr(root, "exercise_suggestion"),
                    MonitoringSuggestion = GetStr(root, "monitoring_suggestion")
                };
            }
            catch
            {
                return null;
            }
        }

        private static string GetStr(System.Text.Json.JsonElement el, string key)
        {
            if (el.TryGetProperty(key, out var p))
                return p.GetString() ?? string.Empty;
            return string.Empty;
        }
    }
}

