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
        var apiKey  = _configuration["AiSettings:ApiKey"];
        var model   = _configuration["AiSettings:Model"]   ?? "meta/llama-3.1-70b-instruct";
        var baseUrl = _configuration["AiSettings:BaseUrl"] ?? "https://integrate.api.nvidia.com/v1/chat/completions";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("AiSettings:ApiKey тохируулагдаагүй тул fallback хариулт ашиглаж байна.");
            return GetFallbackReply(messages);
        }

        try
        {
            var chatMessages = new List<object> { new { role = "system", content = systemPrompt } };
            foreach (var msg in messages)
                chatMessages.Add(new { role = msg.Role, content = msg.Content });

            var requestBody = new
            {
                model       = model,
                messages    = chatMessages,
                temperature = 0.35,   // ↓ Бодит дата дээр тулгуурласан хариултанд тогтвортой байдал чухал тул бууруулсан
                top_p       = 0.9,
                max_tokens  = 900,
                stream      = false
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, baseUrl)
            {
                Content = JsonContent.Create(requestBody)
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // ✅ Timeout — гадны API маш удаашрахаас хамгаалах
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(100));

            var response = await _httpClient.SendAsync(httpRequest, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("AI API алдаа: {Status} - {Body}", response.StatusCode, errorBody);
                return GetFallbackReply(messages);
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!json.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                _logger.LogError("AI API хариу хүлээгдэж байсан 'choices' талбаргүй ирлээ.");
                return GetFallbackReply(messages);
            }

            var reply = choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            return string.IsNullOrWhiteSpace(reply) ? GetFallbackReply(messages) : reply.Trim();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("AI API хүсэлт 25 секундийн дотор хариу өгсөнгүй (timeout).");
            return "Уучлаарай, AI сервер одоо удаашралтай байна. Түр хүлээгээд дахин оролдоно уу.";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AI provider-тэй сүлжээний холболт амжилтгүй боллоо.");
            return GetFallbackReply(messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI provider дуудлага амжилтгүй боллоо.");
            return GetFallbackReply(messages);
        }
    }

    /// <summary>
    /// AI provider ажиллахгүй тохиолдолд хэрэглэгчид ядаж чиглүүлэх энгийн хариулт.
    /// Жинхэнэ AI хариулт биш гэдгийг тодорхой илэрхийлнэ.
    /// </summary>
    private static string GetFallbackReply(List<(string Role, string Content)> messages)
    {
        var lastUserMessage = messages.LastOrDefault(m => m.Role == "user").Content ?? string.Empty;

        if (string.IsNullOrWhiteSpace(lastUserMessage))
            return "Сайн байна уу! Танд туслахад бэлэн байна, гэхдээ AI үйлчилгээ түр боломжгүй байна.";

        var lower = lastUserMessage.ToLowerInvariant();

        string note = "\n\n_(Тэмдэглэл: AI үйлчилгээ түр боломжгүй тул автомат хариулт өгч байна.)_";

        if (lower.Contains("амралт") || lower.Contains("leave") || lower.Contains("чөлөө"))
            return "Амралтын хүсэлт гаргахын тулд 'Амралт' хэсэгт орж шинэ хүсэлт үүсгэнэ үү. Үлдсэн амралтын хоногоо профайл хэсгээс шалгаж болно." + note;

        if (lower.Contains("ирц") || lower.Contains("attendance") || lower.Contains("check"))
            return "Ирцийн мэдээллээ 'Ирц' хэсгээс шалгаж болно. Ирэх, явах товчоор бүртгэл хийгдэнэ." + note;

        if (lower.Contains("цалин") || lower.Contains("salary"))
            return "Цалингийн мэдээллийн талаар дэлгэрэнгүй мэдэхийн тулд HR хэлтэстэй шууд холбогдоорой." + note;

        return $"Таны асуултыг хүлээн авлаа: \"{lastUserMessage}\". Одоогоор AI үйлчилгээ боломжгүй байна — HR-тэй холбогдож тодруулга авахыг зөвлөж байна." + note;
    }
}
