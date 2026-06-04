using System;
using System.Threading.Tasks;
using Lingarr.Core.Data;
using Lingarr.Core.Entities;
using Lingarr.Server.Models.Integrations;
using Lingarr.Server.Services;
using Lingarr.Server.Services.Sync;
using Lingarr.Server.Interfaces.Services.Integration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Lingarr.Server.Tests.Services.Sync;

public class SeasonSyncTests
{
    private static (SeasonSync, LingarrDbContext) CreateSeasonSync(Mock<ISonarrService> sonarrMock)
    {
        var options = new DbContextOptionsBuilder<LingarrDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new LingarrDbContext(options);
        var pathConversion = new PathConversionService(context);

        var sync = new SeasonSync(
            context,
            sonarrMock.Object,
            pathConversion,
            NullLogger<SeasonSync>.Instance);

        return (sync, context);
    }

    private static SonarrShow MakeSonarrShow() => new()
    {
        Id = 1,
        Title = "The Expanse",
        Path = "/tv/The Expanse",
        Added = DateTime.UtcNow.ToString("o"),
        SeasonFolder = true
    };

    private static Show MakeShowEntity() => new()
    {
        SonarrId = 1,
        Title = "The Expanse",
        Path = "/tv/The Expanse",
        DateAdded = DateTime.UtcNow,
        Seasons = []
    };

    [Fact]
    public async Task SyncSeason_FlatStructure_ResolvesSeasonPath()
    {
        // Arrange
        // /tv/The Expanse/Season 1/The Expanse - S01E01.mkv
        var sonarrMock = new Mock<ISonarrService>();
        sonarrMock.Setup(s => s.GetEpisodes(1, 1))
            .ReturnsAsync([new SonarrEpisode { Id = 10, EpisodeNumber = 1, SeasonNumber = 1, HasFile = true, Title = "Dulcinea" }]);
        sonarrMock.Setup(s => s.GetEpisodePath(10))
            .ReturnsAsync(new SonarrEpisodePath
            {
                EpisodeFile = new SonarrEpisodeFile
                {
                    Path = "/tv/The Expanse/Season 1/The Expanse - S01E01.mkv"
                }
            });

        var (sync, _) = CreateSeasonSync(sonarrMock);

        // Act
        var result = await sync.SyncSeason(
            MakeShowEntity(),
            MakeSonarrShow(),
            new SonarrSeason { SeasonNumber = 1 });

        // Assert
        Assert.Equal("/tv/The Expanse/Season 1", result.Path);
    }

    [Fact]
    public async Task SyncSeason_PerEpisodeFolderStructure_ResolvesSeasonPath()
    {
        // Arrange
        // /tv/The Expanse/Season 1/S01E01/The Expanse - S01E01.mkv
        var sonarrMock = new Mock<ISonarrService>();
        sonarrMock.Setup(s => s.GetEpisodes(1, 1))
            .ReturnsAsync([new SonarrEpisode { Id = 10, EpisodeNumber = 1, SeasonNumber = 1, HasFile = true, Title = "Dulcinea" }]);
        sonarrMock.Setup(s => s.GetEpisodePath(10))
            .ReturnsAsync(new SonarrEpisodePath
            {
                EpisodeFile = new SonarrEpisodeFile
                {
                    Path = "/tv/The Expanse/Season 1/S01E01/The Expanse - S01E01.mkv"
                }
            });

        var (sync, _) = CreateSeasonSync(sonarrMock);

        // Act
        var result = await sync.SyncSeason(
            MakeShowEntity(),
            MakeSonarrShow(),
            new SonarrSeason { SeasonNumber = 1 });

        // Assert
        Assert.Equal("/tv/The Expanse/Season 1", result.Path);
    }
}
