# Browser end-to-end tests

The E2E project starts the Blazor app and a lightweight fake API, then drives Chromium through Playwright. No production database or credentials are required.

From the repository root:

```powershell
dotnet test tests\AttendanceSystem.E2ETests\AttendanceSystem.E2ETests.csproj
powershell -ExecutionPolicy Bypass -File tests\AttendanceSystem.E2ETests\bin\Debug\net10.0\playwright.ps1 install chromium
```

Install Chromium once before running the browser suite. The test suite covers every registered page route and the main employee/admin flows: sign-in, the employee/admin notification bell and mark-as-read action, GPS location validation, check-in/check-out, daily leave submission/cancellation, required medical-document validation, hourly leave duration validation, leave approval/rejection, and employee/admin leave page overflow checks at mobile, tablet, and desktop widths. API integration tests cover leave decisions, notification visibility/read state, departments, settings, and authorization metadata. Unit tests cover attendance handlers and attendance rules.
