using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Threads.Application.Exceptions;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data;

internal static class ConcurrencySaveChanges
{
    public static async Task SaveAsync(
        ThreadsDbContext dbContext,
        string resourceName,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }
        catch (DbUpdateConcurrencyException exception)
        {
            if (!await TryMergeNonOverlappingChangesAsync(exception, cancellationToken))
            {
                throw CreateConflictException(exception, resourceName);
            }
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw CreateConflictException(exception, resourceName);
        }
    }

    private static async Task<bool> TryMergeNonOverlappingChangesAsync(
        DbUpdateConcurrencyException exception,
        CancellationToken cancellationToken)
    {
        if (exception.Entries.Count == 0 ||
            exception.Entries.Any(entry => entry.State != EntityState.Modified))
        {
            return false;
        }

        var databaseValuesByEntry = new List<(
            EntityEntry Entry,
            PropertyValues DatabaseValues,
            HashSet<string> ModifiedProperties)>();

        foreach (var entry in exception.Entries)
        {
            var databaseValues = await entry.GetDatabaseValuesAsync(cancellationToken);

            if (databaseValues is null || HasOverlappingChanges(entry, databaseValues))
            {
                return false;
            }

            var modifiedProperties = entry.Properties
                .Where(property => property.IsModified && !property.Metadata.IsConcurrencyToken)
                .Select(property => property.Metadata.Name)
                .ToHashSet(StringComparer.Ordinal);

            databaseValuesByEntry.Add((entry, databaseValues, modifiedProperties));
        }

        foreach (var (entry, databaseValues, modifiedProperties) in databaseValuesByEntry)
        {
            foreach (var property in entry.Properties)
            {
                if (!modifiedProperties.Contains(property.Metadata.Name))
                {
                    property.CurrentValue = databaseValues[property.Metadata];
                }
            }

            entry.OriginalValues.SetValues(databaseValues);

            foreach (var property in entry.Properties.Where(property =>
                         modifiedProperties.Contains(property.Metadata.Name)))
            {
                if (Equals(property.CurrentValue, databaseValues[property.Metadata]))
                {
                    property.IsModified = false;
                }
            }
        }

        return true;
    }

    private static bool HasOverlappingChanges(EntityEntry entry, PropertyValues databaseValues)
    {
        return entry.Properties
            .Where(property => property.IsModified && !property.Metadata.IsConcurrencyToken)
            .Any(property =>
            {
                var databaseValue = databaseValues[property.Metadata];
                return !Equals(databaseValue, property.OriginalValue) &&
                       !Equals(databaseValue, property.CurrentValue);
            });
    }

    private static ConflictException CreateConflictException(
        DbUpdateConcurrencyException exception,
        string resourceName)
    {
        var containsMedia = exception.Entries.Any(entry => entry.Entity is Media);
        var message = containsMedia
            ? "One or more media items were modified by another request."
            : $"The {resourceName} was modified or deleted by another request. Reload it and try again.";

        return new ConflictException(message);
    }
}
