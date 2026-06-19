using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ActivityAgent.Migrations
{
    /// <inheritdoc />
    public partial class init_01 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    UserName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ProcessName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    WindowTitle = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Application = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    StartTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndTime = table.Column<DateTime>(type: "TEXT", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: false),
                    IsIdle = table.Column<bool>(type: "INTEGER", nullable: false),
                    KeyboardCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MouseCount = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSynced = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_IsSynced",
                table: "ActivityLogs",
                column: "IsSynced");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_MachineId",
                table: "ActivityLogs",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_StartTime",
                table: "ActivityLogs",
                column: "StartTime");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs");
        }
    }
}
