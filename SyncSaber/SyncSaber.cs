using HMUI;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
//using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using SyncSaber.SimpleJSON;
using SongCore;
using SyncSaber.Extentions;
using SyncSaber.Configuration;
using SyncSaber.NetWorks;
using System.Threading;
using BS_Utils.Utilities;

namespace SyncSaber
{
    class SyncSaber : MonoBehaviour
    {
        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        private SynchronizationContext context;
        internal readonly System.Timers.Timer _timer = new System.Timers.Timer(new TimeSpan(0, 30, 0).TotalMilliseconds);

        private static readonly string _historyPath = Path.Combine(Environment.CurrentDirectory, "UserData", "SyncSaberHistory.txt");
        private static readonly string _favoriteMappersPath = Path.Combine(Environment.CurrentDirectory, "UserData", "FavoriteMappers.ini");
        private static readonly string _customLevelsPath = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels");
        private int _beastSaberFeedIndex = 0;

        private Stack<string> AuthorDownloadQueue { get; } = new Stack<string>();
        private List<string> SongDownloadHistory { get; } = new List<string>();
        private Playlist _syncSaberSongs = new Playlist("SyncSaberPlaylist", "SyncSaber Playlist", "brian91292", "1");
        private Playlist _curatorRecommendedSongs = new Playlist("SyncSaberCuratorRecommendedPlaylist", "BeastSaber Curator Recommended", "brian91292", "1");
        private Playlist _followingsSongs = new Playlist("SyncSaberFollowingsPlaylist", "BeastSaber Followings", "brian91292", "1");
        private Playlist _bookmarksSongs = new Playlist("SyncSaberBookmarksPlaylist", "BeastSaber Bookmarks", "brian91292", "1");

        private TMP_Text _notificationText;
        private DateTime _uiResetTime;

        private bool _initialized = false;
        private bool _didDownloadAnySong = false;

        Dictionary<string, string> _beastSaberFeeds = new Dictionary<string, string>();

        public static SyncSaber Instance = null;

        public static void OnLoad()
        {
            Logger.Info("Start OnLoad.");
            if (Instance) {
                Logger.Info($"Instance is null.");
                return;
            }
            new GameObject("SyncSaber").AddComponent<SyncSaber>();
        }

        #region UinityMethod
        private void Awake()
        {
            Logger.Info("Start Awake.");
            Instance = this;
            context = SynchronizationContext.Current;
            DontDestroyOnLoad(gameObject);

            _beastSaberFeeds.Add("followings", $"https://bsaber.com/members/%BeastSaberUserName%/wall/followings");
            _beastSaberFeeds.Add("bookmarks", $"https://bsaber.com/members/%BeastSaberUserName%/bookmarks");
            _beastSaberFeeds.Add("curator recommended", $"https://bsaber.com/members/curatorrecommended/bookmarks");

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

            this._timer.Elapsed -= this.Timer_Elapsed;
            this._timer.Elapsed += this.Timer_Elapsed;

            StartCoroutine(FinishInitialization());

            Logger.Info("Finish Awake.");
        }

        private void FixedUpdate()
        {
            if (!string.IsNullOrEmpty(_notificationText?.text) && _uiResetTime <= DateTime.Now)
                _notificationText.text = "";
        }
        #endregion

        private async void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            if (sender is System.Timers.Timer timer) {
                timer.Stop();
                await this.Sync();
                timer.Start();
            }
        }

        private IEnumerator FinishInitialization()
        {
            yield return new WaitUntil(() => Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Any(t => t.name == "Teko-Medium SDF No Glow"));

            _notificationText = Utilities.CreateNotificationText(String.Empty);
            DisplayNotification("SyncSaber Initialized!");

            _initialized = true;
        }

        private void DisplayNotification(string text)
        {
            if (Plugin.instance?.IsInGame == true && !string.IsNullOrEmpty(text)) {
                return;
            }
            Logger.Info(text);
            HMMainThreadDispatcher.instance?.Enqueue(() =>
            {
                _uiResetTime = DateTime.Now.AddSeconds(5);
                if (_notificationText)
                    _notificationText.text = "SyncSaber - " + text;
            });
        }

        public async Task Sync()
        {
            var tasks = new List<Task>();
            foreach (string mapper in File.ReadAllLines(_favoriteMappersPath)) {
                Logger.Info($"Mapper: {mapper}");
                AuthorDownloadQueue.Push(mapper);
            }
            try {
                await this.semaphoreSlim.WaitAsync();
                while (!_initialized  && !Loader.AreSongsLoaded) {
                    await Task.Delay(200);
                }

                while (AuthorDownloadQueue.TryPop(out var author)) {
                    tasks.Add(DownloadAllSongsByAuthor(author));
                }
                while (_beastSaberFeedIndex < _beastSaberFeeds.Count) {
                    if (_beastSaberFeedIndex == 0 && (!PluginConfig.Instance.SyncFollowingsFeed || string.IsNullOrWhiteSpace(PluginConfig.Instance.BeastSaberUsername))) {
                        _beastSaberFeedIndex++;
                        continue;
                    }
                    else if (_beastSaberFeedIndex == 1 && (!PluginConfig.Instance.SyncBookmarksFeed || string.IsNullOrWhiteSpace(PluginConfig.Instance.BeastSaberUsername))) {
                        _beastSaberFeedIndex++;
                        continue;
                    }
                    else if (_beastSaberFeedIndex == 2 && !PluginConfig.Instance.SyncCuratorRecommendedFeed) {
                        _beastSaberFeedIndex++;
                        continue;
                    }
                    tasks.Add(DownloadBeastSaberFeeds(_beastSaberFeedIndex));
                    _beastSaberFeedIndex++;
                }
                while (Loader.AreSongsLoading) {
                    await Task.Delay(200);
                }
                await Task.WhenAll(tasks);
                if (_didDownloadAnySong) {
                    StartCoroutine(SongListUtils.RefreshSongs(false));
                    _didDownloadAnySong = false;
                }
                DisplayNotification("Finished checking for new songs!");
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
                this.semaphoreSlim.Release();
            }
        }

        private int GetMaxBeastSaberPages(int feedToDownload)
        {
            switch (_beastSaberFeeds.ElementAt(feedToDownload).Key) {
                case "followings":
                    return PluginConfig.Instance.MaxFollowingsPages;
                case "bookmarks":
                    return PluginConfig.Instance.MaxBookmarksPages;
                case "curator recommended":
                    return PluginConfig.Instance.MaxCuratorRecommendedPages;
            }
            return 0;
        }

        private Playlist GetPlaylistForFeed(int feedToDownload)
        {
            switch (_beastSaberFeeds.ElementAt(feedToDownload).Key) {
                case "followings":
                    return _followingsSongs;
                case "bookmarks":
                    return _bookmarksSongs;
                case "curator recommended":
                    return _curatorRecommendedSongs;
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
            Logger.Info($"Downloading all songs from {author}");
            JSONNode result = null;
            var startTime = DateTime.Now;
            TimeSpan idleTime = new TimeSpan();
            var pageCount = 0;
            var lastPage = 0;

            try {
                do {
                    while (Plugin.instance?.IsInGame == true) {
                        await Task.Delay(200);
                    }
                    DisplayNotification($"Checking {author}'s maps. ({pageCount} page)");
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
                        var downloadFailed = false;
                        if (PluginConfig.Instance.AutoDownloadSongs && Loader.GetLevelByHash(hash.ToUpper()) == null) {
                            try {
                                var metaData = song["metadata"].AsObject;
                                Logger.Debug($"{hash} : {songName}");
                                string currentSongDirectory = Path.Combine(_customLevelsPath, Regex.Replace($"{key} ({songName} - {metaData["songAuthorName"].Value})", "[:*/?\"<>|]", ""));
                                Logger.Debug($"{songName} : {currentSongDirectory}");
                                DisplayNotification($"Downloading {songName}");
                                var url = $"https://beatsaver.com{song["downloadURL"].Value}";
                                Logger.Info(url);
                                DisplayNotification($"Download - {songName}");
                                while (Plugin.instance?.IsInGame == true) {
                                    await Task.Delay(200);
                                }
                                var buff = await WebClient.DownloadSong(url, new CancellationTokenSource().Token);
                                if (buff == null) {
                                    continue;
                                }
                                while (Plugin.instance?.IsInGame == true) {
                                    await Task.Delay(200);
                                }
                                using (var st = new MemoryStream(buff)) {
                                    Utilities.ExtractZip(st, currentSongDirectory);
                                }
                                _didDownloadAnySong = true;
                            }
                            catch (Exception e) {
                                Logger.Error(e);
                            }
                        }
                        Logger.Info("Finish download");
                        // Keep a history of all the songs we download- count it as downloaded even if the user already had it downloaded previously so if they delete it it doesn't get redownloaded
                        if (!downloadFailed && !SongDownloadHistory.Contains(key)) {
                            SongDownloadHistory.Add(key);

                            // Update our playlist with the latest song info
                            UpdatePlaylist(_syncSaberSongs, hash, songName);

                            // Delete any duplicate songs that we've downloaded
                            RemoveOldVersions(hash);
                        }
                    }
                    pageCount++;
                    Logger.Info($"pageCount : {pageCount} lastPage : {lastPage} [{pageCount <= lastPage}]");
                } while (pageCount <= lastPage);
            }
            catch (Exception e) {
                Logger.Error(e);
            }

            // Write our download history to file
            Utilities.WriteStringListSafe(_historyPath, SongDownloadHistory.Distinct().ToList());

            // Write to the SyncSaber playlist
            _syncSaberSongs.WritePlaylist();

            Logger.Info($"Downloaded downloadCount songs from mapper \"{author}\" in {((DateTime.Now - startTime - idleTime).Seconds)} seconds. Skipped (totalSongs - downloadCount) songs.");
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

                DisplayNotification($"Checking page {pageIndex} of {_beastSaberFeeds.ElementAt(feedToDownload).Key} feed from BeastSaber!");

                try {
                    while (Plugin.instance?.IsInGame == true) {
                        await Task.Delay(200);
                    }
                    var res = await WebClient.GetAsync($"{_beastSaberFeeds.ElementAt(feedToDownload).Value.Replace("%BeastSaberUserName%", PluginConfig.Instance.BeastSaberUsername)}/feed/?acpage={pageIndex}", new CancellationTokenSource().Token);
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
                            string currentSongDirectory = Path.Combine(_customLevelsPath, Regex.Replace($"{key} ({songName} - {node["LevelAuthorName"].InnerText})", "[:*/?\"<>|]", ""));
                            bool downloadFailed = false;
                            if (PluginConfig.Instance.AutoDownloadSongs && !SongDownloadHistory.Contains(key) && Loader.GetLevelByHash(hash.ToUpper()) == null) {
                                DisplayNotification($"Downloading {songName}");

                                string localPath = Path.Combine(Path.GetTempPath(), $"{hash}.zip");
                                while (Plugin.instance?.IsInGame == true) {
                                    await Task.Delay(200);
                                }
                                var buff = await WebClient.DownloadSong(downloadUrl, new CancellationTokenSource().Token);
                                if (buff == null) {
                                    continue;
                                }
                                while (Plugin.instance?.IsInGame == true) {
                                    await Task.Delay(200);
                                }
                                using (var st = new MemoryStream(buff)) {
                                    Utilities.ExtractZip(st, currentSongDirectory);
                                }
                                downloadCount++;
                                downloadCountForPage++;
                                _didDownloadAnySong = true;
                            }

                            // Keep a history of all the songs we download- count it as downloaded even if the user already had it downloaded previously so if they delete it it doesn't get redownloaded
                            if (!downloadFailed && !SongDownloadHistory.Contains(key)) {
                                SongDownloadHistory.Add(key);

                                // Update our playlist with the latest song info
                                UpdatePlaylist(_syncSaberSongs, hash, songName);
                                UpdatePlaylist(GetPlaylistForFeed(feedToDownload), hash, songName);

                                // Delete any duplicate songs that we've downloaded
                                RemoveOldVersions(hash);
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

                // Write our download history to file
                Utilities.WriteStringListSafe(_historyPath, SongDownloadHistory.Distinct().ToList());

                // Write to the SynCSaber playlist
                _syncSaberSongs.WritePlaylist();
                GetPlaylistForFeed(feedToDownload).WritePlaylist();

                //Logger.Info($"Reached end of page! Found {totalSongsForPage.ToString()} songs total, downloaded {downloadCountForPage.ToString()}!");
                pageIndex++;

                if (pageIndex > GetMaxBeastSaberPages(feedToDownload) + 1 && GetMaxBeastSaberPages(feedToDownload) != 0)
                    break;
            }
            Logger.Info($"Downloaded {downloadCount} songs from BeastSaber {_beastSaberFeeds.ElementAt(feedToDownload).Key} feed in {((DateTime.Now - startTime).Seconds)} seconds. Checked {(pageIndex + 1)} page{(pageIndex > 0 ? "s" : "")}, skipped {(totalSongs - downloadCount)} songs.");
        }
    }
}
