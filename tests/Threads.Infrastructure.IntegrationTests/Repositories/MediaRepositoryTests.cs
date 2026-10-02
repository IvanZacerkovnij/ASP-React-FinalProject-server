using Microsoft.EntityFrameworkCore;
using Threads.Domain.Enums;
using Threads.Infrastructure.Data.Repositories.Media;
using Threads.Infrastructure.IntegrationTests.Infrastructure;

namespace Threads.Infrastructure.IntegrationTests.Repositories;

[Collection(PostgreSqlCollection.Name)]
public sealed class MediaRepositoryTests : DatabaseTestBase
{
    public MediaRepositoryTests(PostgreSqlFixture fixture)
        : base(fixture)
    {
    }

    [Fact]
    public async Task AddAndQueries_PersistMetadataAndReturnDistinctStorageKeys()
    {
        var uploader = TestEntityFactory.CreateUser();
        var otherUploader = TestEntityFactory.CreateUser("other-uploader");
        await using (var seedContext = Fixture.CreateContext())
        {
            seedContext.Users.AddRange(uploader, otherUploader);
            await seedContext.SaveChangesAsync();
        }

        var first = CreateMedia(uploader.Id, "media/first.jpg", "media/first-thumb.jpg");
        var second = CreateMedia(uploader.Id, "media/second.jpg", "media/first-thumb.jpg");
        var unrequested = CreateMedia(uploader.Id, "media/unrequested.jpg", null);
        var foreign = CreateMedia(otherUploader.Id, "media/foreign.jpg", "media/foreign-thumb.jpg");

        await using var dbContext = Fixture.CreateContext();
        var repository = new MediaRepository(dbContext);
        await repository.AddAsync(first);
        await repository.AddAsync(second);
        await repository.AddAsync(unrequested);
        await repository.AddAsync(foreign);

        var stored = await repository.GetByIdsAsync([second.Id, first.Id]);
        var keys = await repository.GetStorageKeysByUploaderIdAsync(uploader.Id);

        Assert.Equal(2, stored.Count);
        Assert.Contains(stored, media => media.Id == first.Id && media.ContentType == "image/jpeg");
        Assert.DoesNotContain(stored, media => media.Id == unrequested.Id || media.Id == foreign.Id);
        Assert.Equal(4, keys.Count);
        Assert.Contains(first.StorageKey, keys);
        Assert.Contains(second.StorageKey, keys);
        Assert.Contains(unrequested.StorageKey, keys);
        Assert.Contains(first.ThumbnailStorageKey!, keys);
        Assert.DoesNotContain(foreign.StorageKey, keys);
    }

    [Fact]
    public async Task AddAsync_WhenStorageKeyIsDuplicated_EnforcesUniqueConstraint()
    {
        var uploader = TestEntityFactory.CreateUser();
        await using var dbContext = Fixture.CreateContext();
        dbContext.Users.Add(uploader);
        await dbContext.SaveChangesAsync();
        var repository = new MediaRepository(dbContext);
        var first = CreateMedia(uploader.Id, "media/duplicate.jpg", null);
        var duplicate = CreateMedia(uploader.Id, first.StorageKey, null);
        await repository.AddAsync(first);

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(duplicate));
        Assert.Equal(1, await dbContext.Medias.CountAsync());
    }

    private static Threads.Domain.Entities.Media CreateMedia(
        Guid uploaderId,
        string storageKey,
        string? thumbnailStorageKey)
    {
        return new Threads.Domain.Entities.Media
        {
            StorageKey = storageKey,
            ThumbnailStorageKey = thumbnailStorageKey,
            FileName = Path.GetFileName(storageKey),
            ContentType = "image/jpeg",
            Type = MediaType.Image,
            SizeInBytes = 1024,
            Width = 100,
            Height = 100,
            SortOrder = 0,
            UploadedByUserId = uploaderId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
