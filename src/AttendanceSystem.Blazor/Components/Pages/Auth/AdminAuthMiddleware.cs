namespace AttendanceSystem.AdminPanel;

/// <summary>
/// Session-д токен байгаа эсэхийг шалгаж, байхгүй бол login руу дамжуулна.
/// </summary>
public class AdminAuthMiddleware
{
    private static readonly string[] PublicPaths = ["/login", "/Login"];
    private readonly RequestDelegate _next;

    public AdminAuthMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value ?? "";
        bool isPublic = PublicPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));

        if (!isPublic)
        {
            var token = ctx.Session.GetString("AccessToken");
            if (string.IsNullOrEmpty(token))
            {
                ctx.Response.Redirect("/login");
                return;
            }

            // Токен хугацаа дуусчээ бол login руу
            var expiresStr = ctx.Session.GetString("ExpiresAt");
            if (DateTime.TryParse(expiresStr, out var expires) && expires < DateTime.UtcNow)
            {
                ctx.Session.Clear();
                ctx.Response.Redirect("/login?expired=1");
                return;
            }
        }

        await _next(ctx);
    }
}
