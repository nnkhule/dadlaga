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
        var apiKey = FirstNonEmpty(
            _configuration["AiSettings:ApiKey"],
            _configuration["AiSettings__ApiKey"],
            _configuration["NVIDIA_API_KEY"],
            Environment.GetEnvironmentVariable("AiSettings__ApiKey"),
            Environment.GetEnvironmentVariable("NVIDIA_API_KEY"));
        var model   = _configuration["AiSettings:Model"]   ?? "qwen/qwen3.5-122b-a10b";
        var baseUrl = _configuration["AiSettings:BaseUrl"] ?? "https://integrate.api.nvidia.com/v1/chat/completions";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogError("AI provider is not configured: NVIDIA_API_KEY or AiSettings:ApiKey is missing.");
            return "AI үйлчилгээний NVIDIA API түлхүүр тохируулагдаагүй байна. NVIDIA_API_KEY орчны хувьсагч эсвэл AiSettings:ApiKey тохируулаад API серверээ дахин эхлүүлнэ үү.";
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
                temperature = 0.45,   // ↑ Дата лавлагаанаас гадна HR зөвлөгөө/тооцоолол хариулдаг тул бага зэрэг уян хатан
                top_p       = 0.9,
                max_tokens  = 512,
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
            cts.CancelAfter(TimeSpan.FromSeconds(45));

            var response = await _httpClient.SendAsync(httpRequest, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "AI API returned HTTP {StatusCode} for model {Model}.",
                    (int)response.StatusCode,
                    model);
                return GetProviderErrorReply(response.StatusCode, model);
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!json.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                _logger.LogError("AI API хариу хүлээгдэж байсан 'choices' талбаргүй ирлээ.");
                return "AI provider-ийн хариу танигдахгүй байна. Загварын endpoint болон серверийн тохиргоог шалгана уу.";
            }

            var reply = choices[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(reply))
            {
                _logger.LogError("AI API returned an empty reply.");
                return "AI provider хоосон хариу буцаалаа. Дахин оролдоно уу.";
            }

            return reply.Trim();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("AI API хүсэлт 45 секундийн дотор хариу өгсөнгүй (timeout).");
            return "Уучлаарай, AI сервер одоо удаашралтай байна. Түр хүлээгээд дахин оролдоно уу.";
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AI provider-тэй сүлжээний холболт амжилтгүй боллоо.");
            return "AI үйлчилгээний серверт холбогдож чадсангүй. Сүлжээ болон AI provider-ийн хаягийг шалгаад дахин оролдоно уу.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI provider дуудлага амжилтгүй боллоо.");
            return "AI үйлчилгээ одоогоор хариу боловсруулах боломжгүй байна. Серверийн log-ийг шалгаад дахин оролдоно уу.";
        }
    }

    private static string GetProviderErrorReply(System.Net.HttpStatusCode statusCode, string model)
    {
        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden =>
                "AI үйлчилгээний API түлхүүр хүчингүй эсвэл энэ загварт хандах эрхгүй байна. NVIDIA_API_KEY болон загварын эрхийг шалгана уу.",
            System.Net.HttpStatusCode.TooManyRequests =>
                "AI үйлчилгээний хүсэлтийн хязгаар эсвэл кредит дууссан байна. Provider-ийн usage/billing тохиргоог шалгана уу.",
            System.Net.HttpStatusCode.Gone =>
                $"NVIDIA API энэ model-ийг ашиглах боломжгүй гэж буцаалаа: {model}. AI тохиргооны model болон API access-ийг шалгана уу.",
            System.Net.HttpStatusCode.NotFound =>
                $"NVIDIA API тохируулсан model эсвэл endpoint-ийг олсонгүй: {model}. AI тохиргоог шалгана уу.",
            System.Net.HttpStatusCode.BadRequest =>
                "AI үйлчилгээ хүсэлтийг хүлээн авсангүй. Загварын нэр болон provider-ийн тохиргоог шалгана уу.",
            _ when (int)statusCode >= 500 =>
                "AI provider дээр түр саатал гарлаа. Хэсэг хүлээгээд дахин оролдоно уу.",
            _ =>
                $"AI үйлчилгээ хүсэлтийг боловсруулж чадсангүй (HTTP {(int)statusCode}). Серверийн log-ийг шалгана уу."
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

}
