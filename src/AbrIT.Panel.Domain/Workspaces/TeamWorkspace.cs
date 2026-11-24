namespace AbrIT.Panel.Domain.Workspaces;

public enum WorkspaceMemberStatus
{
    /// <summary>Reserved inside the lock before the connector is called; only left behind by a crash.</summary>
    Pending,
    Active,

    /// <summary>The connector failed; adding the same e-mail again reuses this member (same external id).</summary>
    Failed,
    Removed,
}

public sealed class WorkspaceMember
{
    /// <summary>Seat number, ≥ 2 (the owner is seat 1). Never reused within a workspace.</summary>
    public int MemberNumber { get; set; }

    /// <summary>Normalized (trimmed, lower-case).</summary>
    public string Email { get; set; } = string.Empty;

    public string ExternalUserId { get; set; } = string.Empty;

    public WorkspaceMemberStatus Status { get; set; }

    public DateTime AddedUtc { get; set; }

    public DateTime? RemovedUtc { get; set; }

    public string? LastErrorCode { get; set; }

    public bool OccupiesSeat => Status == WorkspaceMemberStatus.Active;
}

/// <summary>
/// Members of a Team order's Drive workspace (bonus, spec §12). Exactly one per order, created inside the per-order
/// lock. The order owner is seat 1 (the account provisioned with the order) and is not stored in <see cref="Members"/>.
/// </summary>
public sealed class TeamWorkspace
{
    /// <summary>Storage document id (DECISIONS D-12).</summary>
    public long Id { get; set; }

    public string OrderId { get; set; } = string.Empty;

    public string GroupId { get; set; } = string.Empty;

    /// <summary>Plan <c>MaxUsers</c> frozen at order time, owner included.</summary>
    public int SeatLimit { get; set; }

    public int NextMemberNumber { get; set; } = 2;

    public List<WorkspaceMember> Members { get; set; } = [];

    /// <summary>Owner + active members.</summary>
    public int SeatsUsed => 1 + Members.Count(m => m.OccupiesSeat);
}
