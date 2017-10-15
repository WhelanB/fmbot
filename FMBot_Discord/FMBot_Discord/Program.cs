﻿using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using Newtonsoft.Json;
using Discord;
using Discord.WebSocket;
using Discord.Net.Providers.WS4Net;
using Discord.Commands;
using System.Collections;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using IF.Lastfm.Core.Api;
using IF.Lastfm.Core.Objects;
using IF.Lastfm.Core.Api.Enums;
using YoutubeSearch;
using System.Collections.Generic;

namespace FMBot_Discord
{
    class Program
    {
        private CommandService commands;
        private DiscordSocketClient client;
        private IServiceProvider services;
        private string prefix;

        public static void Main(string[] args)
            => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            // first, let's load our configuration file
            Console.WriteLine("[FMBot] Loading Configuration");
            var json = "";
            using (var fs = File.OpenRead(GlobalVars.ConfigFileName))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            if (!File.Exists(GlobalVars.DBFileName))
            {
                Console.WriteLine("[FMBot] Warning: DB not found. Creating empty DB file.");
                File.Create(GlobalVars.DBFileName).Dispose();
                Console.WriteLine("[FMBot] DB File exists. Loading.");
            }
            else
            {
                Console.WriteLine("[FMBot] DB File exists. Loading.");
            }

            // next, let's load the values from that file
            // to our client's configuration
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);

            Console.WriteLine("[FMBot] Initalizing Discord");
            client = new DiscordSocketClient(new DiscordSocketConfig
            {
                WebSocketProvider = WS4NetProvider.Instance,
                LogLevel = LogSeverity.Verbose
            });

            await client.SetGameAsync("🎶");

            client.Log += Log;

            prefix = cfgjson.CommandPrefix;

            Console.WriteLine("[FMBot] Registering Commands");
            commands = new CommandService();

            string token = cfgjson.Token; // Remember to keep this private!

            services = new ServiceCollection()
                .BuildServiceProvider();

            await InstallCommands();

            Console.WriteLine("[FMBot] Logging In");
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        private Task Log(LogMessage arg)
        {
            Console.WriteLine(arg);

            return Task.CompletedTask;
        }

        public async Task InstallCommands()
        {
            // Hook the MessageReceived Event into our Command Handler
            client.MessageReceived += HandleCommand;
            // Discover all of the commands in this assembly and load them.
            await commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        public async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;

            var DiscordCaller = message.Author;

            // first, let's load our configuration file
            var json = "";
            using (var fs = File.OpenRead(GlobalVars.ConfigFileName))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            // next, let's load the values from that file
            // to our client's configuration
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);

            if (User.IncomingRequest(DiscordCaller.Id, double.Parse(cfgjson.Cooldown)) == false) return;
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;
            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix(Convert.ToChar(prefix), ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos))) return;
            // Create a Command Context
            var context = new CommandContext(client, message);
            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed successfully)
            var result = await commands.ExecuteAsync(context, argPos, services);
            if (!result.IsSuccess)
                Console.WriteLine("[FMBot]: Error - " + result.Error + ": " + result.ErrorReason);
        }
    }


    public class FMCommands : ModuleBase
    {
        private readonly CommandService _service;

        public FMCommands(CommandService service)
        {
            _service = service;
        }

        [Command("fm")]
        public async Task fmAsync(IUser user = null)
        {
            var DiscordUser = user ?? Context.Message.Author;
            string LastFMName = DBase.GetNameForID(GlobalVars.DBFileName, DiscordUser.Id.ToString());
            if (LastFMName.Equals("NULL"))
            {
                await ReplyAsync("Your Last.FM name was unable to be found. Please use .fmset to set your name.");
            }
            else
            {
                // first, let's load our configuration file
                Console.WriteLine("[FMBot] Loading Configuration");
                var json = "";
                using (var fs = File.OpenRead(GlobalVars.ConfigFileName))
                using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                    json = await sr.ReadToEndAsync();

                // next, let's load the values from that file
                // to our client's configuration
                var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
                var client = new LastfmClient(cfgjson.FMKey, cfgjson.FMSecret);
                var tracks = await client.User.GetRecentScrobbles(LastFMName, null, 1, 2);
                LastTrack currentTrack = tracks.Content.ElementAt(0);
                LastTrack lastTrack = tracks.Content.ElementAt(1);
                EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                eab.IconUrl = DiscordUser.GetAvatarUrl();
                eab.Name = DiscordUser.Username + " (" + LastFMName + ")";
                eab.Url = "https://www.last.fm/user/" + LastFMName;

                var builder = new EmbedBuilder();

                builder.WithAuthor(eab);
                builder.WithDescription("Recently Played");

                string nulltext = "[undefined]";

                string TrackName = string.IsNullOrWhiteSpace(currentTrack.Name) ? nulltext : currentTrack.Name;
                string ArtistName = string.IsNullOrWhiteSpace(currentTrack.ArtistName) ? nulltext : currentTrack.ArtistName;
                string AlbumName = string.IsNullOrWhiteSpace(currentTrack.AlbumName) ? nulltext : currentTrack.AlbumName;

                string LastTrackName = string.IsNullOrWhiteSpace(lastTrack.Name) ? nulltext : lastTrack.Name;
                string LastArtistName = string.IsNullOrWhiteSpace(lastTrack.ArtistName) ? nulltext : lastTrack.ArtistName;
                string LastAlbumName = string.IsNullOrWhiteSpace(lastTrack.AlbumName) ? nulltext : lastTrack.AlbumName;

                try
                {
                    var AlbumInfo = await client.Album.GetInfoAsync(ArtistName, AlbumName);
                    var AlbumImages = (AlbumInfo.Content.Images != null) ? AlbumInfo.Content.Images : null;
                    var AlbumThumbnail = (AlbumImages != null) ? AlbumImages.Large.AbsoluteUri : null;
                    string ThumbnailImage = (AlbumThumbnail != null) ? AlbumThumbnail.ToString() : null;

                    if (!string.IsNullOrWhiteSpace(ThumbnailImage))
                    {
                        builder.WithThumbnailUrl(ThumbnailImage);
                    }
                }
                catch (Exception)
                {
                    builder.WithThumbnailUrl("missingimg.png");
                }

                builder.AddInlineField("Recent Track", TrackName);
                builder.AddInlineField(AlbumName, ArtistName);
                builder.AddInlineField("Previous Track", LastTrackName);
                builder.AddInlineField(LastAlbumName, LastArtistName);

                EmbedFooterBuilder efb = new EmbedFooterBuilder();
                efb.IconUrl = Context.Client.CurrentUser.GetAvatarUrl();

                var userinfo = await client.User.GetInfoAsync(LastFMName);
                var playcount = userinfo.Content.Playcount;

                efb.Text = LastFMName + "'s Total Tracks: " + playcount.ToString();

                builder.WithFooter(efb);

                await Context.Channel.SendMessageAsync("", false, builder.Build());
            }
        }

        [Command("fmyt")]
        public async Task fmytAsync(IUser user = null)
        {
            var DiscordUser = user ?? Context.Message.Author;
            string LastFMName = DBase.GetNameForID(GlobalVars.DBFileName, DiscordUser.Id.ToString());
            if (LastFMName.Equals("NULL"))
            {
                await ReplyAsync("Your Last.FM name was unable to be found. Please use .fmset to set your name.");
            }
            else
            {
                // first, let's load our configuration file
                Console.WriteLine("[FMBot] Loading Configuration");
                var json = "";
                using (var fs = File.OpenRead(GlobalVars.ConfigFileName))
                using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                    json = await sr.ReadToEndAsync();

                // next, let's load the values from that file
                // to our client's configuration
                var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
                var client = new LastfmClient(cfgjson.FMKey, cfgjson.FMSecret);
                var tracks = await client.User.GetRecentScrobbles(LastFMName, null, 1, 2);
                LastTrack currentTrack = tracks.Content.ElementAt(0);
                LastTrack lastTrack = tracks.Content.ElementAt(1);

                string TrackName = string.IsNullOrWhiteSpace(currentTrack.Name) ? null : currentTrack.Name;
                string ArtistName = string.IsNullOrWhiteSpace(currentTrack.ArtistName) ? null : currentTrack.ArtistName;
                string AlbumName = string.IsNullOrWhiteSpace(currentTrack.AlbumName) ? null : currentTrack.AlbumName;

                try
                {
                    string querystring = TrackName + " - " + ArtistName + " " + AlbumName;
                    var items = new VideoSearch();
                    var item = items.SearchQuery(querystring, 1).ElementAt(0);

                    await Context.Channel.SendMessageAsync(item.Url);
                }
                catch (Exception)
                {
                }

                EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                eab.IconUrl = DiscordUser.GetAvatarUrl();
                eab.Name = DiscordUser.Username + " (" + LastFMName + ")";
                eab.Url = "https://www.last.fm/user/" + LastFMName;

                var builder = new EmbedBuilder();

                builder.WithAuthor(eab);
                builder.WithDescription("Recently Played on YouTube");

                string nulltext = "[undefined]";

                string TrackName2 = string.IsNullOrWhiteSpace(TrackName) ? nulltext : TrackName;
                string ArtistName2 = string.IsNullOrWhiteSpace(ArtistName) ? nulltext : ArtistName;
                string AlbumName2 = string.IsNullOrWhiteSpace(AlbumName) ? nulltext : AlbumName;

                builder.AddField(TrackName2, ArtistName2 + " | " + AlbumName2);

                EmbedFooterBuilder efb = new EmbedFooterBuilder();
                efb.IconUrl = Context.Client.CurrentUser.GetAvatarUrl();

                var userinfo = await client.User.GetInfoAsync(LastFMName);
                var playcount = userinfo.Content.Playcount;

                efb.Text = LastFMName + "'s Total Tracks: " + playcount.ToString();

                builder.WithFooter(efb);

                await Context.Channel.SendMessageAsync("", false, builder.Build());
            }
        }

        [Command("fmrecent")]
        public async Task fmrecentAsync(IUser user = null)
        {
            // first, let's load our configuration file
            Console.WriteLine("[FMBot] Loading Configuration");
            var json = "";
            using (var fs = File.OpenRead(GlobalVars.ConfigFileName))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            // next, let's load the values from that file
            // to our client's configuration
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
            int num = int.Parse(cfgjson.Listnum);
            var DiscordUser = user ?? Context.Message.Author;
            string LastFMName = DBase.GetNameForID(GlobalVars.DBFileName, DiscordUser.Id.ToString());
            if (LastFMName.Equals("NULL"))
            {
                await ReplyAsync("Your Last.FM name was unable to be found. Please use .fmset to set your name.");
            }
            else
            {
                var client = new LastfmClient(cfgjson.FMKey, cfgjson.FMSecret);
                var tracks = await client.User.GetRecentScrobbles(LastFMName, null, 1, num);

                EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                eab.IconUrl = DiscordUser.GetAvatarUrl();
                eab.Name = DiscordUser.Username + " (" + LastFMName + ")";
                eab.Url = "https://www.last.fm/user/" + LastFMName;

                var builder = new EmbedBuilder();
                builder.WithAuthor(eab);
                builder.WithDescription("Top " + num + " Recent Track List");

                string nulltext = "[undefined]";
                int indexval = (num - 1);
                for (int i = 0; i <= indexval; i++)
                {
                    LastTrack track = tracks.Content.ElementAt(i);

                    string TrackName = string.IsNullOrWhiteSpace(track.Name) ? nulltext : track.Name;
                    string ArtistName = string.IsNullOrWhiteSpace(track.ArtistName) ? nulltext : track.ArtistName;
                    string AlbumName = string.IsNullOrWhiteSpace(track.AlbumName) ? nulltext : track.AlbumName;

                    try
                    {
                        if (i == 0)
                        {
                            var AlbumInfo = await client.Album.GetInfoAsync(ArtistName, AlbumName);
                            var AlbumImages = (AlbumInfo.Content.Images != null) ? AlbumInfo.Content.Images : null;
                            var AlbumThumbnail = (AlbumImages != null) ? AlbumImages.Large.AbsoluteUri : null;
                            string ThumbnailImage = (AlbumThumbnail != null) ? AlbumThumbnail.ToString() : null;

                            if (!string.IsNullOrWhiteSpace(ThumbnailImage))
                            {
                                builder.WithThumbnailUrl(ThumbnailImage);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        builder.WithThumbnailUrl("missingimg.png");
                    }

                    int correctnum = (i + 1);
                    builder.AddField("Track #" + correctnum.ToString() + ":", TrackName + " - " + ArtistName + " | " + AlbumName);
                }

                EmbedFooterBuilder efb = new EmbedFooterBuilder();
                efb.IconUrl = Context.Client.CurrentUser.GetAvatarUrl();

                var userinfo = await client.User.GetInfoAsync(LastFMName);
                var playcount = userinfo.Content.Playcount;

                efb.Text = LastFMName + "'s Total Tracks: " + playcount.ToString();

                builder.WithFooter(efb);

                await Context.Channel.SendMessageAsync("", false, builder.Build());
            }
        }

        [Command("fmartists")]
        public async Task fmartistsAsync(IUser user = null)
        {
            // first, let's load our configuration file
            Console.WriteLine("[FMBot] Loading Configuration");
            var json = "";
            using (var fs = File.OpenRead(GlobalVars.ConfigFileName))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            // next, let's load the values from that file
            // to our client's configuration
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
            int num = int.Parse(cfgjson.Listnum);
            var DiscordUser = user ?? Context.Message.Author;
            string LastFMName = DBase.GetNameForID(GlobalVars.DBFileName, DiscordUser.Id.ToString());
            if (LastFMName.Equals("NULL"))
            {
                await ReplyAsync("Your Last.FM name was unable to be found. Please use .fmset to set your name.");
            }
            else
            {
                var client = new LastfmClient(cfgjson.FMKey, cfgjson.FMSecret);
                var artists = await client.User.GetTopArtists(LastFMName, LastStatsTimeSpan.Overall, 1, num);

                EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                eab.IconUrl = DiscordUser.GetAvatarUrl();
                eab.Name = DiscordUser.Username + " (" + LastFMName + ")";
                eab.Url = "https://www.last.fm/user/" + LastFMName;

                var builder = new EmbedBuilder();
                builder.WithAuthor(eab);
                builder.WithDescription("Top " + num + " Artist List");

                string nulltext = "[undefined]";
                int indexval = (num - 1);
                for (int i = 0; i <= indexval; i++)
                {
                    LastArtist artist = artists.Content.ElementAt(i);

                    string ArtistName = string.IsNullOrWhiteSpace(artist.Name) ? nulltext : artist.Name;

                    try
                    {
                        if (i == 0)
                        {
                            var ArtistInfo = await client.Artist.GetInfoAsync(ArtistName);
                            var ArtistImages = (ArtistInfo.Content.MainImage != null) ? ArtistInfo.Content.MainImage : null;
                            var ArtistThumbnail = (ArtistImages != null) ? ArtistImages.Large.AbsoluteUri : null;
                            string ThumbnailImage = (ArtistThumbnail != null) ? ArtistThumbnail.ToString() : null;

                            if (!string.IsNullOrWhiteSpace(ThumbnailImage))
                            {
                                builder.WithThumbnailUrl(ThumbnailImage);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        builder.WithThumbnailUrl("missingimg.png");
                    }

                    int correctnum = (i + 1);
                    builder.AddField("Artist #" + correctnum.ToString() + ":", ArtistName);
                }

                EmbedFooterBuilder efb = new EmbedFooterBuilder();
                efb.IconUrl = Context.Client.CurrentUser.GetAvatarUrl();

                var userinfo = await client.User.GetInfoAsync(LastFMName);
                var playcount = userinfo.Content.Playcount;

                efb.Text = LastFMName + "'s Total Tracks: " + playcount.ToString();

                builder.WithFooter(efb);

                await Context.Channel.SendMessageAsync("", false, builder.Build());
            }
        }

        [Command("fmalbums")]
        public async Task fmalbumsAsync(IUser user = null)
        {
            // first, let's load our configuration file
            Console.WriteLine("[FMBot] Loading Configuration");
            var json = "";
            using (var fs = File.OpenRead(GlobalVars.ConfigFileName))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            // next, let's load the values from that file
            // to our client's configuration
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
            int num = int.Parse(cfgjson.Listnum);
            var DiscordUser = user ?? Context.Message.Author;
            string LastFMName = DBase.GetNameForID(GlobalVars.DBFileName, DiscordUser.Id.ToString());
            if (LastFMName.Equals("NULL"))
            {
                await ReplyAsync("Your Last.FM name was unable to be found. Please use .fmset to set your name.");
            }
            else
            {
                var client = new LastfmClient(cfgjson.FMKey, cfgjson.FMSecret);
                var albums = await client.User.GetTopAlbums(LastFMName, LastStatsTimeSpan.Overall, 1, num);

                EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                eab.IconUrl = DiscordUser.GetAvatarUrl();
                eab.Name = DiscordUser.Username + " (" + LastFMName + ")";
                eab.Url = "https://www.last.fm/user/" + LastFMName;

                var builder = new EmbedBuilder();
                builder.WithAuthor(eab);
                builder.WithDescription("Top " + num + " Album List");

                string nulltext = "[undefined]";
                int indexval = (num - 1);
                for (int i = 0; i <= indexval; i++)
                {
                    LastAlbum album = albums.Content.ElementAt(i);

                    string AlbumName = string.IsNullOrWhiteSpace(album.Name) ? nulltext : album.Name;
                    string ArtistName = string.IsNullOrWhiteSpace(album.ArtistName) ? nulltext : album.ArtistName;

                    try
                    {
                        if (i == 0)
                        {
                            var AlbumInfo = await client.Album.GetInfoAsync(ArtistName, AlbumName);
                            var AlbumImages = (AlbumInfo.Content.Images != null) ? AlbumInfo.Content.Images : null;
                            var AlbumThumbnail = (AlbumImages != null) ? AlbumImages.Large.AbsoluteUri : null;
                            string ThumbnailImage = (AlbumThumbnail != null) ? AlbumThumbnail.ToString() : null;

                            if (!string.IsNullOrWhiteSpace(ThumbnailImage))
                            {
                                builder.WithThumbnailUrl(ThumbnailImage);
                            }
                        }
                    }
                    catch (Exception)
                    {
                        builder.WithThumbnailUrl("missingimg.png");
                    }

                    int correctnum = (i + 1);
                    builder.AddField("Album #" + correctnum.ToString() + ":", AlbumName + " | " + ArtistName);
                }

                EmbedFooterBuilder efb = new EmbedFooterBuilder();
                efb.IconUrl = Context.Client.CurrentUser.GetAvatarUrl();

                var userinfo = await client.User.GetInfoAsync(LastFMName);
                var playcount = userinfo.Content.Playcount;

                efb.Text = LastFMName + "'s Total Tracks: " + playcount.ToString();

                builder.WithFooter(efb);

                await Context.Channel.SendMessageAsync("", false, builder.Build());
            }
        }

        [Command("fmchart")]
        public async Task fmachartAsync(IUser user = null)
        {
            // first, let's load our configuration file
            Console.WriteLine("[FMBot] Loading Configuration");
            var json = "";
            using (var fs = File.OpenRead(GlobalVars.ConfigFileName))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            // next, let's load the values from that file
            // to our client's configuration
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
            int num = int.Parse(cfgjson.Listnum);
            var DiscordUser = user ?? Context.Message.Author;
            string LastFMName = DBase.GetNameForID(GlobalVars.DBFileName, DiscordUser.Id.ToString());
            if (LastFMName.Equals("NULL"))
            {
                await ReplyAsync("Your Last.FM name was unable to be found. Please use .fmset to set your name.");
            }
            else
            {
                var client = new LastfmClient(cfgjson.FMKey, cfgjson.FMSecret);
                var albums = await client.User.GetTopAlbums(LastFMName, LastStatsTimeSpan.Overall, 1, num);

                EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                eab.IconUrl = DiscordUser.GetAvatarUrl();
                eab.Name = DiscordUser.Username + " (" + LastFMName + ")";
                eab.Url = "https://www.last.fm/user/" + LastFMName;

                var builder = new EmbedBuilder();
                builder.WithAuthor(eab);
                builder.WithDescription("Top " + num + " Album Chart (Beta)");

                string nulltext = "[undefined]";
                int indexval = (num - 1);
                string[] arr = new string[num];
                for (int i = 0; i <= indexval; i++)
                {
                    LastAlbum album = albums.Content.ElementAt(i);

                    string AlbumName = string.IsNullOrWhiteSpace(album.Name) ? nulltext : album.Name;
                    string ArtistName = string.IsNullOrWhiteSpace(album.ArtistName) ? nulltext : album.ArtistName;

                    try
                    {
                        if (i == 0)
                        {
                            var AlbumInfo = await client.Album.GetInfoAsync(ArtistName, AlbumName);
                            var AlbumImages = (AlbumInfo.Content.Images != null) ? AlbumInfo.Content.Images : null;
                            var AlbumThumbnail = (AlbumImages != null) ? AlbumImages.Large.AbsoluteUri : null;
                            string ThumbnailImage = (AlbumThumbnail != null) ? AlbumThumbnail.ToString() : null;

                            if (!string.IsNullOrWhiteSpace(ThumbnailImage))
                            {
                                arr[i] = ThumbnailImage;
                            }
                        }
                    }
                    catch (Exception)
                    {
                        arr[i] = "missingimg.png";
                    }
                }

                try
                {
                    System.Drawing.Image[] images = new System.Drawing.Image[arr.Length];
                    for (int k = 0; k < arr.Length; k++)
                    {
                        images[k] = Utils.DownloadImage(arr[k]);
                    }

                    Random random = new Random();
                    int randid = random.Next(2147483647);
                    string fileName = "FMCache\\FMChart_" + randid + ".jpg";
                    System.Drawing.Image mergedImage = Utils.MergeImages(images);
                    if (mergedImage != null)
                    {
                        mergedImage.Save(fileName);
                    }

                    if (!string.IsNullOrWhiteSpace(fileName))
                    {
                        builder.WithImageUrl(fileName);
                    }
                }
                catch (Exception)
                {
                }

                EmbedFooterBuilder efb = new EmbedFooterBuilder();
                efb.IconUrl = Context.Client.CurrentUser.GetAvatarUrl();

                var userinfo = await client.User.GetInfoAsync(LastFMName);
                var playcount = userinfo.Content.Playcount;

                efb.Text = LastFMName + "'s Total Tracks: " + playcount.ToString();

                builder.WithFooter(efb);

                await Context.Channel.SendMessageAsync("", false, builder.Build());
            }
        }

        [Command("fmstats")]
        public async Task fmstatsAsync(IUser user = null)
        {
            // first, let's load our configuration file
            Console.WriteLine("[FMBot] Loading Configuration");
            var json = "";
            using (var fs = File.OpenRead(GlobalVars.ConfigFileName))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            // next, let's load the values from that file
            // to our client's configuration
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
            int num = int.Parse(cfgjson.Listnum);
            var DiscordUser = user ?? Context.Message.Author;
            string LastFMName = DBase.GetNameForID(GlobalVars.DBFileName, DiscordUser.Id.ToString());
            if (LastFMName.Equals("NULL"))
            {
                await ReplyAsync("Your Last.FM name was unable to be found. Please use .fmset to set your name.");
            }
            else
            {
                var client = new LastfmClient(cfgjson.FMKey, cfgjson.FMSecret);

                EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                eab.IconUrl = DiscordUser.GetAvatarUrl();
                eab.Name = DiscordUser.Username + " (" + LastFMName + ")";
                eab.Url = "https://www.last.fm/user/" + LastFMName;

                var builder = new EmbedBuilder();
                builder.WithAuthor(eab);
                builder.WithDescription("Last.FM Statistics for " + LastFMName);

                var userinfo = await client.User.GetInfoAsync(LastFMName);

                try
                {
                    var userinfoImages = (userinfo.Content.Avatar != null) ? userinfo.Content.Avatar : null;
                    var userinfoThumbnail = (userinfoImages != null) ? userinfoImages.Large.AbsoluteUri : null;
                    string ThumbnailImage = (userinfoThumbnail != null) ? userinfoThumbnail.ToString() : null;

                    if (!string.IsNullOrWhiteSpace(ThumbnailImage))
                    {
                        builder.WithThumbnailUrl(ThumbnailImage);
                    }
                }
                catch (Exception)
                {
                    builder.WithThumbnailUrl("missingimg.png");
                }

                var playcount = userinfo.Content.Playcount;
                var usertype = userinfo.Content.Type;
                var playlists = userinfo.Content.Playlists;
                var premium = userinfo.Content.IsSubscriber;
                string URI = "https://www.last.fm/user/" + LastFMName;

                builder.AddInlineField("Last.FM Name: ", LastFMName);
                builder.AddInlineField("Profile URL: ", URI);
                builder.AddInlineField("User Type: ", usertype.ToString());
                builder.AddInlineField("Total Tracks: ", playcount.ToString());
                builder.AddInlineField("Total Playlists: ", playlists.ToString());
                builder.AddInlineField("Has Premium? ", premium.ToString());

                EmbedFooterBuilder efb = new EmbedFooterBuilder();
                efb.IconUrl = Context.Client.CurrentUser.GetAvatarUrl();

                efb.Text = LastFMName + "'s Total Tracks: " + playcount.ToString();

                builder.WithFooter(efb);

                await Context.Channel.SendMessageAsync("", false, builder.Build());
            }
        }

        [Command("fmset"), Summary("Sets your Last.FM name.")]
        public async Task fmsetAsync([Summary("Your Last.FM name")] string name)
        {
            string SelfID = Context.Message.Author.Id.ToString();
            string LastFMName = DBase.GetNameForID(GlobalVars.DBFileName, SelfID);
            if (LastFMName.Equals("NULL"))
            {
                DBase.WriteEntry(GlobalVars.DBFileName, SelfID, name);
                await ReplyAsync("Your Last.FM name has been set to '" + name + "'.");
            }
            else
            {
                await ReplyAsync("Your Last.FM Name is '" + LastFMName + "'.");
            }
        }

        [Command("fmhelp")]
        public async Task fmhelpAsync()
        {
            // first, let's load our configuration file
            Console.WriteLine("[FMBot] Loading Configuration");
            var json = "";
            using (var fs = File.OpenRead(GlobalVars.ConfigFileName))
            using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                json = await sr.ReadToEndAsync();

            // next, let's load the values from that file
            // to our client's configuration
            var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
            var SelfName = Context.Client.CurrentUser;
            string prefix = cfgjson.CommandPrefix;

            var DiscordUser = Context.Message.Author;

            EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
            eab.IconUrl = SelfName.GetAvatarUrl();
            eab.Name = SelfName.Username;

            var builder = new EmbedBuilder();
            builder.WithAuthor(eab);

            foreach (var module in _service.Modules)
            {
                if (module.Name.Equals("AdminCommands"))
                {
                    continue;
                }

                string description = null;
                foreach (var cmd in module.Commands)
                {
                    var result = await cmd.CheckPreconditionsAsync(Context);
                    if (result.IsSuccess)
                        description += $"{prefix}{cmd.Aliases.First()}\n";
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    builder.AddField(x =>
                    {
                        x.Name = "Commands";
                        x.Value = description;
                        x.IsInline = false;
                    });
                }

                builder.WithThumbnailUrl(SelfName.GetAvatarUrl());
            }

            await ReplyAsync("", false, builder.Build());
        }

        [Command("invite"), Summary("Invites the bot to a server")]
        public async Task inviteAsync()
        {
            string SelfID = Context.Client.CurrentUser.Id.ToString();
            await ReplyAsync("https://discordapp.com/oauth2/authorize?client_id=" + SelfID + "&scope=bot&permissions=0");
        }

        [Command("donate"), Summary("Please donate if you like this bot!")]
        public async Task donateAsync()
        {
            await ReplyAsync("Even though this bot is running for free, this bot needs funds in order to run for more than a year. If you like the bot and you would like to support its development, please donate to me at: https://www.paypal.me/Bitl");
        }

        [Command("github"), Summary("GitHub Page")]
        public async Task githubAsync()
        {
            await ReplyAsync("https://github.com/Bitl/FMBot_Discord");
        }

        [Command("bugs"), Summary("Report bugs here!")]
        public async Task bugsAsync()
        {
            await ReplyAsync("Report bugs here: https://github.com/Bitl/FMBot_Discord/issues");
        }
    }

    public class Utils
    {
        public static System.Drawing.Image DownloadImage(string _URL)
        {
            System.Drawing.Image _tmpImage = null;
            try
            {
                System.Net.HttpWebRequest _HttpWebRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(_URL);
                _HttpWebRequest.AllowWriteStreamBuffering = true;
                _HttpWebRequest.Timeout = 20000;
                System.Net.WebResponse _WebResponse = _HttpWebRequest.GetResponse();
                Stream _WebStream = _WebResponse.GetResponseStream();
                _tmpImage = System.Drawing.Image.FromStream(_WebStream);
                _WebResponse.Close();
            }
            catch (Exception _Exception)
            {
                Console.WriteLine("Exception caught in process: {0}", _Exception.ToString());
                return null;
            }
            return _tmpImage;
        }

        public static System.Drawing.Image MergeImages(System.Drawing.Image[] images)
        {
            if (images == null || images.Length <= 0) { return null; }
            Int32 imageWSize = 0; Int32 imageHSize = 0; for (int i = 0; i < images.Length; i++) { if (images[i].Width > imageWSize) imageWSize = images[i].Width; if (images[i].Height > imageHSize) imageHSize = images[i].Height; }
            Int32 width = 0; Int32 height = 0; int picsInOneLine = 10; if (images.Length >= picsInOneLine) { width = picsInOneLine * imageWSize; decimal d = (images.Length + picsInOneLine) / picsInOneLine; height = (int)Math.Round(d) * imageHSize; } else { width = images.Length * imageWSize; height = imageHSize; }
            Bitmap bitmap = new Bitmap(width, height); int hhh = -1; int www = 0; for (int i = 0; i < images.Length; i++)
            {
                Bitmap image = new Bitmap(images[i]); if (i % picsInOneLine == 0) { hhh++; www = 0; }
                //Get All of the x Pixels         
                for (int w = 0; w < imageWSize; w++)
                {
                    //Get All of the Y Pixels             
                    for (int h = 0; h < imageHSize; h++)
                    {
                        //Set the Cooresponding Pixel                 
                        int ww = w + (www * imageWSize);
                        int hh = h + (hhh * imageHSize);
                        bitmap.SetPixel(ww, hh, image.GetPixel(w, h));
                    }
                }
                www++;
            }
            //Return the new Bitmap     
            return bitmap;
        }
    }

    public class AdminCommands : ModuleBase
    {
        //OwnerIDs = Bitl, Mirage, Opus v84, [opti], GreystarMusic
        private readonly List<ulong> AdminIDs = new List<ulong>(new ulong[] {
            184013824850919425,
            183730395836186624,
            205759116889554946,
            120954630388580355,
            205832344744099840
        });

        private readonly CommandService _service;

        public AdminCommands(CommandService service)
        {
            _service = service;
        }

        private bool IsAdmin(IUser user)
        {
            foreach (ulong item in AdminIDs)
            {
                if (user.Id.Equals(item))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            return false;
        }

        [Command("announce"), Summary("Sends an announcement to the main server.")]
        public async Task announceAsync(string message, string ThumbnailURL = null)
        {
            var DiscordUser = Context.Message.Author;
            var SelfUser = Context.Client.CurrentUser;
            ulong BroadcastChannelID = 209847309385596928;
            ITextChannel channel = await Context.Guild.GetTextChannelAsync(BroadcastChannelID);
            if (IsAdmin(DiscordUser))
            {
                EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                eab.IconUrl = DiscordUser.GetAvatarUrl();
                eab.Name = DiscordUser.Username;

                var builder = new EmbedBuilder();
                builder.WithAuthor(eab);

                try
                {
                    if (!string.IsNullOrWhiteSpace(ThumbnailURL))
                    {
                        builder.WithThumbnailUrl(ThumbnailURL);
                    }
                    else
                    {
                        builder.WithThumbnailUrl(SelfUser.GetAvatarUrl());
                    }
                }
                catch (Exception)
                {
                }

                builder.AddField("Announcement", message);

                await channel.SendMessageAsync("", false, builder.Build());
            }
        }

        [Command("dbcheck"), Summary("Checks if an entry is in the database.")]
        public async Task dbcheckAsync(IUser user = null)
        {
            var DiscordUser = Context.Message.Author;
            if (IsAdmin(DiscordUser))
            {
                var ChosenUser = user ?? Context.Message.Author;
                string LastFMName = DBase.GetNameForID(GlobalVars.DBFileName, ChosenUser.Id.ToString());
                if (!LastFMName.Equals("NULL"))
                {
                    await ReplyAsync("The user's Last.FM name is: '" + LastFMName + "'");
                }
                else
                {
                    await ReplyAsync("The user's Last.FM Name has not been set.");
                }
            }
        }

        [Command("clearcache"), Summary("Clears the cooldown cache and resets the cooldown for all users.")]
        public async Task clearcacheAsync()
        {
            var DiscordUser = Context.Message.Author;
            if (IsAdmin(DiscordUser))
            {
                User.Users.Clear();
                await ReplyAsync("Cooldown cache reset for all users.");
            }
        }

        [Command("fmadmin")]
        public async Task HelpAsync()
        {
            var DiscordUser = Context.Message.Author;
            if (IsAdmin(DiscordUser))
            {
                // first, let's load our configuration file
                Console.WriteLine("[FMBot] Loading Configuration");
                var json = "";
                using (var fs = File.OpenRead(GlobalVars.ConfigFileName))
                using (var sr = new StreamReader(fs, new UTF8Encoding(false)))
                    json = await sr.ReadToEndAsync();

                // next, let's load the values from that file
                // to our client's configuration
                var cfgjson = JsonConvert.DeserializeObject<ConfigJson>(json);
                var SelfName = Context.Client.CurrentUser;
                string prefix = cfgjson.CommandPrefix;

                EmbedAuthorBuilder eab = new EmbedAuthorBuilder();
                eab.IconUrl = SelfName.GetAvatarUrl();
                eab.Name = SelfName.Username;

                var builder = new EmbedBuilder();
                builder.WithAuthor(eab);

                foreach (var module in _service.Modules)
                {
                    if (module.Name.Equals("FMCommands"))
                    {
                        continue;
                    }

                    string description = null;
                    foreach (var cmd in module.Commands)
                    {
                        var result = await cmd.CheckPreconditionsAsync(Context);
                        if (result.IsSuccess)
                            description += $"{prefix}{cmd.Aliases.First()}\n";
                    }

                    if (!string.IsNullOrWhiteSpace(description))
                    {
                        builder.AddField(x =>
                        {
                            x.Name = "Admin Commands";
                            x.Value = description;
                            x.IsInline = false;
                        });
                    }

                    builder.WithThumbnailUrl(SelfName.GetAvatarUrl());
                }

                await ReplyAsync("", false, builder.Build());
            }
        }
    }

    public class User
    {
        public ulong ID { get; set; }
        public DateTime LastRequest { get; set; }
        public static List<User> Users = new List<User>(); // list of all users

        public static bool IncomingRequest(ulong DiscordID, double cooltime)//requesting user == username of the person messaging your bot
        {
            User TempUser = Users.FirstOrDefault(User => User.ID.Equals(DiscordID));
            if (TempUser != null)// check to see if you have handled a request in the past from this user.
            {
                if ((DateTime.Now - TempUser.LastRequest).TotalSeconds >= cooltime) // checks if more than 30 seconds have passed between the last requests send by the user
                {
                    Users.Find(User => User.ID.Equals(DiscordID)).LastRequest = DateTime.Now; // update their last request time to now.
                    return true;
                }
                else // if less than 30 seconds has passed return false.
                {
                    return false;
                }
            }
            else // if no user is found, create a new user, and add it to the list
            {
                User NewUser = new User();
                NewUser.ID = DiscordID;
                NewUser.LastRequest = DateTime.Now;
                Users.Add(NewUser);
                return true;
            }
        }
    }

    public class DBase
    {
        public static void WriteEntry(string filename, string id, string name)
        {
            File.AppendAllText(filename, id + Environment.NewLine + name + Environment.NewLine);
        }

        public static string GetNameForID(string filename, string id)
        {
            string line;

            using (StreamReader file = new StreamReader(filename))
            {
                while ((line = file.ReadLine()) != null)
                {
                    if (line.Contains(id))
                    {
                        string nextline = file.ReadLine();
                        file.Close();
                        return nextline;
                    }
                    else
                    {
                        continue;
                    }
                }
            }

            return "NULL";
        }
    }

    // this structure will hold data from config.json
    public struct ConfigJson
    {
        [JsonProperty("token")]
        public string Token { get; private set; }

        [JsonProperty("fmkey")]
        public string FMKey { get; private set; }

        [JsonProperty("fmsecret")]
        public string FMSecret { get; private set; }

        [JsonProperty("prefix")]
        public string CommandPrefix { get; private set; }

        [JsonProperty("cooldown")]
        public string Cooldown { get; private set; }

        [JsonProperty("listnum")]
        public string Listnum { get; private set; }
    }

    public class GlobalVars
    {
        public static string DBFileName = "database.txt";
        public static string DBTempName = "database.tmp";
        public static string ConfigFileName = "config.json";
    }
}
