/// <summary>Identity and scope supplied by the authenticated HTTP boundary.</summary>
public sealed record BookingActor(Guid UserId, Guid TenantId, bool CanManageStudio);
