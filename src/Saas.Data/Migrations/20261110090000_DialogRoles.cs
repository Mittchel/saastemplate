using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Saas.Data.Migrations;

/// <summary>
/// Maps the template roles onto Dialog's: Member 0 -> Employee 0, Admin 1 -> HrAdmin 2,
/// Owner 2 -> HrAdmin 2. Manager (1) is new and only assigned from now on.
/// </summary>
public partial class DialogRoles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // FORCE ROW LEVEL SECURITY applies to the table owner too, so lift it for
        // this one cross-tenant data fix and restore it straight afterwards.
        foreach (var table in new[] { "Memberships", "Invitations" })
        {
            migrationBuilder.Sql($"""
                ALTER TABLE "{table}" NO FORCE ROW LEVEL SECURITY;
                UPDATE "{table}" SET "Role" = 2 WHERE "Role" = 1;
                ALTER TABLE "{table}" FORCE ROW LEVEL SECURITY;
                """);
        }
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Irreversible: the old Admin/Owner distinction is lost. HrAdmin stays 2 (Owner).
    }
}
