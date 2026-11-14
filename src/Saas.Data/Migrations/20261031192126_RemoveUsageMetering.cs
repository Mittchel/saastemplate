using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saas.Data.Migrations;

/// <inheritdoc />
public partial class RemoveUsageMetering : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Subscriptions");

        migrationBuilder.DropTable(
            name: "UsageEvents");

        migrationBuilder.DropTable(
            name: "Plans");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Plans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                ModifiedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                MonthlyTokenLimit = table.Column<long>(type: "bigint", nullable: true),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                PriceCents = table.Column<int>(type: "integer", nullable: false),
                SeatLimit = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Plans", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UsageEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RunId = table.Column<Guid>(type: "uuid", nullable: true),
                CacheReadTokens = table.Column<int>(type: "integer", nullable: false),
                CacheWriteTokens = table.Column<int>(type: "integer", nullable: false),
                CostMicros = table.Column<long>(type: "bigint", nullable: false),
                CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                InputTokens = table.Column<int>(type: "integer", nullable: false),
                Model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                ModifiedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                OutputTokens = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UsageEvents", x => x.Id);
                table.ForeignKey(
                    name: "FK_UsageEvents_Runs_RunId",
                    column: x => x.RunId,
                    principalTable: "Runs",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "Subscriptions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                CurrentPeriodEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CurrentPeriodStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExternalSubscriptionId = table.Column<string>(type: "text", nullable: true),
                ModifiedDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Subscriptions", x => x.Id);
                table.ForeignKey(
                    name: "FK_Subscriptions_Organizations_OrganizationId",
                    column: x => x.OrganizationId,
                    principalTable: "Organizations",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Subscriptions_Plans_PlanId",
                    column: x => x.PlanId,
                    principalTable: "Plans",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Plans_Code",
            table: "Plans",
            column: "Code",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Subscriptions_OrganizationId",
            table: "Subscriptions",
            column: "OrganizationId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Subscriptions_PlanId",
            table: "Subscriptions",
            column: "PlanId");

        migrationBuilder.CreateIndex(
            name: "IX_UsageEvents_OrganizationId_OccurredAt",
            table: "UsageEvents",
            columns: new[] { "OrganizationId", "OccurredAt" });

        migrationBuilder.CreateIndex(
            name: "IX_UsageEvents_RunId",
            table: "UsageEvents",
            column: "RunId");
    }
}
