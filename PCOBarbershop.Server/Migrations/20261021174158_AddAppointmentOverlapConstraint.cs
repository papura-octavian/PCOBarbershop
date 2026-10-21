using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PCOBarbershop.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentOverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // extensia necesara pentru a combina "=" pe BarberId cu "&&" pe intervale
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            // Doua programari ale aceluiasi frizer nu se pot suprapune (cele anulate nu conteaza).
            // Intervalul e [inceput, sfarsit): doua programari lipite una de alta sunt permise.
            migrationBuilder.Sql(@"
                ALTER TABLE ""Appointments""
                ADD CONSTRAINT ""ex_appointments_no_overlap""
                EXCLUDE USING gist (
                    ""BarberId"" WITH =,
                    tstzrange(""StartTime"", ""EndTime"") WITH &&
                )
                WHERE (""Status"" <> 'Cancelled');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"ALTER TABLE ""Appointments"" DROP CONSTRAINT ""ex_appointments_no_overlap"";");
        }
    }
}
