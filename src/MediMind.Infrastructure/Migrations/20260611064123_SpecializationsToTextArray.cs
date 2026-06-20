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
                ALTER TABLE healthcare_centers ADD COLUMN specializations_new text[];

                UPDATE healthcare_centers
                SET specializations_new = CASE
                    WHEN specializations IS NULL OR specializations = ''
                    THEN '{}'::text[]
                    ELSE ARRAY(SELECT jsonb_array_elements_text(specializations::jsonb))
                END;

                ALTER TABLE healthcare_centers DROP COLUMN specializations;
                ALTER TABLE healthcare_centers RENAME COLUMN specializations_new TO specializations;
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
