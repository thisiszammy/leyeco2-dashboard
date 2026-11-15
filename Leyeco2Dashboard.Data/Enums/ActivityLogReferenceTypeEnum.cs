namespace Leyeco2Dashboard.Data.Enums
{
    /// <summary>
    /// Controlled vocabulary of entities an <see cref="Entities.Database.ActivityLog"/> can target.
    /// The enum <em>name</em> is stored in the <c>ReferenceType</c> column (paired with the entity's
    /// primary key in <c>ReferenceId</c>) so an audit query can be filtered to a single entity.
    /// Add a new value when a service starts auditing writes against a new entity.
    /// </summary>
    public enum ActivityLogReferenceTypeEnum
    {
        User = 1
    }
}
