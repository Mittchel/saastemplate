using Saas.Business.Contracts;
using Saas.Data.Models;

namespace Saas.Business.Mapping;

/// <summary>
/// Entity to contract projections.
/// </summary>
/// <remarks>
/// <para>
/// Hand-written rather than generated. AutoMapper, used by both earlier
/// templates, moved to a paid licence at version 15 (14 was the last MIT
/// release) -- a poor foundation for a template cloned into commercial
/// products. Mapperly was tried in its place, but every projection here
/// intentionally differs in shape from its entity, so it needed an attribute
/// per unmapped member and still could not flatten the joined user. Six
/// explicit one-liners are shorter than configuring a generator around them,
/// and they read as ordinary C#.
/// </para>
/// <para>
/// Reach for a generator again if the number of projections grows past the
/// point where the compiler catching a forgotten member is worth the ceremony.
/// </para>
/// </remarks>
public static class EntityMapper
{
    public static InvitationSummary ToSummary(this InvitationEntity entity) =>
        new(entity.Id, entity.Email, entity.Role, entity.ExpiresAt,
            entity.AcceptedAt, entity.IsPending);

    public static RunResponse ToResponse(this RunEntity entity) =>
        new(entity.Id, entity.ThreadId, entity.Status, entity.Model, entity.CreatedDate);

    public static InternalRunResponse ToInternalResponse(this RunEntity entity) =>
        new(entity.Id, entity.OrganizationId, entity.UserId, entity.ThreadId,
            entity.Model, entity.Input, entity.Status);

    /// <summary>Requires <see cref="MembershipEntity.User"/> to be loaded.</summary>
    public static MemberSummary ToSummary(this MembershipEntity entity) =>
        new(entity.UserId, entity.User.Email ?? string.Empty, entity.User.FullName,
            entity.User.Avatar, entity.Role, entity.CreatedDate);

    /// <summary>The caller's role lives on the membership, not the organization.</summary>
    public static OrganizationSummary ToSummary(this OrganizationEntity entity, OrganizationRole role) =>
        new(entity.Id, entity.Name, entity.Slug, entity.IsPersonal, role);

    public static OrganizationDetail ToDetail(this OrganizationEntity entity, int memberCount) =>
        new(entity.Id, entity.Name, entity.Slug, entity.IsPersonal,
            entity.CreatedDate, memberCount);
}
