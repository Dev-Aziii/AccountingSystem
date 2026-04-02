using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AccountingSystem.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginAttemptPolicySecurityState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserFailedLoginAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFailedLoginAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFailedLoginAttempts_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLockoutEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LockoutDurationMinutes = table.Column<int>(type: "int", nullable: false),
                    ConsecutiveLockoutCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLockoutEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLockoutEvents_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLoginSecurityStates",
                columns: table => new
                {
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ConsecutiveLockoutCount = table.Column<int>(type: "int", nullable: false),
                    LastSuccessfulLoginAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisabledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DisabledReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLoginSecurityStates", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserLoginSecurityStates_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserFailedLoginAttempts_UserId_OccurredAtUtc",
                table: "UserFailedLoginAttempts",
                columns: new[] { "UserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLockoutEvents_UserId_OccurredAtUtc",
                table: "UserLockoutEvents",
                columns: new[] { "UserId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UserLoginSecurityStates_UserId",
                table: "UserLoginSecurityStates",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFailedLoginAttempts");

            migrationBuilder.DropTable(
                name: "UserLockoutEvents");

            migrationBuilder.DropTable(
                name: "UserLoginSecurityStates");
        }
    }
}
