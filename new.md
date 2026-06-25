# AttendanceSystem — Бүрэн Дүн Шинжилгээний Тайлан
**Senior Full Stack .NET Developer & Software Architect**
**Огноо:** 2026-06-16

---

## A. Төслийн Ерөнхий Тойм

### Архитектур
```
AttendanceSystem/
├── src/
│   ├── AttendanceSystem.Domain          # Entities, Enums, Events (Clean Architecture)
│   ├── AttendanceSystem.Application     # CQRS Handlers, Interfaces, DTOs, Services
│   ├── AttendanceSystem.Infrastructure  # EF Core, Identity, JWT, AI, Repositories
│   ├── AttendanceSystem.API             # ASP.NET Core 8 Web API (JWT Bearer)
│   ├── AttendanceSystem.Blazor          # Blazor Server (.NET 10, InteractiveServer)
│   ├── AttendanceSystem.AdminPanel      # ⚠️ ТАСАРХАЙ, solution-д байхгүй
│   └── AttendanceSystem.Mobile          # ⚠️ HTML файл ганцаараа, csproj хоосон
└── tests/
    ├── AttendanceSystem.UnitTests
    └── AttendanceSystem.IntegrationTests
```

### Технологийн Stack
- **Backend:** ASP.NET Core 8, EF Core, ASP.NET Identity, MediatR (CQRS), JWT Bearer
- **Frontend:** Blazor Server (.NET 10), Bootstrap Icons, Chart.js
- **Database:** MSSQL (Docker), Redis (cache)
- **AI:** NVIDIA NIM API (LLaMA 3.1 70B), Fallback режим
- **Бусад:** Serilog, OpenTelemetry, Hangfire, AspNetCoreRateLimit

### Ажилладаг портууд
| Апп | URL |
|-----|-----|
| API | `https://localhost:7000` |
| Blazor | `https://localhost:7112` / `http://localhost:5285` |

---

## B. Admin Login — Үндсэн Шалтгааны Шинжилгээ

### 🔴 ҮНДСЭН ШАЛТГААН: Миграцийн Timestamp Зөрчил

**Файл:** `src/AttendanceSystem.Infrastructure/Migrations/20240101000000_FixSchemaIssues.cs`

EF Core-ийн `MigrateAsync()` нь миграцуудыг **файлын нэрийн цагийн дарааллаар** гүйцэтгэдэг:

```
Одоогийн гүйцэтгэлийн дараалал (БУРУУ):
1. 20240101000000_FixSchemaIssues       ← ЭХЛЭЭД гүйдэг
2. 20260602042754_InitialCreate         ← 2-рт гүйдэг  ← хүснэгтүүдийг ЭНД үүсгэдэг
3. 20260603012020_FixRolePermissionKey
4. 20260611071000_AddPasswordResetToken
```

`FixSchemaIssues` нь эхэнд гүйхдээ шууд **хүснэгт олдохгүй алдаа** гаргадаг:

```sql
-- 20240101000000_FixSchemaIssues.cs дотор:
IF EXISTS (SELECT 1 FROM AttendanceRecords GROUP BY EmployeeId, [Date] ...)
-- ⬆️ "Invalid object name 'AttendanceRecords'" алдаа!
-- Учир нь AttendanceRecords нь InitialCreate-д үүсдэг, 
-- харин InitialCreate нь ДАРАА гүйдэг.
```

**Гинжин урвал:**
```
MigrateAsync() → FixSchemaIssues гүйнэ → SqlException алдаа → 
SeedAsync() дуусахгүй → SuperAdmin хэзээ ч үүсэхгүй → 
FindByEmailAsync("admin@attendance.local") = null → 
Login → 401 Unauthorized
```

### 🟠 ХОЁРДУГААР ШАЛТГААН: Seeder-ийн Нөхцөлт Хамгаалалт

**Файл:** `src/AttendanceSystem.Infrastructure/Persistence/Seed/ApplicationDbSeeder.cs`, мөр 30

```csharp
// БУРУУ: Admin үүсгэх код нь Department шалгалтын ДООР байдаг
if (!await context.Departments.AnyAsync())   // ← гацуур!
{
    // Department, WorkSchedule үүсгэх...
    
    // SuperAdmin үүсгэх код эндээс эхэлдэг
    const string adminEmail = "admin@attendance.local";
    if (await userManager.FindByEmailAsync(adminEmail) is null)
    {
        // admin үүсгэх...
    }
}
```

**Асуудал:** Хэрэв DB-д Department аль хэдийн байвал (өмнөх хэсэгчилсэн seed-ийн улмаас), бүх блок алгасагдаж, SuperAdmin **хэзээ ч дахин үүсэхгүй**.

### 🟡 ГУРАВДУГААР ШАЛТГААН: `AdminPanel` namespace-ийн Blazor дотор орсон файлууд

**8 файл** `src/AttendanceSystem.Blazor/Components/Pages/Admin/` дотор байгаа бөгөөд `namespace AttendanceSystem.AdminPanel.Pages` гэж тунхаглагдсан:

```
Attendance.cshtml.cs, Dashboard.cshtml.cs, Departments.cshtml.cs,
Employees.cshtml.cs, Leave.cshtml.cs, Reports.cshtml.cs,
Settings.cshtml.cs, login.cshtml.cs
```

Эдгээр нь Blazor-ийн `AddRazorComponents()` pipeline-д **хэзээ ч ажиллахгүй** бөгөөд build хийхэд `IHttpClientFactory` (registered хийгээгүй), `IConfiguration` зэрэг байхгүй dependency-уудыг шаарддаг. **Build-ийн явцад implicit оролцоо** нь хэрэглэгчийг андуурах эрсдэлтэй.

---

## C. Яг Засварлах Кодууд

### ЗАСВАР 1 — Миграцийн Timestamp засах (КРИТИК)

**Файл нэрийг өөрчил:**
```
ХУУЧИН: 20240101000000_FixSchemaIssues.cs
ШИНЭ:   20260604000000_FixSchemaIssues.cs
```

**Файл 1: Хуучин файлыг устга, шинийг үүсгэ**

`src/AttendanceSystem.Infrastructure/Migrations/20260604000000_FixSchemaIssues.cs`:
```csharp
// Файлын агуулга яг ижил хэвээр — зөвхөн нэр өөрчлөгдөнө
// Хуучин файлыг устга:
// src/AttendanceSystem.Infrastructure/Migrations/20240101000000_FixSchemaIssues.cs

// Мөн ApplicationDbContextModelSnapshot.cs дотор MigrationId-г шинэчил:
// "20240101000000_FixSchemaIssues" → "20260604000000_FixSchemaIssues"
```

**Файл 2: Designer файлыг шинэчил**

`20260604000000_FixSchemaIssues.Designer.cs`:
```csharp
// Хуучин 20240101000000_FixSchemaIssues.Designer.cs файлыг хуулаад,
// нэр болон MigrationAttribute-г өөрчил:
[DbContext(typeof(ApplicationDbContext))]
[Migration("20260604000000_FixSchemaIssues")]  // ← энэ мөр
partial class FixSchemaIssues { }
```

**Файл 3: Snapshot дотор MigrationId шинэчил**

`src/AttendanceSystem.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs`, 1-р мөр орчим:
```csharp
// ХАЙХ:
[Migration("20240101000000_FixSchemaIssues")]
// СОЛИХ:
[Migration("20260604000000_FixSchemaIssues")]
```

---

### ЗАСВАР 2 — Seeder-ийг Idempotent болго (КРИТИК)

**Файл:** `src/AttendanceSystem.Infrastructure/Persistence/Seed/ApplicationDbSeeder.cs`

```csharp
public static async Task SeedAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("ApplicationDbSeeder");
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

    await RepairLegacySchemaAsync(context);
    await context.Database.MigrateAsync();

    // ── Roles ───────────────────────────────────────────────────────────
    foreach (var (name, desc) in RoleDefinitions.All)
    {
        if (!await roleManager.RoleExistsAsync(name))
            await roleManager.CreateAsync(new ApplicationRole { Name = name, Description = desc });
    }

    // ── SuperAdmin — DEPARTMENTS-ийн ГАДНА, ТУСДАА ШАЛГАЛТТАЙ ──────────
    // ⬇️  Энэ бол үндсэн засвар — if (!Departments.Any()) блокоос ГАРСАН
    const string adminEmail = "admin@attendance.local";
    if (await userManager.FindByEmailAsync(adminEmail) is null)
    {
        logger.LogInformation("SuperAdmin хэрэглэгч үүсгэж байна...");
        var admin = new ApplicationUser
        {
            UserName    = adminEmail,
            Email       = adminEmail,
            FullName    = "System Administrator",
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(admin, "Admin@12345!");
        if (createResult.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "SuperAdmin");
            logger.LogInformation("SuperAdmin амжилттай үүслээ: {Email}", adminEmail);
        }
        else
        {
            logger.LogError("SuperAdmin үүсгэж чадсангүй: {Errors}",
                string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }
    }

    // ── Departments, WorkSchedule, OfficeLocation ────────────────────────
    if (!await context.Departments.AnyAsync())
    {
        var schedule = WorkSchedule.CreateStandard();
        var office   = OfficeLocation.Create("Head Office Ulaanbaatar", 47.9123, 106.9308, 100);
        var dept     = Department.Create("Human Resources");
        context.WorkSchedules.Add(schedule);
        context.OfficeLocations.Add(office);
        context.Departments.Add(dept);
        await context.SaveChangesAsync();

        // ── Sample Employee ──────────────────────────────────────────────
        var employee = Employee.Create(
            "EMP001", "Б.Бат", "bat@attendance.local",
            dept.Id, schedule.Id, office.Id,
            DateOnly.FromDateTime(DateTime.Now.AddYears(-1)),
            ContractType.FullTime,
            new DateOnly(1990, 5, 15));
        context.Employees.Add(employee);
        await context.SaveChangesAsync();

        const string empEmail = "bat@attendance.local";
        if (await userManager.FindByEmailAsync(empEmail) is null)
        {
            var empUser = new ApplicationUser
            {
                UserName = empEmail, Email = empEmail,
                FullName = employee.FullName,
                EmployeeId = employee.Id,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(empUser, "Employee@12345!");
            await userManager.AddToRoleAsync(empUser, "Employee");
            employee.LinkUser(empUser.Id);
            await context.SaveChangesAsync();
        }

        SeedMongolianHolidays(context);
        await context.SaveChangesAsync();
    }

    logger.LogInformation("Database seed дууслаа.");
}
```

---

### ЗАСВАР 3 — Admin Pages дээр `[Authorize]` нэмэх

**Файл:** `src/AttendanceSystem.Blazor/Components/Pages/Admin/Attendance.razor`
```razor
@page "/admin/attendance"
@layout AdminLayout
@attribute [Authorize(Roles = "Admin,SuperAdmin,HRManager")]   ← НЭМ
@inject ApiClient Api
```

**Файл:** `src/AttendanceSystem.Blazor/Components/Pages/Admin/Employees.razor`
```razor
@page "/admin/employees"
@layout AdminLayout
@attribute [Authorize(Roles = "Admin,SuperAdmin,HRManager")]   ← НЭМ
@inject ApiClient Api
```

**Файл:** `src/AttendanceSystem.Blazor/Components/Pages/Admin/Leave.razor`
```razor
@page "/admin/leave"
@layout AdminLayout
@attribute [Authorize(Roles = "Admin,SuperAdmin,HRManager")]   ← НЭМ
@inject ApiClient Api
```

**Файл:** `src/AttendanceSystem.Blazor/Components/Pages/Admin/Reports.razor`
```razor
@page "/admin/reports"
@layout AdminLayout
@attribute [Authorize(Roles = "Admin,SuperAdmin,HRManager")]   ← НЭМ
@inject ApiClient Api
```

**Файл:** `src/AttendanceSystem.Blazor/Components/Pages/Admin/AiChat.razor`
```razor
@page "/admin/ai-chat"
@layout AdminLayout                                            ← НЭМ
@attribute [Authorize(Roles = "Admin,SuperAdmin,HRManager")]   ← НЭМ
```

---

### ЗАСВАР 4 — AiController дахь Role нэрийг нэгдсэн болго

**Файл:** `src/AttendanceSystem.API/Controllers/AiController.cs`

```csharp
// БУРУУ — "Admin", "HR" гэсэн role-ууд DB-д байхгүй
[Authorize(Roles = "Admin,HR")]
public async Task<ActionResult<ChatResponseDto>> AdminChat(...)

// ЗӨВШӨӨРӨГДСӨН role-ууд: "SuperAdmin", "HRManager", "DepartmentHead", "Employee", "Auditor"
// ЗАСВАР:
[Authorize(Roles = "SuperAdmin,HRManager,DepartmentHead")]
public async Task<ActionResult<ChatResponseDto>> AdminChat(...)

[Authorize(Roles = "Employee,SuperAdmin,HRManager,DepartmentHead")]
public async Task<ActionResult<ChatResponseDto>> EmployeeChat(...)
```

---

### ЗАСВАР 5 — Шинэ DB эсвэл seed дутуу үед хурдан засах SQL

Хэрэв яаралтай тохиолдолд SuperAdmin-ийг гараар үүсгэх хэрэгтэй бол (application давхаргаас гадна):

```sql
-- SSMS эсвэл Azure Data Studio-д шууд ажиллуулна
-- 1. Role байгаа эсэх шалгах
SELECT Id, Name FROM AspNetRoles WHERE Name = 'SuperAdmin';

-- 2. User байгаа эсэх шалгах  
SELECT Id, Email FROM AspNetUsers WHERE Email = 'admin@attendance.local';

-- 3. User байвал role-той холбогдсон эсэх
SELECT u.Email, r.Name 
FROM AspNetUsers u
JOIN AspNetUserRoles ur ON u.Id = ur.UserId
JOIN AspNetRoles r ON ur.RoleId = r.Id
WHERE u.Email = 'admin@attendance.local';
```

---

## D. Ашиглагдаагүй Файлуудын Жагсаалт

### 1. Blazor дотор нуугдсан AdminPanel PageModel-ууд (БҮГД устгаж болно)

| Файл | Шалтгаан | Устгаж болох? |
|------|----------|---------------|
| `src/AttendanceSystem.Blazor/Components/Pages/Admin/login.cshtml.cs` | `namespace AttendanceSystem.AdminPanel.Pages` — Blazor project-д `AddRazorPages()` дуудагдаагүй, matching `.cshtml` файл байхгүй | ✅ Тийм |
| `src/AttendanceSystem.Blazor/Components/Pages/Admin/Attendance.cshtml.cs` | Мөн нөхцөл — PageModel without Razor Page | ✅ Тийм |
| `src/AttendanceSystem.Blazor/Components/Pages/Admin/Dashboard.cshtml.cs` | Мөн нөхцөл | ✅ Тийм |
| `src/AttendanceSystem.Blazor/Components/Pages/Admin/Departments.cshtml.cs` | Мөн нөхцөл | ✅ Тийм |
| `src/AttendanceSystem.Blazor/Components/Pages/Admin/Employees.cshtml.cs` | Мөн нөхцөл | ✅ Тийм |
| `src/AttendanceSystem.Blazor/Components/Pages/Admin/Leave.cshtml.cs` | Мөн нөхцөл | ✅ Тийм |
| `src/AttendanceSystem.Blazor/Components/Pages/Admin/Reports.cshtml.cs` | Мөн нөхцөл | ✅ Тийм |
| `src/AttendanceSystem.Blazor/Components/Pages/Admin/Settings.cshtml.cs` | Мөн нөхцөл | ✅ Тийм |
| `src/AttendanceSystem.Blazor/Components/Pages/Auth/AdminAuthMiddleware.cs` | `namespace AttendanceSystem.AdminPanel` — Blazor `Program.cs`-д `UseMiddleware<AdminAuthMiddleware>()` хэзээ ч дуудагдаагүй | ✅ Тийм |

### 2. AttendanceSystem.AdminPanel — Бүх Төсөл Тасархай

| Файл | Шалтгаан | Устгаж болох? |
|------|----------|---------------|
| `src/AttendanceSystem.AdminPanel/Program.cs` | Solution `.sln`-д бүртгэлгүй, `AddRazorPages()` л дуудаад дуусдаг, `AddHttpClient`, `AddSession` зэрэг шаардлагатай service-ууд байхгүй | ✅ Тийм |
| `src/AttendanceSystem.AdminPanel/Pages/login.cshtml.cs` | AdminPanel `Program.cs`-д `IHttpClientFactory` registered хийгээгүй | ✅ Тийм |
| `src/AttendanceSystem.AdminPanel/Pages/login.cshtml` | Харьцах PageModel ажиллахгүй | ✅ Тийм |
| `src/AttendanceSystem.AdminPanel/Pages/_ViewImports.cshtml` | Ашиглагдаагүй | ✅ Тийм |
| `src/AttendanceSystem.AdminPanel/AttendanceSystem.AdminPanel.csproj` | Solution-д байхгүй | ✅ Тийм |

### 3. AttendanceSystem.Mobile — Бараг Хоосон

| Файл | Шалтгаан | Устгаж болох? |
|------|----------|---------------|
| `src/AttendanceSystem.Mobile/` (бүхэлдээ) | Зөвхөн `index.html`, `login.html`, `css/`, `js/` файл бүхий статик HTML прототип. `.csproj` хоосон, solution-д бүртгэлгүй. | ✅ Тийм (эсвэл тусдаа repo-д зөөх) |

### 4. EnterpriseApiControllers — Ажиллахгүй Placeholder-ууд

**Файл:** `src/AttendanceSystem.API/Controllers/EnterpriseApiControllers.cs`

`AuthV1Controller`, болон бусад `*V1Controller` class-ууд нь бүгд `ContractControllerBase.Contract(...)` буцааж, `501 Not Implemented` хариу өгдөг. Хэрэглэгчдэд expose хийхгүй бол:

| Controller | Шалтгаан | Устгаж болох? |
|------------|----------|---------------|
| `AuthV1Controller` | 501 stub | ⚠️ Болгоомжтой (production-д unimplemented endpoint-ууд harness болж болно) |
| Бусад V1 stub-ууд | 501 stub | ⚠️ Болгоомжтой |

### 5. Blazor дотор давхардсан Layout / Page-ууд

| Файл | Шалтгаан | Устгаж болох? |
|------|----------|---------------|
| `Components/Layout/Footer.razor` | `MainLayout.razor`-д шууд footer HTML байгаа тул `Footer.razor` хаана ч inject хийгдээгүй | ✅ Тийм |
| `Components/Layout/Sidebar.razor` | `AdminLayout.razor` өөрийн sidebar-тай, `Sidebar.razor` ашиглагдаагүй | ✅ Тийм |
| `Components/Layout/Navbar.razor` | `MainLayout.razor` дотор navbar шууд байгаа, `Navbar.razor` ашиглагдаагүй | ✅ Тийм |
| `Components/Pages/Dashboard/Dashboard.razor` | `/dashboard` route-тай, харин `/admin/dashboard` нь өөр файл. `/dashboard` хандах user flow байхгүй | ⚠️ Шалгах |
| `Components/Pages/Reports/Reports.razor` | `@page "/reports"` — Employee layout-тай харин admin `/admin/reports` тусдаа файл. Давхардал | ⚠️ Шалгах |
| `Components/Pages/Settings/Settings.razor` | Мөн давхардал `/settings` vs `/admin/settings` | ⚠️ Шалгах |
| `Components/Pages/Notifications/Notifications.razor` | `/notifications` route, Employee flow-д бүртгэгдсэн эсэх тодорхойгүй | ⚠️ Шалгах |

### 6. Ашиглагдаагүй DTOs / Models

| Файл | Шалтгаан | Устгаж болох? |
|------|----------|---------------|
| `src/AttendanceSystem.API/Dtos/EmployeeStatisticsDto.cs` | `EmployeeStatisticsService` + `EmployeeStatisticsController`-д ашиглагддаг ч `/api/v1/statistics/employee` хаа нэг API controller-д implement хийгдэхгүй stub | ⚠️ Болгоомжтой |
| `src/AttendanceSystem.Blazor/Models/DashboardStats.cs` | `ApiModels.cs`-д `DashboardSummaryDto` байгаа тул давхардал | ⚠️ Шалгах |

---

## E. Admin Module Дутуу Функциональ Байдал

### 🔴 Критик (Заавал хэрэгтэй)

| # | Дутуу зүйл | Тайлбар |
|---|-----------|---------|
| 1 | **Role Management хуудас** | DB-д `Roles`, `RolePermission` хүснэгт бий, харин admin UI дээр role харах, засах, хэрэглэгчид оноох боломжгүй |
| 2 | **Employee-д Account үүсгэх UI** | `POST /api/auth/setup-employee-account` endpoint бий ч Blazor admin хуудсанд товч / form байхгүй |
| 3 | **Leave Request Approve/Reject** | `Leave.razor` нь `ApproveLeaveRequestAsync(id)` / `RejectLeaveRequestAsync(id)` дуудаж болох ч UI button-ууд байхгүй |
| 4 | **Department CRUD** | `DepartmentsApiController` хаана ч алга — GET/POST/PUT/DELETE endpoint байхгүй. Admin sidebar-д Department цэс байхгүй |

### 🟠 Өндөр Тэргүүлэлттэй

| # | Дутуу зүйл | Тайлбар |
|---|-----------|---------|
| 5 | **Attendance Manual Adjustment** | `TimeAdjustmentRequest` entity DB-д бий, харин admin UI болон API controller алга |
| 6 | **Employee деталь / засах** | `EmployeeDetails.razor`, `EmployeeEdit.razor` байхгүй. `EmployeeCreate.razor` бий ч admin panel-д харагдахгүй |
| 7 | **Statistics Dashboard** | `DashboardApiController` нь `/api/dashboard/summary`, `/statistics`, `/recent-activities` буцаадаг ч overtime, tardiness trend, department breakdown chart алга |
| 8 | **Notification send UI** | Admin нь notification илгээх боломжгүй — `NotificationsApiController` endpoint бий |

### 🟡 Дунд Тэргүүлэлттэй

| # | Дутуу зүйл | Тайлбар |
|---|-----------|---------|
| 9 | **Audit Log харах** | `AuditLog` entity, `AuditLogConfiguration` бий — харах UI байхгүй |
| 10 | **Holiday / Calendar** | `Holiday` entity, Mongolian holiday seed бий — admin-аас засах боломжгүй |
| 11 | **Work Schedule засах** | `WorkSchedule` entity бий — admin UI алга |
| 12 | **CSV Export** | `ReportsApiController` байж болзошгүй ч frontend дээр download товч алга |
| 13 | **Form validation** | `Employees.razor`, `Leave.razor` дотор client-side validation алга |

### 🟢 Бага Тэргүүлэлттэй

| # | Дутуу зүйл | Тайлбар |
|---|-----------|---------|
| 14 | **Dark mode toggle** | `ThemeService` бий ч admin layout-д toggle UI алга |
| 15 | **Pagination** | `Pagination.razor` shared component бий ч admin pages ашиглахгүй |
| 16 | **Suspicious Activity alerts** | `SuspiciousActivityAlert` entity — UI алга |

---

## F. AI Chat — Database Integration Дизайн

### Одоогийн Байдал (ХАНГАЛТТАЙ)

`AiChatService` нь **аль хэдийн** database-ийн өгөгдлийг ашиглаж байна:
- `BuildAdminContextAsync()` — нийт ажилтан, ирц, чөлөө, хэлтэс татна
- `BuildEmployeeContextAsync()` — хувийн ирц, чөлөөний түүх, balance татна

### Одоогийн Асуудлууд

#### 1. AiController-ийн буруу Route

```csharp
// ApiClient.cs (Blazor):
public Task<ChatResponseDto?> PostChatAsync(ChatRequestDto request, ...)
    => ... PostAsync("api/ai/chat", request, ...);   // ← /api/ai/chat

// AiController.cs (API):
[HttpPost("admin/chat")]   // ← /api/ai/admin/chat  ← ТОХИРОХГҮЙ!
[HttpPost("employee/chat")] // ← /api/ai/employee/chat
```

**Засвар:** `ApiClient.cs`-ийг шинэчил:

```csharp
// src/AttendanceSystem.Blazor/Services/ApiClient.cs
public async Task<ChatResponseDto?> PostChatAsync(
    ChatRequestDto request, bool isAdmin = false, 
    CancellationToken cancellationToken = default)
{
    var endpoint = isAdmin ? "api/ai/admin/chat" : "api/ai/employee/chat";
    var response = await PostAsync(endpoint, request, cancellationToken);
    if (!response.IsSuccessStatusCode) return null;
    return await response.Content.ReadFromJsonAsync<ChatResponseDto>(
        cancellationToken: cancellationToken);
}
```

#### 2. Admin AiChat.razor — Layout болон Auth дутуу

```razor
@page "/admin/ai-chat"
// ← AdminLayout байхгүй!
// ← [Authorize] байхгүй!
// ← HttpClient шууд inject хийсэн (ApiClient бус)
```

#### 3. AdminAiContextDto — Нарийн мэдээлэл дутуу

```csharp
// Одоо:
TotalEmployees, PresentToday, AbsentToday, OnLeaveToday,
PendingLeaveRequests, DepartmentNames

// Нэмэх хэрэгтэй:
TodayLateCount,
WeeklyAttendanceRate (сүүлийн 7 хоног),
DepartmentBreakdown (хэлтэс тус бүрийн ирц),
RecentAbnormalActivities (хоцорч ирэх чиг хандлага)
```

### Шинэ RAG Архитектур

```
User Question
     ↓
[AiController.AdminChat / EmployeeChat]
     ↓
[AiChatService]
     ├── BuildAdminContextAsync()     ← EF Core queries
     │     ├── Employees count/active
     │     ├── AttendanceRecords (today + 7-day trend)
     │     ├── LeaveRequests (pending + today on-leave)
     │     └── Department breakdown
     │
     └── BuildEmployeeContextAsync()  ← personal data only
           ├── Employee profile + department
           ├── AttendanceRecords (last 7 days)
           ├── LeaveRequests (history + balance)
           └── LateCount this month
     ↓
[System Prompt + DB Context + Chat History]
     ↓
[DefaultAiProvider → NVIDIA NIM API]
     ↓
ChatResponseDto
```

### Хэрэгжүүлэх шаардлагатай өөрчлөлтүүд

#### Файл 1: `AiChatService.cs` — Department Breakdown нэмэх

```csharp
private async Task<AdminAiContextDto> BuildAdminContextAsync(CancellationToken cancellationToken)
{
    var today = DateOnly.FromDateTime(DateTime.UtcNow);
    var sevenDaysAgo = today.AddDays(-7);

    var totalEmployees = await _dbContext.Employees
        .CountAsync(e => e.IsActive, cancellationToken);
    
    var presentToday = await _dbContext.AttendanceRecords
        .Where(a => a.Date == today && a.Status == AttendanceStatus.Present)
        .Select(a => a.EmployeeId).Distinct().CountAsync(cancellationToken);
    
    var lateToday = await _dbContext.AttendanceRecords
        .Where(a => a.Date == today && a.Status == AttendanceStatus.Late)
        .Select(a => a.EmployeeId).Distinct().CountAsync(cancellationToken);
    
    var onLeaveToday = await _dbContext.LeaveRequests
        .Where(l => l.Status == RequestStatus.Approved 
                 && l.StartDate <= today && l.EndDate >= today)
        .Select(l => l.EmployeeId).Distinct().CountAsync(cancellationToken);
    
    var pendingLeaveRequests = await _dbContext.LeaveRequests
        .CountAsync(l => l.Status == RequestStatus.Pending, cancellationToken);
    
    // Хэлтэс тус бүрийн өнөөдрийн ирц
    var deptBreakdown = await _dbContext.Departments
        .Select(d => new
        {
            d.Name,
            Total = d.Employees.Count(e => e.IsActive),
            Present = d.Employees
                .Where(e => e.AttendanceRecords
                    .Any(a => a.Date == today && a.Status == AttendanceStatus.Present))
                .Count()
        })
        .ToListAsync(cancellationToken);

    var departmentSummary = deptBreakdown
        .Select(d => $"{d.Name}: {d.Present}/{d.Total}")
        .ToList();

    // 7 хоногийн ирцийн хувь
    var weekAttendance = await _dbContext.AttendanceRecords
        .Where(a => a.Date >= sevenDaysAgo && a.Status == AttendanceStatus.Present)
        .CountAsync(cancellationToken);
    var totalPossible = totalEmployees * 7;
    var weeklyRate = totalPossible > 0
        ? (int)Math.Round((double)weekAttendance / totalPossible * 100)
        : 0;

    return new AdminAiContextDto
    {
        TotalEmployees = totalEmployees,
        PresentToday = presentToday,
        LateToday = lateToday,
        AbsentToday = Math.Max(totalEmployees - presentToday - lateToday - onLeaveToday, 0),
        OnLeaveToday = onLeaveToday,
        PendingLeaveRequests = pendingLeaveRequests,
        DepartmentNames = departmentSummary,   // ← нарийвчилсан
        WeeklyAttendanceRate = weeklyRate
    };
}
```

#### Файл 2: `AdminAiContextDto.cs` — Шинэ талбар нэмэх

```csharp
// src/AttendanceSystem.Application/DTOs/AI/AdminAiContextDto.cs
public class AdminAiContextDto
{
    public int TotalEmployees { get; set; }
    public int PresentToday { get; set; }
    public int LateToday { get; set; }          // ← ШИНЭ
    public int AbsentToday { get; set; }
    public int OnLeaveToday { get; set; }
    public int PendingLeaveRequests { get; set; }
    public List<string> DepartmentNames { get; set; } = [];
    public int WeeklyAttendanceRate { get; set; }   // ← ШИНЭ (хувиар)
}
```

#### Файл 3: `AiController.cs` — userId-г employeeId болго

```csharp
[HttpPost("employee/chat")]
[Authorize(Roles = "Employee,SuperAdmin,HRManager,DepartmentHead")]
public async Task<ActionResult<ChatResponseDto>> EmployeeChat(
    [FromBody] ChatRequestDto request, CancellationToken cancellationToken)
{
    if (string.IsNullOrWhiteSpace(request.Message))
        return BadRequest("Message хоосон байж болохгүй.");

    // ⬇️ employee_id claim ашиглах (userId бус!)
    var employeeIdClaim = User.FindFirstValue("employee_id");
    if (!Guid.TryParse(employeeIdClaim, out var employeeId))
        return BadRequest(new { message = "Employee profile linked байхгүй байна." });

    var response = await _aiChatService.GetEmployeeResponseAsync(
        request, employeeId, cancellationToken);
    return Ok(response);
}
```

---

## G. Аюулгүй Байдлын Шинжилгээ

### 🔴 Критик

#### G-1. Hardcoded API Key — `appsettings.json`

```json
// src/AttendanceSystem.API/appsettings.json, мөр 42-43:
"AiSettings": {
    "ApiKey": "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",
```

**Засвар:**
```bash
# 1. API key-г шууд хүчингүй болго (NVIDIA console-оос)
# 2. appsettings.json-с устга
# 3. Environment variable ашиглана:
export AiSettings__ApiKey="nvapi-..."

# Docker Compose:
environment:
  - AiSettings__ApiKey=${NVIDIA_API_KEY}

# appsettings.json-д зөвхөн:
"AiSettings": {
    "ApiKey": "",  // ← хоосон, env-ээс унших
```

#### G-2. Hardcoded SA Password — `appsettings.json`

```json
"DefaultConnection": "Server=localhost,1433;Database=AttendanceDB;
    User Id=sa;Password=YourStrong@Passw0rd;..."
```

**Засвар:**
```bash
# Environment variable эсвэл Docker secret ашиглах
export ConnectionStrings__DefaultConnection="Server=...;Password=${DB_PASSWORD}"
```

#### G-3. JWT Secret Key — Хангалттай энтропи байхгүй

```json
"SecretKey": "CHANGE_ME_TO_A_SECURE_KEY_AT_LEAST_32_CHARS_LONG!"
```

**Засвар:**
```bash
# 256-bit random key үүсгэх:
openssl rand -base64 32
# Гарсан string-ийг env variable болгох:
export JwtSettings__SecretKey="<generated>"
```

### 🟠 Өндөр

#### G-4. AiController — Admin endpoint буруу Role

```csharp
// "Admin", "HR" role DB-д байхгүй → SuperAdmin admin chat руу хандах боломжгүй
[Authorize(Roles = "Admin,HR")]
```
→ Засвар E, F хэсэгт тайлбарласан.

#### G-5. CORS — `AllowAnyOrigin()` нь `Cors.AllowedOrigins` тохиргоог дардаг

```csharp
// API Program.cs:
policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
// appsettings.json-д:
"AllowedOrigins": ["https://localhost:7001", ...]  ← үр нөлөөгүй!
```

**Засвар:**
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        var origins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];
        
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
```

#### G-6. Sensitive Data — `EmployeeAccountSeeder` нь нууц үгийг log-д бичдэг

```csharp
// src/AttendanceSystem.Infrastructure/Persistence/Seed/EmployeeAccountSeeder.cs
logger.LogInformation(
    "Created user {Email} with password Employee@12345!", email);
// ⬆️ Нууц үг plaintext-ээр log-д!
```

**Засвар:**
```csharp
logger.LogInformation("Created user {Email} successfully.", email);
// Нууц үгийг log-оос хасна
```

### 🟡 Дунд

#### G-7. Refresh Token — `DateTime.Now` vs `DateTime.UtcNow` зөрчил

```csharp
// JwtTokenService.cs мөр 54:
t.ExpiresAt > DateTime.Now,     // ← Local time
// vs
t.ExpiresAt > DateTime.UtcNow   // ← UTC (зөв)
```

**Засвар:** бүх gas нь `DateTime.UtcNow` ашиглах.

#### G-8. AdminLayout — SuperAdmin-д `api/employees/me` 401 буцаадаг

```csharp
// AdminLayout.razor OnInitializedAsync():
profile = await Api.GetAsync<EmployeeProfileDto>("api/employees/me");
// SuperAdmin-д EmployeeId байхгүй → 401 → ApiClient Logout хийнэ!
```

**Засвар:**
```csharp
try
{
    profile = await Api.GetAsync<EmployeeProfileDto>("api/employees/me");
}
catch (UnauthorizedAccessException)
{
    // SuperAdmin-д employee profile байхгүй нь нормал
    profile = new EmployeeProfileDto { FullName = "System Administrator" };
}
catch
{
    profile = null;
}
```

---

## H. Рефакторингийн Зөвлөмжүүд

### H-1. Solution `.sln`-д Blazor төслийг бүртгэ

```xml
<!-- AttendanceSystem.sln-д нэмэх: -->
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") 
    = "AttendanceSystem.Blazor", 
      "src\AttendanceSystem.Blazor\AttendanceSystem.Blazor.csproj", 
      "{A1000005-0000-0000-0000-000000000005}"
EndProject
```

### H-2. Role Enum / Constants нэгтгэх

```csharp
// src/AttendanceSystem.Infrastructure/Identity/ApplicationRoles.cs (ШИНЭ ФАЙЛ)
public static class ApplicationRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string HRManager = "HRManager";
    public const string DepartmentHead = "DepartmentHead";
    public const string Employee = "Employee";
    public const string Auditor = "Auditor";
}
// EnterpriseApiControllers.cs дахь Roles class болон 
// Dashboard.razor дахь hardcoded string-ийг энэ constant-аар сол
```

### H-3. Миграцийн стратеги сайжруулах

```
ЗӨВЛӨМЖ: Шинэ environment дээр "reset" хийхийг хялбарчлах.
```

```bash
# Database-г шинэчлэхэд:
dotnet ef database drop --force
dotnet ef database update
# → Одоо timestamp дараалал зөв байх тул ажиллана
```

### H-4. Blazor `Program.cs`-д `ThemeService` болон `LeaveService` бүртгэх

```csharp
// src/AttendanceSystem.Blazor/Program.cs-д дутуу service-ууд:
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<LeaveService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<EmployeeService>();
```

---

## I. Кодын Чанарын Асуудлууд

| # | Файл | Асуудал | Цэвэрлэх |
|---|------|---------|---------|
| 1 | `MainLayout.razor` | Bengali/Bangla текст (`নেভিগেট করুন`, `প্রোফাইল` гэх мэт) — Монгол эсвэл Англи байх ёстой | Засах |
| 2 | `App.razor` | `<script src="location.js">` — `/wwwroot`-д файл байхгүй | Нэмэх эсвэл устгах |
| 3 | `AiChat.razor` (Admin) | `@inject HttpClient Http` — `ApiClient` ашиглах ёстой (auth header дутуу) | Засах |
| 4 | `EmployeeStatisticsController.cs` | `Roles` class-ыг `EnterpriseApiControllers.cs`-с давхардуулан тодорхойлсон байна | Нэгтгэх |
| 5 | Blazor `.csproj` | `<TargetFramework>net10.0</TargetFramework>` — API нь `net10.0`, тааралдахгүй target | Нэгтгэх |
| 6 | `AttendanceRepository.cs` | Pagination query-д `AsNoTracking()` дуталтай | Нэмэх |

---

## J. Тэргүүлэлтэй Үйлдлийн Төлөвлөгөө

```
╔══════════════════════════════════════════════════════════════════╗
║  ШАТЛАЛ 1 — ШААРДЛАГАТАЙ (login ажиллуулах)               ⏱ 1ц ║
╠══════════════════════════════════════════════════════════════════╣
║ ☐ 1. 20240101000000_FixSchemaIssues.cs файлыг                   ║
║      20260604000000_FixSchemaIssues.cs болгон rename хийх        ║
║ ☐ 2. ApplicationDbContextModelSnapshot.cs дотор MigrationId      ║
║      шинэчлэх                                                    ║
║ ☐ 3. ApplicationDbSeeder.cs-ийн admin seed-ийг                   ║
║      Department if блокоос гаргаж, тусдаа идемпотент болгох     ║
║ ☐ 4. DB drop → migrate → API restart                            ║
╚══════════════════════════════════════════════════════════════════╝

╔══════════════════════════════════════════════════════════════════╗
║  ШАТЛАЛ 2 — АЮУЛГҮЙ БАЙДАЛ                               ⏱ 2ц  ║
╠══════════════════════════════════════════════════════════════════╣
║ ☐ 5. NVIDIA API key-г appsettings.json-с устгах                 ║
║ ☐ 6. SA password-г env variable болгох                          ║
║ ☐ 7. JWT SecretKey-г production-д env variable болгох           ║
║ ☐ 8. CORS-ийг AllowedOrigins-ийн тохиргоо ашиглах болгох       ║
║ ☐ 9. EmployeeAccountSeeder password log-г арилгах               ║
╚══════════════════════════════════════════════════════════════════╝

╔══════════════════════════════════════════════════════════════════╗
║  ШАТЛАЛ 3 — ЦЭВЭРЛЭГЭЭ                                   ⏱ 1ц  ║
╠══════════════════════════════════════════════════════════════════╣
║ ☐ 10. 9 ширхэг .cshtml.cs файл устгах                          ║
║ ☐ 11. AdminAuthMiddleware.cs устгах                             ║
║ ☐ 12. Footer.razor, Sidebar.razor, Navbar.razor устгах          ║
║ ☐ 13. Admin pages-д [Authorize] attribute нэмэх                 ║
║ ☐ 14. AttendanceSystem.Blazor-г .sln-д нэмэх                   ║
╚══════════════════════════════════════════════════════════════════╝

╔══════════════════════════════════════════════════════════════════╗
║  ШАТЛАЛ 4 — ЗАСВАР                                       ⏱ 3ц  ║
╠══════════════════════════════════════════════════════════════════╣
║ ☐ 15. AiController role fix (SuperAdmin,HRManager)              ║
║ ☐ 16. ApiClient PostChatAsync endpoint fix                      ║
║ ☐ 17. AdminLayout employee/me 401 handle                        ║
║ ☐ 18. DateTime.Now → DateTime.UtcNow (RefreshToken)             ║
║ ☐ 19. MainLayout Bengali text Монгол болгох                     ║
╚══════════════════════════════════════════════════════════════════╝

╔══════════════════════════════════════════════════════════════════╗
║  ШАТЛАЛ 5 — ШИНЭ ФУНКЦИОНАЛЬ БАЙДАЛ                     ⏱ 5+ ц ║
╠══════════════════════════════════════════════════════════════════╣
║ ☐ 20. Department CRUD API + Admin UI                            ║
║ ☐ 21. Role Management хуудас                                    ║
║ ☐ 22. Leave Approve/Reject товч                                 ║
║ ☐ 23. Audit Log харах хуудас                                    ║
║ ☐ 24. AdminAiContextDto-д department breakdown нэмэх            ║
╚══════════════════════════════════════════════════════════════════╝
```

---

**Нэвтрэх итгэмжлэл (засвар хийсний дараа):**
```
Admin:    admin@attendance.local  /  Admin@12345!
Employee: bat@attendance.local    /  Employee@12345!
```
