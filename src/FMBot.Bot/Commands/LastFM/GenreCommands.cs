using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord.Commands;
using Fergun.Interactive;
using FMBot.Bot.Attributes;
using FMBot.Bot.Extensions;
using FMBot.Bot.Interfaces;
using FMBot.Bot.Resources;
using FMBot.Bot.Services;
using FMBot.Bot.Services.ThirdParty;
using FMBot.Domain;
using FMBot.Domain.Models;
using FMBot.LastFM.Domain.Types;
using FMBot.LastFM.Repositories;
using Humanizer;
using Microsoft.Extensions.Options;
using StringExtensions = FMBot.Bot.Extensions.StringExtensions;
using TimePeriod = FMBot.Domain.Models.TimePeriod;

namespace FMBot.Bot.Commands.LastFM
{
    public class GenreCommands : BaseCommandModule
    {
        private readonly IPrefixService _prefixService;
        private readonly UserService _userService;
        private readonly SettingService _settingService;
        private readonly LastFmRepository _lastFmRepository;
        private readonly PlayService _playService;
        private readonly GenreService _genreService;
        private readonly ArtistsService _artistsService;
        private readonly SpotifyService _spotifyService;
        private InteractiveService Interactivity { get; }

        public GenreCommands(
            IPrefixService prefixService,
            IOptions<BotSettings> botSettings,
            UserService userService,
            SettingService settingService,
            LastFmRepository lastFmRepository,
            PlayService playService,
            InteractiveService interactivity,
            GenreService genreService,
            ArtistsService artistsService,
            SpotifyService spotifyService) : base(botSettings)
        {
            this._prefixService = prefixService;
            this._userService = userService;
            this._settingService = settingService;
            this._lastFmRepository = lastFmRepository;
            this._playService = playService;
            this.Interactivity = interactivity;
            this._genreService = genreService;
            this._artistsService = artistsService;
            this._spotifyService = spotifyService;
        }

        [Command("topgenres", RunMode = RunMode.Async)]
        [Summary("Shows a list of your or someone else their top genres over a certain time period.")]
        [Options(Constants.CompactTimePeriodList, Constants.UserMentionExample)]
        [Examples("tg", "topgenres", "tg a lfm:fm-bot", "topgenres weekly @user")]
        [Alias("gl", "tg", "genrelist", "genres", "top genres", "genreslist")]
        [UsernameSetRequired]
        [SupportsPagination]
        public async Task TopGenresAsync([Remainder] string extraOptions = null)
        {
            var contextUser = await this._userService.GetUserSettingsAsync(this.Context.User);

            _ = this.Context.Channel.TriggerTypingAsync();

            var timeSettings = SettingService.GetTimePeriod(extraOptions);
            var userSettings = await this._settingService.GetUser(extraOptions, contextUser, this.Context);

            var pages = new List<PageBuilder>();

            string userTitle;
            if (!userSettings.DifferentUser)
            {
                this._embedAuthor.WithIconUrl(this.Context.User.GetAvatarUrl());
                userTitle = await this._userService.GetUserTitleAsync(this.Context);
            }
            else
            {
                userTitle =
                    $"{userSettings.UserNameLastFm}, requested by {await this._userService.GetUserTitleAsync(this.Context)}";
            }

            this._embedAuthor.WithName($"Top {timeSettings.Description.ToLower()} artist genres for {userTitle}");
            this._embedAuthor.WithUrl($"{Constants.LastFMUserUrl}{userSettings.UserNameLastFm}/library/artists?{timeSettings.UrlParameter}");

            try
            {
                Response<TopArtistList> artists;

                if (!timeSettings.UsePlays && timeSettings.TimePeriod != TimePeriod.AllTime)
                {
                    artists = await this._lastFmRepository.GetTopArtistsAsync(userSettings.UserNameLastFm,
                        timeSettings.TimePeriod, 1000);

                    if (!artists.Success || artists.Content == null)
                    {
                        this._embed.ErrorResponse(artists.Error, artists.Message, this.Context);
                        this.Context.LogCommandUsed(CommandResponse.LastFmError);
                        await ReplyAsync("", false, this._embed.Build());
                        return;
                    }
                }
                else if (timeSettings.TimePeriod == TimePeriod.AllTime)
                {
                    artists = new Response<TopArtistList>
                    {
                        Content = new TopArtistList
                        {
                            TopArtists = await this._artistsService.GetUserAllTimeTopArtists(userSettings.UserId, true)
                        }
                    };
                }
                else
                {
                    artists = new Response<TopArtistList>
                    {
                        Content = await this._playService.GetTopArtists(userSettings.UserId,
                            timeSettings.PlayDays.GetValueOrDefault())
                    };
                }

                if (artists.Content.TopArtists.Count < 10)
                {
                    this._embed.WithDescription("Sorry, you don't have enough top artists yet to use this command.\n\n" +
                                                "Please try again later.");
                    await this.Context.Channel.SendMessageAsync("", false, this._embed.Build());
                    this.Context.LogCommandUsed(CommandResponse.NoScrobbles);
                    return;
                }

                var genres = await this._genreService.GetTopGenresForTopArtists(artists.Content.TopArtists);

                var genrePages = genres.ChunkBy(10);

                var counter = 1;
                var pageCounter = 1;
                foreach (var genrePage in genrePages)
                {
                    var genrePageString = new StringBuilder();
                    foreach (var genre in genrePage)
                    {
                        genrePageString.AppendLine($"{counter}. **{genre.GenreName.Humanize(LetterCasing.Title)}**");
                        counter++;
                    }

                    var footer = $"Genre source: Spotify\n" +
                                 $"Page {pageCounter}/{genrePages.Count} - {genres.Count} total genres";

                    pages.Add(new PageBuilder()
                        .WithDescription(genrePageString.ToString())
                        .WithAuthor(this._embedAuthor)
                        .WithFooter(footer));
                    pageCounter++;
                }

                var paginator = StringService.BuildStaticPaginator(pages);

                _ = this.Interactivity.SendPaginatorAsync(
                    paginator,
                    this.Context.Channel,
                    TimeSpan.FromMinutes(DiscordConstants.PaginationTimeoutInSeconds));

                this.Context.LogCommandUsed();
            }
            catch (Exception e)
            {
                this.Context.LogCommandException(e);
                await ReplyAsync("Unable to show genre info due to an internal error. Please contact .fmbot staff.");
            }
        }

        [Command("genre", RunMode = RunMode.Async)]
        [Summary("Shows your top artists for a specific genre")]
        [Options(Constants.CompactTimePeriodList, Constants.UserMentionExample)]
        [Examples("genre", "genres hip hop, electronic", "g", "genre Indie Soul")]
        [Alias("genreinfo", "genres", "gi", "g")]
        [UsernameSetRequired]
        [SupportsPagination]
        public async Task GenreInfoAsync([Remainder] string genreOptions = null)
        {
            var contextUser = await this._userService.GetUserSettingsAsync(this.Context.User);

            _ = this.Context.Channel.TriggerTypingAsync();

            try
            {
                var genres = new List<string>();
                if (string.IsNullOrWhiteSpace(genreOptions))
                {
                    var recentScrobbles = await this._lastFmRepository.GetRecentTracksAsync(contextUser.UserNameLastFM, 1, true, contextUser.SessionKeyLastFm);

                    if (await GenericEmbedService.RecentScrobbleCallFailedReply(recentScrobbles, contextUser.UserNameLastFM, this.Context))
                    {
                        return;
                    }

                    var artistName = recentScrobbles.Content.RecentTracks.First().ArtistName;

                    var foundGenres = await this._genreService.GetGenresForArtist(artistName);

                    if (foundGenres == null)
                    {
                        var artistCall = await this._lastFmRepository.GetArtistInfoAsync(artistName, contextUser.UserNameLastFM);
                        if (artistCall.Success)
                        {
                            var cachedArtist = await this._spotifyService.GetOrStoreArtistAsync(artistCall.Content);

                            if (cachedArtist.ArtistGenres != null && cachedArtist.ArtistGenres.Any())
                            {
                                genres.AddRange(cachedArtist.ArtistGenres.Select(s => s.Name));
                            }
                        }
                    }
                    else
                    {
                        genres.AddRange(foundGenres);
                    }

                    if (genres.Any())
                    {
                        var artist = await this._artistsService.GetArtistFromDatabase(artistName);
                        var userTopArtists = await this._artistsService.GetUserAllTimeTopArtists(contextUser.UserId, true);
                        var topGenres = await this._genreService.GetArtistsForGenres(genres, userTopArtists);

                        this._embed.WithTitle($"Genre info for '{artistName}'");

                        var genreDescription = new StringBuilder();
                        foreach (var topGenre in topGenres)
                        {
                            genreDescription.AppendLine($"- **{topGenre.GenreName.Transform(To.TitleCase)}**");
                        }

                        if (artist?.SpotifyImageUrl != null)
                        {
                            this._embed.WithThumbnailUrl(artist.SpotifyImageUrl);
                        }

                        this._embed.WithDescription(genreDescription.ToString());

                        this._embed.WithFooter($"Genre source: Spotify\n" +
                                               $"Add a genre to this command to see top artists");

                        await this.Context.Channel.SendMessageAsync("", false, this._embed.Build());
                        return;
                    }
                }
                else
                {
                    genres = new List<string> { genreOptions };
                }

                if (!genres.Any())
                {
                    var prfx = this._prefixService.GetPrefix(this.Context.Guild?.Id);
                    this._embed.WithDescription(
                        "Sorry, we don't have any registered genres for the artist you're currently listening to.\n\n" +
                        $"Please try again later or manually enter a genre (example: `{prfx}genre hip hop`)");
                    await this.Context.Channel.SendMessageAsync("", false, this._embed.Build());
                    this.Context.LogCommandUsed(CommandResponse.NotFound);
                    return;
                }

                var topArtists = await this._artistsService.GetUserAllTimeTopArtists(contextUser.UserId, true);
                if (topArtists.Count < 100)
                {
                    this._embed.WithDescription("Sorry, you don't have enough top artists yet to use this command.\n\n" +
                                                "Please try again later.");
                    await this.Context.Channel.SendMessageAsync("", false, this._embed.Build());
                    this.Context.LogCommandUsed(CommandResponse.NoScrobbles);
                    return;
                }

                var genresWithArtists = await this._genreService.GetArtistsForGenres(genres, topArtists);

                if (!genresWithArtists.Any())
                {
                    this._embed.WithDescription("Sorry, we couldn't find any top artists for your selected genres.");
                    await this.Context.Channel.SendMessageAsync("", false, this._embed.Build());
                    this.Context.LogCommandUsed(CommandResponse.NotFound);
                    return;
                }

                var userTitle = await this._userService.GetUserTitleAsync(this.Context);

                var pages = new List<PageBuilder>();

                var genre = genresWithArtists.First();

                if (!genre.Artists.Any())
                {
                    var prfx = this._prefixService.GetPrefix(this.Context.Guild?.Id);
                    this._embed.WithDescription(
                        "Sorry, we don't have any registered artists for you for the genre you're searching for.");
                    await this.Context.Channel.SendMessageAsync("", false, this._embed.Build());
                    this.Context.LogCommandUsed(CommandResponse.NotFound);
                    return;
                }

                this._embedAuthor.WithIconUrl(this.Context.User.GetAvatarUrl());
                this._embedAuthor.WithName($"Top '{genre.GenreName.Transform(To.TitleCase)}' artists for {userTitle}");

                var genrePages = genre.Artists.ChunkBy(10);

                var counter = 1;
                var pageCounter = 1;
                foreach (var genrePage in genrePages)
                {
                    var genrePageString = new StringBuilder();
                    foreach (var genreArtist in genrePage)
                    {
                        genrePageString.AppendLine($"{counter}. **{genreArtist.ArtistName}** ({genreArtist.UserPlaycount} {StringExtensions.GetPlaysString(genreArtist.UserPlaycount)})");
                        counter++;
                    }

                    var footer = $"Genre source: Spotify\n" +
                                 $"Page {pageCounter}/{genrePages.Count} - {genre.Artists.Count} total artists";

                    pages.Add(new PageBuilder()
                        .WithDescription(genrePageString.ToString())
                        .WithAuthor(this._embedAuthor)
                        .WithFooter(footer));
                    pageCounter++;
                }

                var paginator = StringService.BuildStaticPaginator(pages);

                _ = this.Interactivity.SendPaginatorAsync(
                    paginator,
                    this.Context.Channel,
                    TimeSpan.FromMinutes(DiscordConstants.PaginationTimeoutInSeconds));

                this.Context.LogCommandUsed();
            }
            catch (Exception e)
            {
                this.Context.LogCommandException(e);
                await ReplyAsync("Unable to show genre info due to an internal error. Please contact .fmbot staff.");
            }
        }
    }
}
