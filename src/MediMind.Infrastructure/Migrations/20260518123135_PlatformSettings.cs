using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    trial_fee_etb = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    basic_fee_etb = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    premium_fee_etb = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    max_advance_booking_days = table.Column<int>(type: "integer", nullable: false),
                    max_slot_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    feature_flags_json = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    maintenance_mode = table.Column<bool>(type: "boolean", nullable: false),
                    maintenance_message = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_configurations", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_configurations");
        }
    }
}
