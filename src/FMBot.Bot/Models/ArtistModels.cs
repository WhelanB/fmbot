using System;
using FMBot.Domain.Models;

namespace FMBot.Bot.Models
{
    public class GuildRankingSettings
    {
        public OrderType OrderType { get; set; }

        public TimePeriod ChartTimePeriod { get; set; }

        public string TimeDescription { get; set; }

        public int AmountOfDays { get; set; }

        public string NewSearchValue { get; set; }
    }

    public enum OrderType
    {
        Playcount = 1,
        Listeners = 2
    }

    public class ListArtist
    {
        public string ArtistName { get; set; }

        public int TotalPlaycount { get; set; }

        public int ListenerCount { get; set; }
    }

    public class WhoKnowsArtistDto
    {
        public int UserId { get; set; }

        public string Name { get; set; }

        public int Playcount { get; set; }

        public string UserNameLastFm { get; set; }

        public ulong DiscordUserId { get; set; }

        public string UserName { get; set; }

        public bool? WhoKnowsWhitelisted { get; set; }
    }

    public class WhoKnowsGlobalArtistDto
    {
        public int UserId { get; set; }

        public string Name { get; set; }

        public int Playcount { get; set; }

        public string UserNameLastFm { get; set; }

        public ulong DiscordUserId { get; set; }

        public DateTime? RegisteredLastFm { get; set; }

        public PrivacyLevel PrivacyLevel { get; set; }
    }

    public class ArtistSpotifyCoverDto
    {
        public string LastFmUrl { get; set; }

        public string SpotifyImageUrl { get; set; }
    }

    public class TopGuildArtistsDto
    {
        public string Name { get; set; }

        public int TotalPlaycount { get; set; }

        public int TotalUsers { get; set; }
    }

    public class AffinityArtist
    {
        public int UserId { get; set; }

        public string ArtistName { get; set; }

        public long Playcount { get; set; }

        public decimal Weight { get; set; }
    }

    public class AffinityArtistResultWithUser
    {
        public string Name { get; set; }

        public decimal MatchPercentage { get; set; }

        public string LastFMUsername { get; set; }

        public ulong DiscordUserId { get; set; }

        public int UserId { get; set; }
    }
}
