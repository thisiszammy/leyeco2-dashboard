using System;
using Leyeco2Dashboard.Data.Enums;

namespace Leyeco2Dashboard.Data.Entities.Database;

/// <summary>
/// Hand-written partial that adds the audit factory to the EF-scaffolded <see cref="ActivityLog"/>
/// entity. Kept in a separate file so re-running EF Core Power Tools never overwrites it. Services
/// build every <see cref="ActivityLog"/> through <see cref="Create"/> (never by property assignment)
/// and every detail string through <see cref="BuildDetail"/> so audit wording stays consistent.
/// </summary>
public partial class ActivityLog
{
    /// <summary>
    /// Builds an audit row for a single database write.
    /// <paramref name="batchId"/> groups every row emitted by one service-method call;
    /// <paramref name="executionOrder"/> is the pre-incremented position of this row within that batch.
    /// The primary key is assigned here because the column is <c>ValueGeneratedNever</c>.
    /// <paramref name="referenceType"/>/<paramref name="referenceId"/> point the audit trail at the
    /// affected entity. <paramref name="fromSignature"/>/<paramref name="toSignature"/> capture a
    /// before/after snapshot on updates and are left null otherwise.
    /// </summary>
    public static ActivityLog Create(
        Guid? batchId,
        int executionOrder,
        ActivityEnum activity,
        Guid? executedBy,
        DateTime executedOn,
        string? ipAddress,
        string? userAgent,
        string detail,
        ActivityLogReferenceTypeEnum referenceType,
        string referenceId,
        string? fromSignature = null,
        string? toSignature = null)
    {
        return new ActivityLog
        {
            ActivityLogId = Guid.NewGuid(),
            BatchId = batchId,
            ExecutionOrder = executionOrder,
            Activity = (int)activity,
            ExecutedBy = executedBy,
            ExecutedOn = executedOn,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Detail = detail,
            ReferenceType = referenceType.ToString(),
            ReferenceId = referenceId,
            FromSignature = fromSignature,
            ToSignature = toSignature
        };
    }

    /// <summary>
    /// Produces the human-readable detail string for an activity so the wording stays identical
    /// everywhere that activity is logged. Positional <paramref name="args"/> fill the template
    /// (e.g. the username, what changed); missing args render as empty so a caller can never crash the audit.
    /// </summary>
    public static string BuildDetail(ActivityEnum activity, params object?[] args)
    {
        return activity switch
        {
            ActivityEnum.CreatedUser => $"Created user '{Arg(args, 0)}' ({Arg(args, 1)}).",
            ActivityEnum.UpdatedUser => $"Updated user '{Arg(args, 0)}' ({Arg(args, 1)}).",
            ActivityEnum.ResetUserPassword => $"Reset the password of user '{Arg(args, 0)}'.",
            ActivityEnum.SignedIn => $"User '{Arg(args, 0)}' signed in.",
            ActivityEnum.ChangedUserAccess => $"Changed the access of user '{Arg(args, 0)}': {Arg(args, 1)}.",
            ActivityEnum.DeactivatedUser => $"Deactivated user '{Arg(args, 0)}'; {Arg(args, 1)} open session(s) ended.",
            ActivityEnum.ReactivatedUser => $"Reactivated user '{Arg(args, 0)}'.",
            ActivityEnum.UnlockedUser => $"Unlocked user '{Arg(args, 0)}'.",
            ActivityEnum.ChangedOwnPassword => $"User '{Arg(args, 0)}' changed their own password.",
            ActivityEnum.LockedUser => $"Locked user '{Arg(args, 0)}' until {Arg(args, 1)} after {Arg(args, 2)} failed sign-ins.",
            _ => activity.ToString()
        };
    }

    private static string Arg(object?[] args, int index)
        => index >= 0 && index < args.Length ? args[index]?.ToString() ?? string.Empty : string.Empty;
}
