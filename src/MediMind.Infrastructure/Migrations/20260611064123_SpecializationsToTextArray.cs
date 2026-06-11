using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediMind.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SpecializationsToTextArray : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE healthcare_centers
                ALTER COLUMN specializations TYPE text[]
                USING (
                    CASE
                        WHEN specializations IS NULL OR specializations = ''
                        THEN '{}'::text[]
                        ELSE ARRAY(SELECT jsonb_array_elements_text(specializations::jsonb))
                    END
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE healthcare_centers
                ALTER COLUMN specializations TYPE text
                USING array_to_json(specializations)::text;
            ");
        }
    }
}
