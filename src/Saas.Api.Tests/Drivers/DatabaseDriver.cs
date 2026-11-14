using Npgsql;

namespace Saas.Api.Tests.Drivers;

/// <summary>
/// Queries Postgres directly, as the runtime role.
/// </summary>
/// <remarks>
/// The point of these queries is to check the isolation layer the application
/// cannot vouch for itself. Connecting as <c>saas_app</c> -- the same non-owner
/// login the API uses -- and setting the tenant by hand tests the row-level
/// security policies with no EF query filter anywhere in the picture, so a pass
/// means the database is enforcing it rather than the ORM.
/// </remarks>
public class DatabaseDriver(TestSettings settings)
{
    /// <summary>Counts rows in a table as a given tenant would see them.</summary>
    public async Task<int> CountAsTenantAsync(string table, Guid? organizationId, Guid? userId = null)
    {
        await using var connection = new NpgsqlConnection(settings.Postgres);
        await connection.OpenAsync();

        await SetTenantAsync(connection, organizationId, userId);

        await using var command = new NpgsqlCommand($"SELECT count(*) FROM \"{table}\"", connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    /// <summary>True when the role may read the table at all.</summary>
    public async Task<bool> CanReadAsync(string connectionString, string qualifiedTable)
    {
        try
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();

            await using var command = new NpgsqlCommand(
                $"SELECT 1 FROM {qualifiedTable} LIMIT 1", connection);
            await command.ExecuteScalarAsync();

            return true;
        }
        catch (PostgresException exception) when (exception.SqlState == "42501")
        {
            // insufficient_privilege
            return false;
        }
    }

    /// <summary>Whether row-level security is both enabled and forced on a table.</summary>
    public async Task<(bool Enabled, bool Forced)> GetRowLevelSecurityAsync(string table)
    {
        await using var connection = new NpgsqlConnection(settings.PostgresOwner);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT relrowsecurity, relforcerowsecurity FROM pg_class WHERE relname = @table",
            connection);
        command.Parameters.AddWithValue("table", table);

        await using var reader = await command.ExecuteReaderAsync();

        return await reader.ReadAsync()
            ? (reader.GetBoolean(0), reader.GetBoolean(1))
            : (false, false);
    }

    /// <summary>Finds an account's user id by email, or null.</summary>
    public async Task<Guid?> FindUserIdAsync(string email)
    {
        await using var connection = new NpgsqlConnection(settings.PostgresOwner);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT \"Id\" FROM \"Users\" WHERE lower(\"Email\") = lower(@email)", connection);
        command.Parameters.AddWithValue("email", email);

        return await command.ExecuteScalarAsync() as Guid?;
    }

    /// <summary>
    /// Adds a membership for an existing account, with the owner connection.
    /// Row-level security is forced even for the owner, so the tenant is set to
    /// the organization first, as the insert policy requires.
    /// </summary>
    public Task AddMembershipAsync(Guid userId, Guid organizationId, string role) =>
        ExecuteAsTenantAsync(
            organizationId,
            "INSERT INTO \"Memberships\" (\"Id\", \"OrganizationId\", \"UserId\", \"Role\") " +
            "VALUES (@id, @organization, @user, @role)",
            command =>
            {
                command.Parameters.AddWithValue("id", Guid.CreateVersion7());
                command.Parameters.AddWithValue("organization", organizationId);
                command.Parameters.AddWithValue("user", userId);
                command.Parameters.AddWithValue("role", RoleValue(role));
            });

    /// <summary>Writes a raw role value, including ones no role name maps to.</summary>
    public Task SetMembershipRoleValueAsync(Guid userId, Guid organizationId, int roleValue) =>
        ExecuteAsTenantAsync(
            organizationId,
            "UPDATE \"Memberships\" SET \"Role\" = @role WHERE \"OrganizationId\" = @organization AND \"UserId\" = @user",
            command =>
            {
                command.Parameters.AddWithValue("organization", organizationId);
                command.Parameters.AddWithValue("user", userId);
                command.Parameters.AddWithValue("role", roleValue);
            });

    /// <summary>Removes a membership row.</summary>
    public Task RemoveMembershipAsync(Guid userId, Guid organizationId) =>
        ExecuteAsTenantAsync(
            organizationId,
            "DELETE FROM \"Memberships\" WHERE \"OrganizationId\" = @organization AND \"UserId\" = @user",
            command =>
            {
                command.Parameters.AddWithValue("organization", organizationId);
                command.Parameters.AddWithValue("user", userId);
            });

    /// <summary>
    /// Counts membership and invitation rows, across every tenant, whose role
    /// is not 0, 1 or 2. Read-only: it sets the tenant per organization, as
    /// the policies require, and changes no schema.
    /// </summary>
    public async Task<int> CountRowsWithUnknownRoleAsync()
    {
        await using var connection = new NpgsqlConnection(settings.PostgresOwner);
        await connection.OpenAsync();

        var userIds = new List<Guid>();
        await using (var users = new NpgsqlCommand("SELECT \"Id\" FROM \"Users\"", connection))
        await using (var reader = await users.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                userIds.Add(reader.GetGuid(0));
            }
        }

        // Memberships are readable by their own user from any organization, which
        // enumerates every organization that has at least one member.
        var organizationIds = new HashSet<Guid>();
        foreach (var userId in userIds)
        {
            await SetTenantAsync(connection, null, userId);
            await using var memberships = new NpgsqlCommand(
                "SELECT \"OrganizationId\" FROM \"Memberships\" WHERE \"UserId\" = @user", connection);
            memberships.Parameters.AddWithValue("user", userId);
            await using var reader = await memberships.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                organizationIds.Add(reader.GetGuid(0));
            }
        }

        var total = 0;
        foreach (var organizationId in organizationIds)
        {
            await SetTenantAsync(connection, organizationId, null);
            foreach (var table in new[] { "Memberships", "Invitations" })
            {
                await using var count = new NpgsqlCommand(
                    $"SELECT count(*) FROM \"{table}\" WHERE \"Role\" NOT IN (0, 1, 2)", connection);
                total += Convert.ToInt32(await count.ExecuteScalarAsync());
            }
        }

        return total;
    }

    /// <summary>The stored value of a role name.</summary>
    public static int RoleValue(string role) => role switch
    {
        "Employee" => 0,
        "Manager" => 1,
        "HrAdmin" => 2,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown role name."),
    };

    private async Task ExecuteAsTenantAsync(
        Guid organizationId,
        string sql,
        Action<NpgsqlCommand> bind)
    {
        await using var connection = new NpgsqlConnection(settings.PostgresOwner);
        await connection.OpenAsync();

        await SetTenantAsync(connection, organizationId, null);

        await using var command = new NpgsqlCommand(sql, connection);
        bind(command);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetTenantAsync(
        NpgsqlConnection connection,
        Guid? organizationId,
        Guid? userId)
    {
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.organization_id', @organization, false), " +
            "       set_config('app.user_id', @user, false)",
            connection);

        command.Parameters.AddWithValue("organization", organizationId?.ToString() ?? string.Empty);
        command.Parameters.AddWithValue("user", userId?.ToString() ?? string.Empty);

        await command.ExecuteNonQueryAsync();
    }
}
