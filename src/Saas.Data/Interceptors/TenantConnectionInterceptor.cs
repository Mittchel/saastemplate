using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Saas.Core.Tenancy;

namespace Saas.Data.Interceptors;

/// <summary>
/// Publishes the current tenant into the Postgres session so row-level
/// security policies can read it back.
/// </summary>
/// <remarks>
/// <para>
/// Hooks connection-open rather than transaction-begin deliberately. EF Core
/// pools connections, so a value set once per transaction would be missing for
/// any query that runs outside one; setting it every time a connection is
/// handed out guarantees the session variables match the request that is about
/// to use it.
/// </para>
/// <para>
/// Uses <c>set_config</c> rather than <c>SET</c> because <c>SET</c> cannot take
/// parameters -- the tenant id would have to be concatenated into SQL.
/// </para>
/// </remarks>
public class TenantConnectionInterceptor(ITenantContext tenantContext) : DbConnectionInterceptor
{
    private const string Sql =
        "SELECT set_config('app.organization_id', @organization_id, false), " +
        "       set_config('app.user_id', @user_id, false)";

    public override void ConnectionOpened(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = Sql;

        // Empty string rather than NULL: the SQL helpers below map '' to NULL,
        // and set_config rejects a NULL value for a non-nullable setting.
        AddParameter(command, "@organization_id", tenantContext.OrganizationId?.ToString() ?? string.Empty);
        AddParameter(command, "@user_id", tenantContext.UserId?.ToString() ?? string.Empty);

        return command;
    }

    private static void AddParameter(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
