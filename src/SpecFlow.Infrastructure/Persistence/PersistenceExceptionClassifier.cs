using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace SpecFlow.Infrastructure.Persistence;

public static class PersistenceExceptionClassifier
{
    public static bool IsConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19
        };

    public static bool IsUniqueConstraintViolation(DbUpdateException exception) =>
        exception.InnerException is SqliteException
        {
            SqliteErrorCode: 19,
            SqliteExtendedErrorCode: 2067
        };
}
