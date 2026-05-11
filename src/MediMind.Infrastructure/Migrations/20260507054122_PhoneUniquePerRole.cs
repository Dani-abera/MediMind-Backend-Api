using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PhoneUniquePerRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_users_phone_number",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "i_x_users_phone_number_user_type",
                table: "users",
                columns: new[] { "phone_number", "user_type" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "i_x_users_phone_number_user_type",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "i_x_users_phone_number",
                table: "users",
                column: "phone_number",
                unique: true);
        }
    }
}
