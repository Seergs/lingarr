using Lingarr.Core.Data;
using Lingarr.Core.Entities;
using Lingarr.Core.Enum;
using Lingarr.Server.Interfaces.Services.Integration;
using Lingarr.Server.Interfaces.Services.Sync;
using Lingarr.Server.Models.Integrations;
using Microsoft.EntityFrameworkCore;

namespace Lingarr.Server.Services.Sync;

public class SeasonSync : ISeasonSync
{
    private readonly LingarrDbContext _dbContext;
    private readonly ISonarrService _sonarrService;
    private readonly PathConversionService _pathConversionService;
    private readonly ILogger<SeasonSync> _logger;

    public SeasonSync(
        LingarrDbContext dbContext,
        ISonarrService sonarrService,
        PathConversionService pathConversionService,
        ILogger<SeasonSync> logger)
    {
        _dbContext = dbContext;
        _sonarrService = sonarrService;
        _pathConversionService = pathConversionService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Season> SyncSeason(Show show, SonarrShow sonarrShow, SonarrSeason season, bool defaultInclude = true)
    {
        var seasonPath = await GetSeasonPath(sonarrShow, season);
        
        var seasonEntity = await _dbContext.Seasons
            .Include(s => s.Episodes)
            .FirstOrDefaultAsync(s => s.ShowId == show.Id && s.SeasonNumber == season.SeasonNumber);

        if (seasonEntity == null)
        {
            seasonEntity = new Season
            {
                SeasonNumber = season.SeasonNumber,
                Path = seasonPath,
                Show = show,
                IncludeInTranslation = defaultInclude
            };
            show.Seasons.Add(seasonEntity);
        }
        else
        {
            seasonEntity.SeasonNumber = season.SeasonNumber;
            seasonEntity.Path = seasonPath;
            seasonEntity.Show = show;
        }

        return seasonEntity;
    }

    /// <summary>
    /// Retrieves and formats the season path from an episode within the season
    /// </summary>
    /// <param name="show">The Sonarr show containing the season</param>
    /// <param name="season">The Sonarr season to get the path for</param>
    /// <returns>The converted and mapped path for the season, or an empty string if no path could be determined</returns>
    private async Task<string> GetSeasonPath(SonarrShow show, SonarrSeason season)
    {
        var episodes = await _sonarrService.GetEpisodes(show.Id, season.SeasonNumber);
        var episode = episodes?.Where(episode => episode.HasFile).FirstOrDefault();
        if (episode == null)
        {
            return string.Empty;
        }
        
        var episodePathResult = await _sonarrService.GetEpisodePath(episode.Id);
        var normalizePath = _pathConversionService.NormalizePath(episodePathResult?.EpisodeFile.Path ?? string.Empty);

        // Get the directory containing the episode file.
        // Depending on the library structure, this may be the season folder directly (flat)
        // or a per-episode subfolder one level deeper (eg. Season 1/S01E01/episode.mkv)
        var episodeDir = Path.GetDirectoryName(normalizePath);
        var normalizedShowPath = _pathConversionService.NormalizePath(show.Path).TrimEnd('/');
        var seasonPath = Path.GetDirectoryName(episodeDir) is { } parent
          && parent.TrimEnd('/') != normalizedShowPath
          ? parent
          : episodeDir;

        _logger.LogInformation("Syncing episode: {episode.Id} with Path: {seasonPath}", episode.Id, seasonPath);

        if (seasonPath != null)
        {
            if (!seasonPath.StartsWith("/"))
            {
                seasonPath = $"/{seasonPath}";
            }
        }
        else
        {
            seasonPath = $"/Season {season.SeasonNumber}";
        }

        return _pathConversionService.ConvertAndMapPath(
            seasonPath,
            MediaType.Show
        );
    }
}
