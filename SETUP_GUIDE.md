# Attendance System - Quick Setup & Deployment Guide

## 🚀 Quick Start

### Prerequisites
- .NET 8.0 SDK
- SQL Server 2019+
- Docker & Docker Compose (optional)
- Visual Studio Code or Visual Studio 2022

### Step 1: Clone & Install

```bash
cd c:\Users\HP\AttendanceSystem
dotnet restore
```

### Step 2: Setup Database

```bash
# Start SQL Server with Docker
docker compose up -d sqlserver

# Run migrations
dotnet ef database update --project src/AttendanceSystem.Infrastructure --startup-project src/AttendanceSystem.API
```

### Step 3: Run Application

#### Option A: Run All Services
```bash
# Terminal 1: Run API
dotnet run --project src/AttendanceSystem.API

# Terminal 2: Run Blazor App
dotnet run --project src/AttendanceSystem.Blazor

# Terminal 3: Run Admin Panel
dotnet run --project src/AttendanceSystem.AdminPanel
```

#### Option B: Use Docker Compose
```bash
docker compose up -d
```

### Step 4: Access Applications

| Application | URL | Role |
|-------------|-----|------|
| Employee App (Blazor) | https://localhost:7001 | Employee |
| Admin Dashboard (Blazor) | https://localhost:7001/admin | SuperAdmin |
| Admin Panel | https://localhost:5001 | SuperAdmin |
| API Swagger | https://localhost:7000/swagger | API Docs |

### Step 5: Login with Demo Credentials

| Role | Email | Password |
|------|-------|----------|
| SuperAdmin | admin@attendance.local | Admin@12345! |
| Employee | bat@attendance.local | Employee@12345! |

## 📋 New Features Overview

### Employee Interface ✨
- **Path**: `/attendance`
- Modern gradient design
- Location-based check-in/out
- Real-time geofence detection (500m radius)
- Work hours tracking
- Status indicators

### Admin Dashboard 📊
- **Path**: `/admin/dashboard`
- Real-time attendance statistics
- Employee attendance table
- Department tracking
- Monthly analytics
- Quick reports

### Geofence Monitoring 🗺️
- **Path**: `/admin/geofence`
- Live employee location tracking
- Distance from office display
- Geofence status (in/outside/near)
- Real-time updates every 10 seconds
- Zone-based statistics

## 🎨 UI/UX Highlights

### Design System
✓ Modern glassmorphism with blur effects
✓ Gradient backgrounds and animations
✓ Color-coded status indicators
✓ Responsive mobile-first design
✓ Dark theme for better readability
✓ Smooth transitions and animations

### Components Updated
- ✅ Employee Check-In Page
- ✅ Admin Dashboard
- ✅ Geofence Monitor
- ✅ Navigation Layout
- ✅ Admin Login Page
- ✅ Custom CSS Styling

## 🔧 Configuration

### GPS Settings
Edit constants in `AttendanceSystem.Blazor/Components/Pages/AttendanceCheckin.razor`:

```csharp
private const double OFFICE_LATITUDE = 47.9123;
private const double OFFICE_LONGITUDE = 106.9318;
private const double GEOFENCE_RADIUS_KM = 0.5; // 500 meters
```

### API Settings
Update `appsettings.json`:

```json
{
  "JwtSettings": {
    "SecretKey": "your-secret-key-here",
    "ExpiryMinutes": 60
  },
  "AttendanceRules": {
    "LateThresholdMinutes": 15
  }
}
```

## 📁 Project Structure

```
AttendanceSystem/
├── src/
│   ├── AttendanceSystem.Domain/          [Entities, Enums]
│   ├── AttendanceSystem.Application/     [CQRS, Business Logic]
│   ├── AttendanceSystem.Infrastructure/  [Data Access, Identity]
│   ├── AttendanceSystem.API/             [REST Endpoints]
│   ├── AttendanceSystem.Blazor/          [Razor Components - NEW UI]
│   │   └── Components/Pages/
│   │       ├── AttendanceCheckin.razor   [Employee Check-In]
│   │       ├── AdminDashboard.razor      [Admin Dashboard]
│   │       └── GeofenceMonitoring.razor  [Geofence Monitor]
│   └── AttendanceSystem.AdminPanel/      [Admin Panel]
└── tests/
    ├── AttendanceSystem.UnitTests/
    └── AttendanceSystem.IntegrationTests/
```

## 🧪 Testing

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Project
```bash
dotnet test tests/AttendanceSystem.UnitTests/
dotnet test tests/AttendanceSystem.IntegrationTests/
```

## 🐳 Docker Deployment

### Build Custom Image
```bash
docker build -f src/AttendanceSystem.API/Dockerfile -t attendance-api:latest .
```

### Run with Docker Compose
```bash
docker compose up -d
```

### Services
- **attendance-api**: Main API (Port 7000)
- **attendance-web**: Blazor App (Port 7001)
- **sqlserver**: SQL Server Database (Port 1433)
- **redis**: Cache Server (Port 6379)

## 📊 API Endpoints

### Authentication
- `POST /api/auth/login` - User login
- `POST /api/auth/refresh` - Refresh JWT token

### Attendance (Requires Auth)
- `POST /api/attendance/checkin` - Check-in
  ```json
  {
    "latitude": 47.9123,
    "longitude": 106.9318,
    "locationPermissionGranted": true,
    "verificationMethod": "Gps"
  }
  ```

- `POST /api/attendance/checkout` - Check-out
  ```json
  {
    "latitude": 47.9123,
    "longitude": 106.9318,
    "verificationMethod": "Gps"
  }
  ```

- `GET /api/attendance/today` - Get today's record
- `GET /api/attendance/statistics` - Get monthly stats

## 🔐 Security Best Practices

1. **Change Default Credentials**
   ```bash
   Update appsettings.json with strong passwords
   ```

2. **Use Environment Variables**
   ```bash
   SET ASPNETCORE_ENVIRONMENT=Production
   SET ConnectionStrings__DefaultConnection=your-connection-string
   ```

3. **Enable HTTPS**
   - Ensure HTTPS redirection is enabled
   - Use valid SSL certificates in production

4. **SQL Server Security**
   - Change SA password
   - Use authentication
   - Disable xp_cmdshell

## 🚨 Troubleshooting

### Port Already in Use
```bash
netstat -ano | findstr :7000
taskkill /PID <PID> /F
```

### Database Connection Error
```bash
# Check SQL Server running
docker ps | grep sqlserver

# Check connection string in appsettings.json
```

### Location Permission Denied
```
Browser permission required for geolocation
Grant location access in browser settings
```

## 📈 Performance Tips

1. **Enable Caching**
   - Redis is configured in docker-compose.yml
   - Distributed caching for frequently accessed data

2. **Database Optimization**
   - Create indexes on Employee and AttendanceRecord
   - Use pagination for large data sets

3. **Frontend Optimization**
   - Lazy load components
   - Use Tailwind CSS for minimal CSS bundle
   - Compress images

## 📚 Additional Resources

- [ASP.NET Core Documentation](https://docs.microsoft.com/aspnet/core)
- [Blazor Components Guide](https://docs.microsoft.com/aspnet/core/blazor/components)
- [Tailwind CSS Docs](https://tailwindcss.com/docs)
- [Entity Framework Core](https://docs.microsoft.com/ef/core)

## ✅ Deployment Checklist

- [ ] Update database connection strings
- [ ] Change default admin credentials
- [ ] Configure JWT secret key
- [ ] Enable HTTPS/SSL
- [ ] Setup email service (for notifications)
- [ ] Configure backup strategy
- [ ] Setup monitoring and logging
- [ ] Test all features in production
- [ ] Document API endpoints
- [ ] Create user documentation

## 📞 Support & Feedback

For issues, bugs, or feature requests:
1. Check README.md and UI_UX_IMPROVEMENTS.md
2. Review existing issues on GitHub
3. Contact: support@attendance.local

---

**Version**: 2.0 - Modern UI/UX Edition
**Last Updated**: 2024
**Status**: ✅ Production Ready
