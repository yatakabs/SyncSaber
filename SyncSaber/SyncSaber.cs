using SongCore;
using SyncSaber.Configuration;
using SyncSaber.Extentions;
using SyncSaber.Interfaces;
using SyncSaber.NetWorks;
using SyncSaber.ScoreSabers;
using SyncSaber.SimpleJSON;
using SyncSaber.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;
using Zenject;

namespace SyncSaber
{
    public class SyncSaber : MonoBehaviour, ISyncSaber
    {
        private static SemaphoreSlim _semaphoreSlim;
        private System.Timers.Timer SyncTimer { get; set; }

        private static readonly string _historyPath = Path.Combine(Environment.CurrentDirectory, "UserData", "SyncSaberHistory.txt");
        private static readonly string _favoriteMappersPath = Path.Combine(Environment.CurrentDirectory, "UserData", "FavoriteMappers.ini");
        private static readonly string _customLevelsPath = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels");
        private int _beastSaberFeedIndex = 0;

        private Stack<string> AuthorDownloadQueue { get; } = new Stack<string>();
        public static HashSet<string> SongDownloadHistory => Plugin.SongDownloadHistory;
        private readonly Playlist _syncSaberSongs = new Playlist("SyncSaberPlaylist", "SyncSaber Playlist", "brian91292", "1");
        private readonly Playlist _curatorRecommendedSongs = new Playlist("SyncSaberCuratorRecommendedPlaylist", "BeastSaber Curator Recommended", "brian91292", "1");
        private readonly Playlist _followingsSongs = new Playlist("SyncSaberFollowingsPlaylist", "BeastSaber Followings", "brian91292", "1");
        private readonly Playlist _bookmarksSongs = new Playlist("SyncSaberBookmarksPlaylist", "BeastSaber Bookmarks", "brian91292", "1");

        public event Action<string> NotificationTextChange;
        private bool _initialized = false;
        private volatile bool _didDownloadAnySong = false;
        private readonly Dictionary<string, string> _beastSaberFeeds = new Dictionary<string, string>();
        [Inject]
        private readonly DiContainer _diContainer;

        #region UinityMethod
        private void Awake()
        {
            Logger.Info("Start Awake.");
            DontDestroyOnLoad(this);
            if (_semaphoreSlim == null) {
                _semaphoreSlim = new SemaphoreSlim(1, 1);
            }
            if (this.SyncTimer == null) {
#if DEBUG
                SyncTimer = new System.Timers.Timer(new TimeSpan(0, 0, 30).TotalMilliseconds);
#else
                this.SyncTimer = new System.Timers.Timer(new TimeSpan(0, 30, 0).TotalMilliseconds);
#endif
            }
            this._beastSaberFeeds.Add("followings", $"https://bsaber.com/members/%BeastSaberUserName%/wall/followings");
            this._beastSaberFeeds.Add("bookmarks", $"https://bsaber.com/members/%BeastSaberUserName%/bookmarks");
            this._beastSaberFeeds.Add("curator recommended", $"https://bsaber.com/members/curatorrecommended/bookmarks");

            if (File.Exists(_historyPath + ".bak")) {
                // Something went wrong when the history file was being written previously, restore it from backup
                if (File.Exists(_historyPath)) File.Delete(_historyPath);
                File.Move(_historyPath + ".bak", _historyPath);
            }
            if (File.Exists(_historyPath)) {
                SongDownloadHistory.Clear();
                SongDownloadHistory.AddRange(File.ReadAllLines(_historyPath));
            }
            if (!Directory.Exists(_customLevelsPath)) {
                Directory.CreateDirectory(_customLevelsPath);
            }

            if (!File.Exists(_favoriteMappersPath)) {
#if DEBUG
                File.WriteAllLines(_favoriteMappersPath, new string[] { "denpadokei", "ejiejidayo", "fefy" });
#else
                File.WriteAllLines(_favoriteMappersPath, new string[] { "" });
#endif
            }
            this.SyncTimer.Elapsed -= this.Timer_Elapsed;
            this.SyncTimer.Elapsed += this.Timer_Elapsed;
            Logger.Info("Finish Awake.");
        }
        #endregion
        public void Initialize()
        {
            Logger.Debug("initialize call.");
            this.FinishInitialization();
        }

        private async void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (sender is System.Timers.Timer timer) {
                timer.Stop();
                await this.Sync();
                timer.Start();
            }
        }

        private void FinishInitialization()
        {
            this.DisplayNotification("SyncSaber Initialized!");
            this._initialized = true;
        }

        private void DisplayNotification(string text)
        {
            if (Plugin.instance?.IsInGame == true && !string.IsNullOrEmpty(text)) {
                return;
            }
            Logger.Info(text);
            HMMainThreadDispatcher.instance?.Enqueue(() =>
            {
                NotificationTextChange?.Invoke("SyncSaber - " + text);
            });
        }

        public async Task Sync()
        {
            var tasks = new List<Task>();
            foreach (string mapper in File.ReadAllLines(_favoriteMappersPath)) {
                Logger.Info($"Mapper: {mapper}");
                this.AuthorDownloadQueue.Push(mapper);
            }
            try {
                await _semaphoreSlim.WaitAsync();
                this._didDownloadAnySong = false;
                while (!this._initialized || !Loader.AreSongsLoaded) {
                    await Task.Delay(200);
                }

                while (this.AuthorDownloadQueue.TryPop(out var author)) {
                    tasks.Add(this.DownloadAllSongsByAuthor(author));
                }
                while (this._beastSaberFeedIndex < this._beastSaberFeeds.Count) {
                    if (this._beastSaberFeedIndex == 0 && (!PluginConfig.Instance.SyncFollowingsFeed || string.IsNullOrWhiteSpace(PluginConfig.Instance.BeastSaberUsername))) {
                        this._beastSaberFeedIndex++;
                        continue;
                    }
                    else if (this._beastSaberFeedIndex == 1 && (!PluginConfig.Instance.SyncBookmarksFeed || string.IsNullOrWhiteSpace(PluginConfig.Instance.BeastSaberUsername))) {
                        this._beastSaberFeedIndex++;
                        continue;
                    }
                    else if (this._beastSaberFeedIndex == 2 && !PluginConfig.Instance.SyncCuratorRecommendedFeed) {
                        this._beastSaberFeedIndex++;
                        continue;
                    }
                    tasks.Add(this.DownloadBeastSaberFeeds(this._beastSaberFeedIndex));
                    this._beastSaberFeedIndex++;
                }
                if (PluginConfig.Instance.SyncPPSongs) {
                    tasks.Add(this.DownloadPPSongs());
                }
                while (!Loader.AreSongsLoaded || Loader.AreSongsLoading) {
                    await Task.Delay(200);
                }
                await Task.WhenAll(tasks);
                this.StartCoroutine(this.BeforeDownloadSongs());
                if (Plugin.instance.IsPlaylistDownlaoderInstalled) {
                    this.StartCoroutine(this.CheckPlaylist());
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
                _semaphoreSlim.Release();
            }
        }

        public void StartTimer()
        {
            this.SyncTimer.Start();
        }

        private int GetMaxBeastSaberPages(int feedToDownload)
        {
            switch (this._beastSaberFeeds.ElementAt(feedToDownload).Key) {
                case "followings":
                    return PluginConfig.Instance.MaxFollowingsPages;
                case "bookmarks":
                    return PluginConfig.Instance.MaxBookmarksPages;
                case "curator recommended":
                    return PluginConfig.Instance.MaxCuratorRecommendedPages;
            }
            return 0;
        }

        private IEnumerator BeforeDownloadSongs()
        {
            yield return new WaitWhile(() => !Loader.AreSongsLoaded || Loader.AreSongsLoading || Plugin.instance.IsInGame);
            if (this._didDownloadAnySong) {
                yield return this.StartCoroutine(SongListUtils.RefreshSongs());
            }
            yield return new WaitWhile(() => !Loader.AreSongsLoaded || Loader.AreSongsLoading || Plugin.instance.IsInGame);
            this.DisplayNotification("Finished checking for new songs!");
            yield return null;
            this._didDownloadAnySong = false;
        }

        private Playlist GetPlaylistForFeed(int feedToDownload)
        {
            switch (this._beastSaberFeeds.ElementAt(feedToDownload).Key) {
                case "followings":
                    return this._followingsSongs;
                case "bookmarks":
                    return this._bookmarksSongs;
                case "curator recommended":
                    return this._curatorRecommendedSongs;
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="playlist"></param>
        /// <param name="hash"></param>
        /// <param name="songName"></param>
        private void UpdatePlaylist(Playlist playlist, string hash, string songName)
        {
            // Update our playlist with the new song if it doesn't exist, or replace the old song id/name with the updated info if it does
            bool playlistSongFound = playlist.Songs.Any(x => x.hash.ToUpper() == hash.ToUpper());

            if (!playlistSongFound) {
                playlist.Add(hash, songName);
                Logger.Info($"Success adding new song \"{songName}\" with BeatSaver index {hash} to playlist {playlist.Title}!");
            }
        }



        private void RemoveOldVersions(string hash)
        {
            if (!PluginConfig.Instance.DeleteOldVersions) return;

            var levelMap = Loader.CustomLevels.FirstOrDefault(x => x.Value.levelID.ToUpper() == $"CUSTOM_LEVEL_{hash.ToUpper()}");

            if (levelMap.Key != null) {
                try {
                    Loader.Instance.RemoveSongWithLevelID(levelMap.Value.levelID);
                    var directoryName = levelMap.Key;
                    Directory.Delete(directoryName, true);
                }
                catch (Exception e) {
                    Logger.Info($"Exception when trying to remove old versions {e}");
                }
            }
        }

        private async Task DownloadAllSongsByAuthor(string author)
        {
            if (string.IsNullOrEmpty(author)) {
                return;
            }

            Logger.Info($"Downloading all songs from {author}");
            JSONNode result = null;
            var stopWatch = new Stopwatch();
            var pageCount = 0;
            var lastPage = 0;

            try {
                stopWatch.Start();
                do {
                    while (Plugin.instance?.IsInGame == true) {
                        await Task.Delay(200);
                    }
                    this.DisplayNotification($"Checking {author}'s maps. ({pageCount} page)");
                    var res = await WebClient.GetAsync($"https://beatsaver.com/api/search/advanced/{pageCount}?q=uploader.username:{author}", new CancellationTokenSource().Token).ConfigureAwait(false);
                    if (!res.IsSuccessStatusCode) {
                        Logger.Info($"{res.StatusCode}");
                        return;
                    }
                    result = JSON.Parse(res.ContentToString());
                    var docs = result["docs"].AsArray;
                    lastPage = result["lastPage"].AsInt;

                    foreach (var keyvalue in docs) {
                        var song = keyvalue.Value as JSONObject;
                        var hash = song["hash"].Value;
                        var key = song["key"].Value;
                        var songName = song["name"].Value;
                        var downloadSucess = false;
                        if (SongDownloadHistory.Contains(hash.ToLower()) || Loader.GetLevelByHash(hash.ToUpper()) != null) {
                            SongDownloadHistory.Add(hash.ToLower());
                            // Update our playlist with the latest song info
                            this.UpdatePlaylist(this._syncSaberSongs, hash, songName);
                            continue;
                        }

                        if (PluginConfig.Instance.AutoDownloadSongs) {
                            try {
                                var metaData = song["metadata"].AsObject;
                                Logger.Debug($"{hash} : {songName}");
                                string currentSongDirectory = Path.Combine(_customLevelsPath, Regex.Replace($"{key} ({songName} - {metaData["songAuthorName"].Value})", "[\\\\:*/?\"<>|]", "_"));
                                Logger.Debug($"{songName} : {currentSongDirectory}");
                                this.DisplayNotification($"Downloading {songName}");
                                var url = $"https://beatsaver.com{song["downloadURL"].Value}";
                                Logger.Info(url);
                                this.DisplayNotification($"Download - {songName}");
                                while (Plugin.instance?.IsInGame == true) {
                                    await Task.Delay(200);
                                }
                                var buff = await WebClient.DownloadSong(url, new CancellationTokenSource().Token);
                                if (buff == null) {
                                    Logger.Notice($"Failed to download song : {songName}");
                                    continue;
                                }
                                while (Plugin.instance?.IsInGame == true) {
                                    await Task.Delay(200);
                                }
                                using (var st = new MemoryStream(buff)) {
                                    Utility.ExtractZip(st, currentSongDirectory);
                                }
                                downloadSucess = true;
                                this._didDownloadAnySong = true;
                            }
                            catch (Exception e) {
                                Logger.Error(e);
                            }
                        }
                        Logger.Info("Finish download");
                        // Keep a history of all the songs we download- count it as downloaded even if the user already had it downloaded previously so if they delete it it doesn't get redownloaded
                        if (downloadSucess && !SongDownloadHistory.Contains(hash.ToLower())) {
                            SongDownloadHistory.Add(hash.ToLower());

                            // Update our playlist with the latest song info
                            this.UpdatePlaylist(this._syncSaberSongs, hash, songName);

                            // Delete any duplicate songs that we've downloaded
                            this.RemoveOldVersions(hash);
                        }
                    }
                    pageCount++;
                    Logger.Info($"pageCount : {pageCount} lastPage : {lastPage} [{pageCount <= lastPage}]");
                } while (pageCount <= lastPage);
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
                stopWatch.Stop();
            }

            // Write our download history to file
            Utility.WriteStringListSafe(_historyPath, SongDownloadHistory.ToList());

            // Write to the SyncSaber playlist
            this._syncSaberSongs.WritePlaylist();

            Logger.Info($"Downloaded downloadCount songs from mapper \"{author}\" in {stopWatch.Elapsed.Seconds} seconds.");
        }

        private async Task DownloadBeastSaberFeeds(int feedToDownload)
        {
            var startTime = DateTime.Now;
            int downloadCount = 0;
            int totalSongs = 0;
            int pageIndex = 0;

            while (true) {
                int totalSongsForPage = 0;
                int downloadCountForPage = 0;

                this.DisplayNotification($"Checking page {pageIndex} of {this._beastSaberFeeds.ElementAt(feedToDownload).Key} feed from BeastSaber!");

                try {
                    while (Plugin.instance?.IsInGame == true) {
                        await Task.Delay(200);
                    }
                    var res = await WebClient.GetAsync($"{this._beastSaberFeeds.ElementAt(feedToDownload).Value.Replace("%BeastSaberUserName%", PluginConfig.Instance.BeastSaberUsername)}/feed/?acpage={pageIndex}", new CancellationTokenSource().Token);
                    if (!res.IsSuccessStatusCode) {
                        return;
                    }
                    string beastSaberFeed = res.ContentToString();
                    XmlDocument doc = new XmlDocument();
                    try {
                        doc.LoadXml(beastSaberFeed);
                    }
                    catch (Exception ex) {
                        Logger.Error(ex);
                        return;
                    }

                    XmlNodeList nodes = doc.DocumentElement.SelectNodes("/rss/channel/item");
                    if (nodes?.Count != 0) {
                        foreach (XmlNode node in nodes) {
                            while (Plugin.instance?.IsInGame != false || Loader.AreSongsLoading) {
                                await Task.Delay(200);
                            }

                            if (node["DownloadURL"] == null || node["SongTitle"] == null) {
                                Logger.Info("Essential node was missing! Skipping!");
                                continue;
                            }

                            string songName = node["SongTitle"].InnerText;
                            string downloadUrl = node["DownloadURL"].InnerText;

                            if (downloadUrl.Contains("dl.php")) {
                                Logger.Info("Skipping BeastSaber download with old url format!");
                                totalSongs++;
                                totalSongsForPage++;
                                continue;
                            }

                            string key = node["SongKey"].InnerText;
                            string hash = node["Hash"].InnerText;
                            string currentSongDirectory = Path.Combine(_customLevelsPath, Regex.Replace($"{key} ({songName} - {node["LevelAuthorName"].InnerText})", "[\\\\:*/?\"<>|]", "_"));
                            bool downloadSucess = false;
                            if (SongDownloadHistory.Contains(hash.ToLower()) || Loader.GetLevelByHash(hash.ToUpper()) != null) {
                                SongDownloadHistory.Add(hash.ToLower());
                                // Update our playlist with the latest song info
                                this.UpdatePlaylist(this._syncSaberSongs, hash, songName);
                                this.UpdatePlaylist(this.GetPlaylistForFeed(feedToDownload), hash, songName);
                                continue;
                            }
                            if (PluginConfig.Instance.AutoDownloadSongs) {
                                this.DisplayNotification($"Downloading {songName}");

                                while (Plugin.instance?.IsInGame == true) {
                                    await Task.Delay(200);
                                }
                                var buff = await WebClient.DownloadSong(downloadUrl, new CancellationTokenSource().Token);
                                if (buff == null) {
                                    Logger.Notice($"Failed to download song : {songName}");
                                    continue;
                                }
                                while (Plugin.instance?.IsInGame == true) {
                                    await Task.Delay(200);
                                }
                                using (var st = new MemoryStream(buff)) {
                                    Utility.ExtractZip(st, currentSongDirectory);
                                }
                                downloadCount++;
                                downloadCountForPage++;
                                this._didDownloadAnySong = true;
                                downloadSucess = true;
                            }

                            // Keep a history of all the songs we download- count it as downloaded even if the user already had it downloaded previously so if they delete it it doesn't get redownloaded
                            if (downloadSucess) {
                                SongDownloadHistory.Add(hash.ToLower());

                                // Update our playlist with the latest song info
                                this.UpdatePlaylist(this._syncSaberSongs, hash, songName);
                                this.UpdatePlaylist(this.GetPlaylistForFeed(feedToDownload), hash, songName);

                                // Delete any duplicate songs that we've downloaded
                                this.RemoveOldVersions(hash);
                            }

                            totalSongs++;
                            totalSongsForPage++;
                        }
                    }

                    if (totalSongsForPage == 0) {
                        Logger.Info("Reached end of feed!");
                        break;
                    }
                }
                catch (Exception e) {
                    Logger.Error(e);
                }

                //Logger.Info($"Reached end of page! Found {totalSongsForPage.ToString()} songs total, downloaded {downloadCountForPage.ToString()}!");
                pageIndex++;

                if (pageIndex > this.GetMaxBeastSaberPages(feedToDownload) + 1 && this.GetMaxBeastSaberPages(feedToDownload) != 0)
                    break;
            }
            // Write our download history to file
            Utility.WriteStringListSafe(_historyPath, SongDownloadHistory.ToList());
            // Write to the SynCSaber playlist
            this._syncSaberSongs.WritePlaylist();
            this.GetPlaylistForFeed(feedToDownload).WritePlaylist();
            Logger.Info($"Downloaded {downloadCount} songs from BeastSaber {this._beastSaberFeeds.ElementAt(feedToDownload).Key} feed in {((DateTime.Now - startTime).Seconds)} seconds. Checked {(pageIndex + 1)} page{(pageIndex > 0 ? "s" : "")}, skipped {(totalSongs - downloadCount)} songs.");
        }

        private async Task DownloadPPSongs()
        {
            this.DisplayNotification("Download PPSongs.");

            var songlist = await ScoreSaberManager.Ranked(PluginConfig.Instance.MaxPPSongsCount, PluginConfig.Instance.RankSort); //SongDataCore.Plugin.Songs.Data.Songs?.Where(x => x.Value.diffs.Max(y => y.pp) > 0d).OrderByDescending(x => x.Value.diffs.Max(y => y.pp))?.ToList();
            if (songlist == null) {
                return;
            }
            foreach (var ppMap in songlist.Values) {
                try {
                    while (Plugin.instance?.IsInGame != false) {
                        await Task.Delay(200);
                    }
                    var hash = ppMap["id"].Value;
                    var beatmap = Loader.GetLevelByHash(hash);
                    if (SongDownloadHistory.Contains(hash.ToLower()) || beatmap != null) {
                        this.UpdatePlaylist(this._syncSaberSongs, hash, beatmap.songName);
                        SongDownloadHistory.Add(hash.ToLower());
                        continue;
                    }
                    var songInfo = await WebClient.GetAsync($"https://beatsaver.com/api/maps/by-hash/{hash}", new CancellationTokenSource().Token);
                    var jsonObject = JSON.Parse(songInfo?.ContentToString());
                    if (jsonObject == null) {
                        Logger.Info($"missing pp song : https://beatsaver.com/api/maps/by-hash/{hash}");
                        continue;
                    }
                    var key = jsonObject["key"].Value.ToLower();
                    var chara = jsonObject["characteristics"].AsObject;
                    var author = ppMap["levelAuthorName"].Value;
                    this.DisplayNotification($"Downloading {jsonObject["name"].Value}");
                    var buff = await WebClient.DownloadSong($"https://beatsaver.com/api/download/key/{key}", new CancellationTokenSource().Token);
                    if (buff == null) {
                        Logger.Notice($"Failed to download song : {jsonObject["name"].Value}");
                        continue;
                    }
                    while (Plugin.instance?.IsInGame == true) {
                        await Task.Delay(200);
                    }
                    var songDirectory = Path.Combine(_customLevelsPath, Regex.Replace($"{key} ({jsonObject["name"].Value} - {author})", "[\\\\:*/?\"<>|]", "_")); ;
                    using (var st = new MemoryStream(buff)) {
                        Utility.ExtractZip(st, songDirectory);
                    }
                    this._didDownloadAnySong = true;
                    SongDownloadHistory.Add(hash.ToLower());
                    this.UpdatePlaylist(this._syncSaberSongs, hash, jsonObject["name"].Value);
                }
                catch (Exception e) {
                    Logger.Error($"{e}\r\n{ppMap["name"]} : {ppMap["id"]}");
                    Logger.Debug($"{ppMap}");
                }
            }
            try {
                // Write our download history to file
                Utility.WriteStringListSafe(_historyPath, SongDownloadHistory.ToList());
                // Write to the SyncSaber playlist
                this._syncSaberSongs.WritePlaylist();
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
        public void SetEvent()
        {
            try {
                Utility.GetPlaylistDownloader(this._diContainer).ChangeNotificationText -= this.NotificationTextChange;
                Utility.GetPlaylistDownloader(this._diContainer).ChangeNotificationText += this.NotificationTextChange;
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        private IEnumerator CheckPlaylist()
        {
            yield return new WaitWhile(() => !Loader.AreSongsLoaded || Loader.AreSongsLoading);
            try {
                _ = Utility.GetPlaylistDownloader(this._diContainer).CheckPlaylistsSong();
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        public void Dispose()
        {
            Logger.Debug("Dispose call!");
            this.SyncTimer.Elapsed -= this.Timer_Elapsed;
            ((IDisposable)this.SyncTimer).Dispose();
            this.SyncTimer = null;
            _semaphoreSlim.Dispose();
            _semaphoreSlim = null;
        }
    }
}
