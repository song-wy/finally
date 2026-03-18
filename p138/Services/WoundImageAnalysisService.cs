using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DiabetesPatientApp.Services
{
    /// <summary>
    /// AI 对伤口照片的分析结果（用于伤口档案）
    /// </summary>
    public class WoundImageAnalysisResult
    {
        /// <summary>伤口位置及分级</summary>
        public string LocationAndGrade { get; set; } = string.Empty;
        /// <summary>创面大小</summary>
        public string WoundSize { get; set; } = string.Empty;
        /// <summary>创面外观</summary>
        public string WoundAppearance { get; set; } = string.Empty;
        /// <summary>周围皮肤（颜色、硬结等）</summary>
        public string SurroundingSkin { get; set; } = string.Empty;
        /// <summary>感染倾向</summary>
        public string InfectionTendency { get; set; } = string.Empty;
        /// <summary>清创方式建议</summary>
        public string DebridementSuggestion { get; set; } = string.Empty;
        /// <summary>换药频率建议</summary>
        public string DressingFrequencySuggestion { get; set; } = string.Empty;
        /// <summary>患者自我管理建议</summary>
        public string SelfManagementSuggestion { get; set; } = string.Empty;
    }

    public interface IWoundImageAnalysisService
    {
        /// <summary>
        /// 根据伤口照片（本地路径）调用 AI 视觉分析，返回伤口评估、感染倾向与建议。
        /// 若未配置 API 或文件不存在则返回 null。
        /// </summary>
        Task<WoundImageAnalysisResult?> AnalyzeImageAsync(string imageFilePath, CancellationToken cancellationToken = default);
    }

    public class WoundImageAnalysisService : IWoundImageAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public WoundImageAnalysisService(HttpClient httpClient, IConfiguration configuration)
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

            // If baseUrl already ends with /v1, append OpenAI-compatible path.
            if (trimmed.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                return trimmed + "/chat/completions";

            // Otherwise assume baseUrl is a host/root and add /v1/chat/completions.
            return trimmed + "/v1/chat/completions";
        }

        public async Task<WoundImageAnalysisResult?> AnalyzeImageAsync(string imageFilePath, CancellationToken cancellationToken = default)
        {
            // 伤口图片分析优先走独立的视觉模型配置（不影响现有 DeepSeek 文本分析配置）
            var apiKey = _configuration["VisionAI:ApiKey"]?.Trim();
            var baseUrl = _configuration["VisionAI:BaseUrl"]?.Trim();
            var model = _configuration["VisionAI:Model"]?.Trim();

            // 兼容旧配置：如果未配置 VisionAI，则回退到 OpenAI 节点（但 DeepSeek 通常不支持 image_url）
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(model))
            {
                apiKey = _configuration["OpenAI:ApiKey"]?.Trim();
                baseUrl = _configuration["OpenAI:BaseUrl"]?.Trim();
                model = _configuration["OpenAI:Model"]?.Trim();
            }

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(model))
                return null;

            if (string.IsNullOrEmpty(imageFilePath) || !File.Exists(imageFilePath))
                return null;

            byte[] bytes = await File.ReadAllBytesAsync(imageFilePath, cancellationToken);
            string base64 = Convert.ToBase64String(bytes);
            string mimeType = "image/jpeg";
            string ext = Path.GetExtension(imageFilePath).ToLowerInvariant();
            if (ext == ".png") mimeType = "image/png";
            else if (ext == ".gif") mimeType = "image/gif";
            else if (ext == ".webp") mimeType = "image/webp";
            string dataUrl = $"data:{mimeType};base64,{base64}";

            var prompt = @"你是一位伤口护理专家。请根据这张患者提交的足部伤口照片，用中文给出简要、专业的评估与建议。针对足部创面（如足底、足跟、足背、趾间等）进行分析。输出一段完整的 JSON（不要 markdown 代码块，不要多余说明），键名与含义如下：
""location_and_grade"": 伤口位置及分级（如足底、足跟、足背、趾间等具体位置，以及若可推断的分级）。
""wound_size"": 创面大小（可描述长宽或面积、深度等，若无法精确则描述大致范围）。
""wound_appearance"": 创面外观（色泽、渗液、坏死组织、肉芽等）。
""surrounding_skin"": 周围皮肤情况（颜色、硬结、红肿、浸渍等）。
""infection_tendency"": 感染倾向评估（低/中/高及简要理由）。
""debridement_suggestion"": 清创方式建议（如机械清创、自溶性等，针对足部伤口的一两句话）。
""dressing_frequency_suggestion"": 换药频率建议（如每日一次、每两日一次等）。
""self_management_suggestion"": 患者自我管理建议（保护创面、抬高患肢、减少负重、营养、何时复诊等，两三句话）。";

            var url = ResolveChatCompletionsUrl(baseUrl);

            var requestBody = new
            {
                model,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "text", text = prompt },
                            new { type = "image_url", image_url = new { url = dataUrl } }
                        }
                    }
                },
                max_tokens = 1500,
                temperature = 0.3
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Add("Authorization", "Bearer " + apiKey);
            req.Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8,
                "application/json");

            HttpResponseMessage res;
            try
            {
                res = await _httpClient.SendAsync(req, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("视觉AI接口请求失败：" + ex.Message, ex);
            }

            if (!res.IsSuccessStatusCode)
            {
                var err = await res.Content.ReadAsStringAsync(cancellationToken);
                if (string.IsNullOrWhiteSpace(err)) err = "(无返回内容)";
                if (err.Length > 800) err = err.Substring(0, 800) + "…";
                throw new InvalidOperationException($"视觉AI接口返回失败：HTTP {(int)res.StatusCode} {res.ReasonPhrase}；{err}");
            }

            var json = await res.Content.ReadAsStringAsync(cancellationToken);
            var doc = JsonDocument.Parse(json);
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
                int start = content.IndexOf('{');
                int end = content.LastIndexOf('}');
                if (start >= 0 && end > start)
                    content = content.Substring(start, end - start + 1);
            }

            try
            {
                var root = JsonDocument.Parse(content).RootElement;
                return new WoundImageAnalysisResult
                {
                    LocationAndGrade = GetStr(root, "location_and_grade"),
                    WoundSize = GetStr(root, "wound_size"),
                    WoundAppearance = GetStr(root, "wound_appearance"),
                    SurroundingSkin = GetStr(root, "surrounding_skin"),
                    InfectionTendency = GetStr(root, "infection_tendency"),
                    DebridementSuggestion = GetStr(root, "debridement_suggestion"),
                    DressingFrequencySuggestion = GetStr(root, "dressing_frequency_suggestion"),
                    SelfManagementSuggestion = GetStr(root, "self_management_suggestion")
                };
            }
            catch
            {
                return null;
            }
        }

        private static string GetStr(JsonElement el, string key)
        {
            if (el.TryGetProperty(key, out var p))
                return p.GetString() ?? string.Empty;
            return string.Empty;
        }
    }
}
