namespace Platform.Core.Abstractions;

/// <summary>Audit stamps, set by the persistence layer on save; never by request handlers or clients.</summary>
public interface IStamped
{
    DateTimeOffset CreatedAt { get; set; }
    Guid? CreatedBy { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
    Guid? UpdatedBy { get; set; }
}

/// <summary>Rows that are soft-deleted (DATA_MODEL "soft delete only where stated").</summary>
public interface ISoftDeletable
{
    DateTimeOffset? DeletedAt { get; set; }
}
