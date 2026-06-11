using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSchemaIssues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM AttendanceRecords
                    GROUP BY EmployeeId, [Date]
                    HAVING COUNT(*) > 1
                )
                BEGIN
                    THROW 51000, 'Duplicate AttendanceRecords found for EmployeeId + Date. Clean duplicates before applying UQ_Attendance_Employee_Date.', 1;
                END
                """);

            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM RefreshTokens WHERE LEN(Token) > 512)
                BEGIN
                    THROW 51001, 'RefreshTokens.Token contains values longer than 512 characters. Clean or rotate tokens before applying this migration.', 1;
                END
                """);

            migrationBuilder.Sql("""
                UPDATE e
                SET UserId = NULL
                FROM Employees e
                WHERE e.UserId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM AspNetUsers u WHERE u.Id = e.UserId);

                UPDATE u
                SET EmployeeId = NULL
                FROM AspNetUsers u
                WHERE u.EmployeeId IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM Employees e WHERE e.Id = u.EmployeeId);
                """);

            migrationBuilder.DropIndex(
                name: "IX_AttendanceRecords_EmployeeId",
                table: "AttendanceRecords");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Employees",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<decimal>(
                name: "TotalDays",
                table: "LeaveRequests",
                type: "decimal(5,1)",
                precision: 5,
                scale: 1,
                nullable: false,
                computedColumnSql: "CAST(DATEDIFF(day, [StartDate], [EndDate]) + 1 AS decimal(5,1))",
                stored: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "Notifications",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "UQ_Attendance_Employee_Date",
                table: "AttendanceRecords",
                columns: new[] { "EmployeeId", "Date" });

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_AspNetUsers",
                table: "Employees",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Employees",
                table: "AspNetUsers",
                column: "EmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddCheckConstraint(
                name: "CHK_WorkDays_Range",
                table: "WorkSchedules",
                sql: "[WorkDays] BETWEEN 0 AND 127");

            migrationBuilder.CreateIndex(
                name: "IX_Attendance_EmployeeId_Date",
                table: "AttendanceRecords",
                columns: new[] { "EmployeeId", "Date" })
                .Annotation("SqlServer:Include", new[] { "Status", "CheckInTime", "CheckOutTime" });

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens",
                column: "Token");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Leave_EmployeeId_Status",
                table: "LeaveRequests",
                columns: new[] { "EmployeeId", "Status" })
                .Annotation("SqlServer:Include", new[] { "StartDate", "EndDate", "LeaveType" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLog_EntityId_Action",
                table: "AuditLogs",
                columns: new[] { "EntityId", "Action" })
                .Annotation("SqlServer:Include", new[] { "CreatedAt", "PerformedBy" });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_DepartmentId_Active",
                table: "Employees",
                column: "DepartmentId",
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Employees_DepartmentId_Active",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_AuditLog_EntityId_Action",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_Leave_EmployeeId_Status",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_Token",
                table: "RefreshTokens");

            migrationBuilder.DropIndex(
                name: "IX_Attendance_EmployeeId_Date",
                table: "AttendanceRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CHK_WorkDays_Range",
                table: "WorkSchedules");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Employees",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_AspNetUsers",
                table: "Employees");

            migrationBuilder.DropUniqueConstraint(
                name: "UQ_Attendance_Employee_Date",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "TotalDays",
                table: "LeaveRequests");

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Employees",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AttendanceRecords_EmployeeId",
                table: "AttendanceRecords",
                column: "EmployeeId");
        }
    }
}
