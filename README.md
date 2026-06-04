# Attendance System (Цаг Бүртгэлийн Систем)

Production-oriented employee attendance management built with **ASP.NET Core 8**, Clean Architecture, CQRS (MediatR), EF Core, JWT + Identity RBAC, and Docker.

## Solution structure

```
src/
  AttendanceSystem.Domain/          Entities, enums, domain events
  AttendanceSystem.Application/     CQRS, rules engine, interfaces
  AttendanceSystem.Infrastructure/  EF Core, Identity, JWT
  AttendanceSystem.API/             REST API
  AttendanceSystem.AdminPanel/      Admin UI scaffold (Tailwind)
  AttendanceSystem.Mobile/          MAUI placeholder (Phase 3)
tests/
  AttendanceSystem.UnitTests/
  AttendanceSystem.IntegrationTests/
```

## Phase 1 (implemented)

- Clean Architecture solution and domain model
- EF Core `ApplicationDbContext` + configurations
- JWT authentication + ASP.NET Core Identity roles
- Check-in / check-out API with **AttendanceRulesService** (grace, late, half-day, break, overtime)
- GPS geofence validation (Haversine)
- Database seed (admin user, sample employee, Mongolian holidays)
- Unit tests for rules engine
- Docker Compose (SQL Server, Redis, API, optional Seq/CompreFace/Ollama)

## Quick start

### 1. Infrastructure

```powershell
cd C:\Users\HP\AttendanceSystem
docker compose up -d sqlserver redis
```

### 2. Database migration

```powershell
dotnet tool install --global dotnet-ef
dotnet ef migrations add InitialCreate --project src\AttendanceSystem.Infrastructure --startup-project src\AttendanceSystem.API
dotnet ef database update --project src\AttendanceSystem.Infrastructure --startup-project src\AttendanceSystem.API
```

### 3. Run API

```powershell
dotnet run --project src\AttendanceSystem.API
```

Open Swagger: https://localhost:7000/swagger

### Seed credentials

| Role     | Email                   | Password        |
|----------|-------------------------|-----------------|
| SuperAdmin | admin@attendance.local | Admin@12345!    |
| Employee | bat@attendance.local    | Employee@12345! |

Employee check-in requires GPS near office seed coordinates (47.9184, 106.9177).

### 4. Tests

```powershell
dotnet test
```

## API (Phase 1)

| Method | Endpoint              | Description        |
|--------|-----------------------|--------------------|
| POST   | /api/auth/login       | JWT login          |
| POST   | /api/auth/refresh     | Refresh token      |
| POST   | /api/attendance/checkin  | Check in (auth) |
| POST   | /api/attendance/checkout | Check out       |
| GET    | /api/attendance/today    | Today record    |

## Next phases

- **Phase 2**: CompreFace, Hangfire jobs, SignalR, FCM/email, Blazor admin CRUD, daily/monthly reports
- **Phase 3**: MAUI app, auto-geo, suspicious activity, exports
- **Phase 4**: ML.NET anomalies, AI chatbot, E2E tests, GitHub Actions

## Configuration

See `src/AttendanceSystem.API/appsettings.json` for `AttendanceRules`, `JwtSettings`, `FaceRecognition`, and rate limits.

**Security:** Replace `JwtSettings:SecretKey` and SQL password before production. Use Azure Key Vault or Docker secrets for sensitive values.
"# dadlaga" 
