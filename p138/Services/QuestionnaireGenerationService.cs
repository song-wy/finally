using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace DiabetesPatientApp.Services
{
    public class QuestionnaireGenerationResult
    {
        public string Title { get; set; } = string.Empty;
        public string Introduction { get; set; } = string.Empty;
        public List<string> Questions { get; set; } = new();
        public string Source { get; set; } = string.Empty;
    }

    public interface IQuestionnaireGenerationService
    {
        Task<QuestionnaireGenerationResult> GenerateAsync(string keyword, string requirements, CancellationToken cancellationToken = default);
    }

    public class QuestionnaireGenerationService : IQuestionnaireGenerationService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public QuestionnaireGenerationService(HttpClient httpClient, IConfiguration configuration)
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

        public async Task<QuestionnaireGenerationResult> GenerateAsync(string keyword, string requirements, CancellationToken cancellationToken = default)
        {
            var normalizedKeyword = (keyword ?? string.Empty).Trim();
            var normalizedRequirements = (requirements ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(normalizedKeyword) && string.IsNullOrWhiteSpace(normalizedRequirements))
            {
                throw new InvalidOperationException("请输入关键词或补充要求后再生成问卷。");
            }

            // 多选关键词：按选择项逐项生成“了解程度量表”，每项固定 5 个选项（完全不了解→非常了解）
            var keywordItems = ParseKeywordItems(normalizedKeyword);
            if (keywordItems.Count > 0)
            {
                var scale = BuildKeywordKnowledgeScaleQuestionnaire(keywordItems, normalizedRequirements);
                scale.Source = "量表模板生成";
                return scale;
            }

            var aiResult = await GenerateWithOpenAiAsync(normalizedKeyword, normalizedRequirements, cancellationToken);
            if (aiResult != null && aiResult.Questions.Count > 0)
            {
                aiResult.Source = "AI生成";
                return aiResult;
            }

            var fallbackResult = BuildFallbackQuestionnaire(normalizedKeyword, normalizedRequirements);
            fallbackResult.Source = "智能模板生成";
            return fallbackResult;
        }

        private static List<string> ParseKeywordItems(string keyword)
        {
            var raw = (keyword ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(raw)) return new List<string>();

            var parts = raw
                .Split(new[] { ',', '，', '、', ';', '；', '|', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // 只要医生选择了关键词（哪怕 1 项），就走量表模式；避免把“自然语言长句”误判为关键词
            if (parts.Count == 1 && parts[0].Length > 40) return new List<string>();

            return parts;
        }

        private static QuestionnaireGenerationResult BuildKeywordKnowledgeScaleQuestionnaire(List<string> keywords, string requirements)
        {
            var title = "关键词评估问卷（了解程度量表）";
            var introSuffix = string.IsNullOrWhiteSpace(requirements) ? string.Empty : $"补充要求：{requirements}。";
            var introduction = $"请针对以下每一项关键词选择您的了解程度（完全不了解/一点点了解/一般/较了解/非常了解）。{introSuffix}".Trim();

            // 问题直接逐项列出关键词，便于前端按“了解程度”统一渲染选项
            return new QuestionnaireGenerationResult
            {
                Title = title,
                Introduction = introduction,
                Questions = keywords
            };
        }

        private async Task<QuestionnaireGenerationResult?> GenerateWithOpenAiAsync(string keyword, string requirements, CancellationToken cancellationToken)
        {
            var apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
            var baseUrl = _configuration["OpenAI:BaseUrl"]?.Trim();
            var model = _configuration["OpenAI:Model"]?.Trim();
            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            {
                return null;
            }

            var prompt = $@"你是一名医疗健康问卷设计助手。请根据医生输入的主题关键词和补充要求，生成一份结构清晰、适合门诊或随访使用的中文健康问卷。

【关键词】
{keyword}

【补充要求】
{(string.IsNullOrWhiteSpace(requirements) ? "无" : requirements)}

请输出一段严格的 JSON，不要 markdown 代码块，不要额外说明，格式如下：
{{
  ""title"": ""问卷标题"",
  ""introduction"": ""问卷开头说明，1-2句"",
  ""questions"": [""问题1"", ""问题2"", ""问题3""]
}}

要求：
1. 问卷标题简洁明确。
2. 问卷问题数量控制在 8 到 12 个。
3. 问题必须适合患者填写，措辞清楚、简洁。
4. 问题内容要尽量覆盖症状、生活方式、既往情况、依从性或风险因素等相关维度。
5. 所有内容必须使用中文。";

            var url = ResolveChatCompletionsUrl(baseUrl);

            var requestBody = new
            {
                model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                max_tokens = 1500,
                temperature = 0.4
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Authorization", "Bearer " + apiKey);
            req.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                Encoding.UTF8,
                "application/json");

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(req, cancellationToken);
            }
            catch
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                return null;
            }

            content = content.Trim();
            if (content.StartsWith("```", StringComparison.Ordinal))
            {
                var start = content.IndexOf('{');
                var end = content.LastIndexOf('}');
                if (start >= 0 && end > start)
                {
                    content = content.Substring(start, end - start + 1);
                }
            }

            try
            {
                var root = JsonDocument.Parse(content).RootElement;
                var questions = new List<string>();
                if (root.TryGetProperty("questions", out var questionsElement) && questionsElement.ValueKind == JsonValueKind.Array)
                {
                    questions.AddRange(questionsElement.EnumerateArray()
                        .Select(x => x.GetString() ?? string.Empty)
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                }

                return new QuestionnaireGenerationResult
                {
                    Title = GetString(root, "title"),
                    Introduction = GetString(root, "introduction"),
                    Questions = questions
                };
            }
            catch
            {
                return null;
            }
        }

        private static QuestionnaireGenerationResult BuildFallbackQuestionnaire(string keyword, string requirements)
        {
            var topic = string.IsNullOrWhiteSpace(keyword) ? "糖尿病健康管理" : keyword;
            var introSuffix = string.IsNullOrWhiteSpace(requirements) ? "请结合患者实际情况如实填写。" : $"请结合以下要求填写：{requirements}";

            var questions = new List<string>
            {
                $"您目前最主要想咨询或评估的{topic}问题是什么？",
                "您最近一周是否出现不适症状？如果有，请简要说明。",
                "您目前是否正在接受相关治疗或护理？",
                "您近期是否有按时用药、复诊或执行健康管理计划？",
                "您的日常饮食、运动和作息情况如何？",
                "您是否存在影响病情管理的困难，例如疼痛、行动不便或情绪压力？",
                "您是否有既往相关疾病、并发症或家族史需要特别说明？",
                "您最近是否做过相关检查或监测？结果是否有异常？",
                "您希望医生重点帮助您解决哪些问题？",
                "除以上内容外，您还有哪些需要补充说明的情况？"
            };

            if (!string.IsNullOrWhiteSpace(requirements))
            {
                questions.Insert(1, $"根据“{requirements}”，目前最需要重点了解的情况是什么？");
            }

            return new QuestionnaireGenerationResult
            {
                Title = $"{topic}健康问卷",
                Introduction = $"{topic}问卷用于医生在门诊、随访或问诊前快速了解患者情况。{introSuffix}",
                Questions = questions.Take(12).ToList()
            };
        }

        private static string GetString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property)
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }
    }
}
