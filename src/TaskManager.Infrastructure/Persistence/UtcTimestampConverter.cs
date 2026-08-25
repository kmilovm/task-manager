using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace TaskManager.Infrastructure.Persistence;

/// <summary>
/// Stores instants as UTC date-times rather than offsets. Every timestamp originates from
/// <c>IClock.UtcNow</c>, so no offset information is lost, and it is the only mapping SQLite can
/// order by, which BR-209 needs for its creation-time tie-break.
/// </summary>
public sealed class UtcTimestampConverter : ValueConverter<DateTimeOffset, DateTime>
{
    public UtcTimestampConverter()
        : base(value => value.UtcDateTime, value => new DateTimeOffset(value, TimeSpan.Zero))
    {
    }
}
