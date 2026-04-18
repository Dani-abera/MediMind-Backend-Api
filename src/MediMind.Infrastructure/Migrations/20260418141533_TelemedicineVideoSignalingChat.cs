using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TelemedicineVideoSignalingChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    consultation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    content = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "TIMEZONE('utc', NOW())"),
                    is_read = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.message_id);
                    table.ForeignKey(
                        name: "f_k_chat_messages_video_consultations_consultation_id",
                        column: x => x.consultation_id,
                        principalTable: "video_consultations",
                        principalColumn: "consultation_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "video_quality_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    consultation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bandwidth_kbps = table.Column<int>(type: "integer", nullable: false),
                    packets_lost = table.Column<int>(type: "integer", nullable: false),
                    frame_rate = table.Column<int>(type: "integer", nullable: false),
                    reported_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "TIMEZONE('utc', NOW())"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_quality_metrics", x => x.id);
                    table.ForeignKey(
                        name: "f_k_video_quality_metrics_video_consultations_consultation_id",
                        column: x => x.consultation_id,
                        principalTable: "video_consultations",
                        principalColumn: "consultation_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "i_x_chat_messages_consultation_id_sent_at",
                table: "chat_messages",
                columns: new[] { "consultation_id", "sent_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "i_x_video_quality_metrics_consultation_id_reported_at",
                table: "video_quality_metrics",
                columns: new[] { "consultation_id", "reported_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "video_quality_metrics");
        }
    }
}
