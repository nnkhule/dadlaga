using AttendanceSystem.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AttendanceSystem.Infrastructure.Services;

public class DefaultAiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DefaultAiProvider> _logger;

    public DefaultAiProvider(HttpClient httpClient, IConfiguration configuration, ILogger<DefaultAiProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> GenerateReplyAsync(
        string systemPrompt,
        List<(string Role, string Content)> messages,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["AiSettings:ApiKey"];
        var model = _configuration["AiSettings:Model"] ?? "meta/llama-3.1-70b-instruct";
        var baseUrl = _configuration["AiSettings:BaseUrl"] ?? "https://integrate.api.nvidia.com/v1/chat/completions";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return GetFallbackReply(messages);
        }

        try
        {
            // OpenAI-compatible chat completions формат
            var chatMessages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };

            foreach (var msg in messages)
            {
                chatMessages.Add(new { role = msg.Role, content = msg.Content });
            }

            var requestBody = new
            {
                model = model,
                messages = chatMessages,
                temperature = 0.5,
                top_p = 0.9,
                max_tokens = 1024,
                stream = false
            };

            var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = JsonContent.Create(requestBody)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("NVIDIA API алдаа: {Status} - {Body}", response.StatusCode, errorBody);
                return GetFallbackReply(messages);
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            // OpenAI формат: choices[0].message.content
            var reply = json.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(reply) ? GetFallbackReply(messages) : reply.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NVIDIA AI provider дуудлага амжилтгүй боллоо.");
            return GetFallbackReply(messages);
        }
    }

    private static string GetFallbackReply(List<(string Role, string Content)> messages)
    {
        var lastUserMessage = messages.LastOrDefault(m => m.Role == "user").Content ?? string.Empty;

        if (string.IsNullOrWhiteSpace(lastUserMessage))
            return "Сайн байна уу! Танд юугаар туслах вэ?";

        var lower = lastUserMessage.ToLowerInvariant();

        if (lower.Contains("амралт") || lower.Contains("leave") || lower.Contains("чөлөө"))
            return "Амралтын хүсэлт гаргахын тулд 'Амралт' хэсэгт орж шинэ хүсэлт үүсгээрэй. Үлдсэн амралтын хоногоо профайл хэсгээс шалгаж болно.";

        if (lower.Contains("ирц") || lower.Contains("attendance") || lower.Contains("check"))
            return "Ирцийн мэдээллээ 'Ирц' хэсгээс шалгах боломжтой. Ирэх, явахдаа товчоор бүртгэл хийгээрэй.";

        if (lower.Contains("цалин") || lower.Contains("salary"))
            return "Цалингийн мэдээллийн талаар дэлгэрэнгүйг HR хэлтэстэй холбогдож шалгаарай.";

        return $"Таны асуултыг хүлээн авлаа: \"{lastUserMessage}\". Одоогоор AI үйлчилгээ холбогдоогүй байна — HR-тэй холбогдож дэлгэрэнгүй мэдээлэл аваарай.";
    }
}