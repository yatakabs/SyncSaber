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
        private readonly Regex _digitRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);
        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        private SynchronizationContext context;

        private bool _downloaderRunning = false;
        private bool _downloaderComplete = false;
        private bool _isInGame = false;
        private string _historyPath = null;
        private int _beastSaberFeedIndex = 0;

        private Stack<string> AuthorDownloadQueue { get; } = new Stack<string>();
        private ConcurrentStack<KeyValuePair<JSONObject, KeyValuePair<string, CustomPreviewBeatmapLevel>>> _updateQueue = new ConcurrentStack<KeyValuePair<JSONObject, KeyValuePair<string, CustomPreviewBeatmapLevel>>>();
        private List<string> SongDownloadHistory { get; } = new List<string>();
        private Playlist _syncSaberSongs = new Playlist("SyncSaberPlaylist", "SyncSaber Playlist", "brian91292", "1");
        private Playlist _curatorRecommendedSongs = new Playlist("SyncSaberCuratorRecommendedPlaylist", "BeastSaber Curator Recommended", "brian91292", "1");
        private Playlist _followingsSongs = new Playlist("SyncSaberFollowingsPlaylist", "BeastSaber Followings", "brian91292", "1");
        private Playlist _bookmarksSongs = new Playlist("SyncSaberBookmarksPlaylist", "BeastSaber Bookmarks", "brian91292", "1");
        private Dictionary<string, DateTime> _updateCheckTracker = new Dictionary<string, DateTime>();

        private IPreviewBeatmapLevel _lastLevel;
        private SoloFreePlayFlowCoordinator _standardLevelSelectionFlowCoordinator;
        private LevelCollectionViewController _standardLevelListViewController;

        private TMP_Text _notificationText;
        private DateTime _uiResetTime;
        private bool _songBrowserInstalled = false;
        private int _updateCheckIntervalMinutes = 30;
        private bool _initialized = false;
        private bool _didDownloadAnySong = false;

        Dictionary<string, string> _beastSaberFeeds = new Dictionary<string, string>();

        public static SyncSaber Instance = null;

        //private List<IBeatmapLevel> CurrentLevels
        //{
        //    get
        //    {
        //        return ReflectionUtil.GetPrivateField<IBeatmapLevel[]>(_standardLevelListViewController, "_levels").ToList();
        //    }
        //    set
        //    {
        //        ReflectionUtil.SetPrivateField(_standardLevelListViewController, "_levels", value);
        //    }
        //}

        public static void OnLoad()
        {
            Logger.Info("Start OnLoad.");
            if (Instance) {
                Logger.Info($"Instance is null.");
                return;
            }
            new GameObject("SyncSaber").AddComponent<SyncSaber>();
        }

        private void Awake()
        {
            Logger.Info("Start Awake.");
            Instance = this;
            context = SynchronizationContext.Current;
            DontDestroyOnLoad(gameObject);

            if (PluginConfig.Instance.SyncFollowingsFeed && !string.IsNullOrWhiteSpace(PluginConfig.Instance.BeastSaberUsername))
                _beastSaberFeeds.Add("followings", $"https://bsaber.com/members/{PluginConfig.Instance.BeastSaberUsername}/wall/followings");
            if (PluginConfig.Instance.SyncBookmarksFeed && !string.IsNullOrWhiteSpace(PluginConfig.Instance.BeastSaberUsername))
                _beastSaberFeeds.Add("bookmarks", $"https://bsaber.com/members/{PluginConfig.Instance.BeastSaberUsername}/bookmarks");
            if (PluginConfig.Instance.SyncCuratorRecommendedFeed)
                _beastSaberFeeds.Add("curator recommended", $"https://bsaber.com/members/curatorrecommended/bookmarks");

            _songBrowserInstalled = Utilities.IsModInstalled("Song Browser");

            _historyPath = Path.Combine(Environment.CurrentDirectory, "UserData", "SyncSaberHistory.txt");
            if (File.Exists(_historyPath + ".bak")) {
                // Something went wrong when the history file was being written previously, restore it from backup
                if (File.Exists(_historyPath)) File.Delete(_historyPath);
                File.Move(_historyPath + ".bak", _historyPath);
            }
            if (File.Exists(_historyPath)) {
                SongDownloadHistory.Clear();
                SongDownloadHistory.AddRange(File.ReadAllLines(_historyPath));
            }
            if (!Directory.Exists(Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels"))) {
                Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels"));
            }

            string favoriteMappersPath = Path.Combine(Environment.CurrentDirectory, "UserData", "FavoriteMappers.ini");
            if (!File.Exists(favoriteMappersPath)) {
#if DEBUG
                File.WriteAllLines(favoriteMappersPath, new string[] { "denpadokei", "ejiejidayo", "fefy" });
#else
                File.WriteAllLines(favoriteMappersPath, new string[] { "" });
#endif
            }

            foreach (string mapper in File.ReadAllLines(favoriteMappersPath)) {
                Logger.Info($"Mapper: {mapper}");
                AuthorDownloadQueue.Push(mapper);
            }

            StartCoroutine(FinishInitialization());

            Logger.Info("Finish Awake.");
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
            Logger.Info(text);
            context.Post(d =>
            {
                _uiResetTime = DateTime.Now.AddSeconds(5);
                if (_notificationText)
                    _notificationText.text = "SyncSaber- " + text;
            }, null);
        }

        public async Task Sync()
        {
            try {
                await this.semaphoreSlim.WaitAsync();
                if (!_initialized) return;

                if (_updateQueue.Any()) {
                    if (_updateQueue.TryPop(out var songUpdateInfo)) {
                        //Logger.Info($"Updating {songUpdateInfo.Key}");
                        await UpdateSong(songUpdateInfo);
                    }
                }
                while (AuthorDownloadQueue.Any()) {
                    await DownloadAllSongsByAuthor(AuthorDownloadQueue.Pop());
                }
                if (!string.IsNullOrWhiteSpace(PluginConfig.Instance.BeastSaberUsername) && _beastSaberFeedIndex < _beastSaberFeeds.Count) {
                    await DownloadBeastSaberFeeds(_beastSaberFeedIndex);
                    _beastSaberFeedIndex++;
                }
                while (Loader.AreSongsLoading) {
                    await Task.Delay(200);
                }
                if (_didDownloadAnySong) {
                    StartCoroutine(SongListUtils.RefreshSongs(false, false));
                }
                DisplayNotification("Finished checking for new songs!");
                _downloaderComplete = true;
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            finally {
                this.semaphoreSlim.Release();
            }
        }

        private void FixedUpdate()
        {
            if (_uiResetTime <= DateTime.Now && _notificationText.text != String.Empty)
                _notificationText.text = String.Empty;
        }

        internal void DelayedActiveSceneChanged()
        {
            SongListUtils.Initialize();

            _standardLevelSelectionFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().FirstOrDefault();
            if (_standardLevelSelectionFlowCoordinator == null) return;
            var selectionNavigate = _standardLevelSelectionFlowCoordinator.GetPrivateField<LevelSelectionNavigationController>("_levelSelectionNavigationController");
            _standardLevelListViewController = selectionNavigate.GetPrivateField<LevelCollectionViewController>("_levelCollectionViewController");
            if (_standardLevelListViewController == null) return;

            _standardLevelListViewController.didSelectLevelEvent -= standardLevelListViewController_didSelectLevelEvent;
            _standardLevelListViewController.didSelectLevelEvent += standardLevelListViewController_didSelectLevelEvent;
        }

        private void standardLevelListViewController_didSelectLevelEvent(LevelCollectionViewController sender, IPreviewBeatmapLevel level)
        {
            if (PluginConfig.Instance.AutoUpdateSongs && level != _lastLevel && level.levelID.Length > 32) {
                _lastLevel = level;
                HandleDidSelectLevelEvent(level.levelID);
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

        private async Task UpdateSong(KeyValuePair<JSONObject, KeyValuePair<string, CustomPreviewBeatmapLevel>> songUpdateInfo)
        {
            _downloaderRunning = true;
            var song = songUpdateInfo.Key;
            var oldLevel = songUpdateInfo.Value;
            string songkey = song["key"].Value;
            string songHash = (song["hash"].Value).ToUpper();

            if (PluginConfig.Instance.DeleteOldVersions) {
                string songPath = oldLevel.Key;
                DirectoryInfo parent = Directory.GetParent(songPath);
                while (parent.Name != "CustomLevels") {
                    songPath = parent.FullName;
                    parent = parent.Parent;
                }

                // Only delete the old song after the new one is downloaded and extracted
                Utilities.EmptyDirectory(songPath, true);
                Loader.Instance.RemoveSongWithLevelID(oldLevel.Value.levelID);
            }

            string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels", Regex.Replace($"{songkey} ({oldLevel.Value.songName} - {oldLevel.Value.levelAuthorName})", "[:*/?\"<>|]", ""));
            string localPath = Path.Combine(Path.GetTempPath(), $"{songHash}.zip");

            // Download and extract the update
            await Utilities.DownloadFile($"https://beatsaver.com{song["downloadURL"].Value}", localPath);
            await Utilities.ExtractZip(localPath, currentSongDirectory);
            while (Loader.AreSongsLoaded && !Loader.AreSongsLoading) {
                await Task.Delay(200);
            }
            Loader.Instance.RefreshSongs(false);
            while (Loader.AreSongsLoaded && !Loader.AreSongsLoading) {
                await Task.Delay(200);
            }
            bool success = false;
            // Try to scroll to the newly updated level, if it exists in the list
            var levels = Loader.CustomLevels.Where(l => l.Value.levelID.Split('_').Last().ToUpper() == songHash).Select(x => x.Value);
            if (levels.Any()) {
                Logger.Info($"Scrolling to level {levels.First().levelID}");
                SongListUtils.ScrollToLevel(levels.First().levelID, (s) => success = s, false);
            }

            if (!success) {
                Logger.Info("Failed to find new level!");
                DisplayNotification("Song update failed.");
                _downloaderRunning = false;
                return;
            }

            // Write our download history to file
            if (!SongDownloadHistory.Contains(songkey)) SongDownloadHistory.Add(songkey);
            Utilities.WriteStringListSafe(_historyPath, SongDownloadHistory.Distinct().ToList());

            DisplayNotification("Song update complete!");
            Logger.Info($"Success updating song {songkey}");
            _downloaderRunning = false;
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

        private async void HandleDidSelectLevelEvent(string levelId)
        {
            Logger.Info($"Selected level {levelId}");
            if (levelId.Length > 32)// && CurrentLevels.Any(x => x.levelID == levelId))
            {
                if (Loader.GetLevelById(levelId) == null) {
                    Logger.Info("Level does not exist!");
                    return;
                }
                var level = Loader.CustomLevels.FirstOrDefault(x => x.Value.levelID.ToUpper() == levelId.ToUpper());

                bool zippedSong = false;
                string songPath = level.Key;
                if (!string.IsNullOrEmpty(songPath) && songPath.Contains("/.cache/"))
                    zippedSong = true;

                if (string.IsNullOrEmpty(songPath)) {
                    Logger.Info("Song path is null or empty!");
                    return;
                }
                if (!Directory.Exists(songPath)) {
                    Logger.Info("Song folder does not exist!");
                    return;
                }

                if (!zippedSong) {
                    string songHash = levelId.Split('_').Last();
                    if (!_updateCheckTracker.ContainsKey(songHash) || (_updateCheckTracker.ContainsKey(songHash) && (DateTime.Now - _updateCheckTracker[songHash]).TotalSeconds >= _updateCheckIntervalMinutes * 60)) {
                        _updateCheckTracker[songHash] = DateTime.Now;
                        //Logger.Info($"Getting latest version for song with hash {songHash}");

                        var res = await WebClient.GetAsync($"https://beatsaver.com/api/maps/by-hash/{songHash}", new CancellationTokenSource().Token);
                        if (!res.IsSuccessStatusCode) {
                            Logger.Info($"{res.StatusCode}");
                            return;
                        }

                        JSONNode result = JSON.Parse(res.ContentToString());
                        if (result["hash"].Value.ToLower() != songHash.ToLower()) {
                            Logger.Info("Downloading update for " + level.Value.songName);
                            DisplayNotification($"Updating song {level.Value.songName}");

                            _updateQueue.Push(new KeyValuePair<JSONObject, KeyValuePair<string, CustomPreviewBeatmapLevel>>(result.AsObject, level));
                        }
                    }
                }
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
                _downloaderRunning = true;
                do {
                    DisplayNotification($"Checking {author}'s maps. ({pageCount} page)");
                    var res = await WebClient.GetAsync($"https://beatsaver.com/api/search/advanced/{pageCount}?q=uploader.username:{author}", new CancellationTokenSource().Token);
                    if (!res.IsSuccessStatusCode) {
                        Logger.Info($"{res.StatusCode}");
                        return;
                    }
                    result = JSON.Parse(res.ContentToString());
                    var docs = result["docs"].AsArray;
                    lastPage = result["lastPage"].AsInt;

                    foreach (var keyvalue in docs) {
                        while (_isInGame || Loader.AreSongsLoading) {
                            Logger.Debug($"{_isInGame}");
                            Logger.Debug($"{Loader.AreSongsLoading}");
                            await Task.Delay(200);
                        }
                        var song = keyvalue.Value as JSONObject;
                        Logger.Debug($"{song}");
                        var hash = song["hash"].Value;
                        var key = song["key"].Value;
                        var songName = song["name"].Value;
                        var downloadFailed = false;
                        if (PluginConfig.Instance.AutoDownloadSongs && Loader.GetLevelByHash(hash.ToUpper()) == null) {
                            try {
                                var metaData = song["metadata"].AsObject;
                                Logger.Debug($"{hash} : {songName}");
                                string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels", Regex.Replace($"{key} ({songName} - {metaData["songAuthorName"].Value})", "[:*/?\"<>|]", ""));
                                
                                Logger.Debug($"{songName} : {currentSongDirectory}");
                                DisplayNotification($"Downloading {songName}");
                                string localPath = Path.Combine(Path.GetTempPath(), $"{hash}.zip");
                                var url = $"https://beatsaver.com{song["downloadURL"].Value}";
                                Logger.Info(url);
                                Logger.Info(localPath);
                                DisplayNotification($"Download - {songName}");
                                await Utilities.DownloadFile(url, localPath);
                                if (File.Exists(localPath)) {
                                    await Utilities.ExtractZip(localPath, currentSongDirectory);
                                    _didDownloadAnySong = true;
                                }
                                else
                                    downloadFailed = true;
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
            finally {
                _downloaderRunning = false;
            }

            // Write our download history to file
            Utilities.WriteStringListSafe(_historyPath, SongDownloadHistory.Distinct().ToList());

            // Write to the SyncSaber playlist
            _syncSaberSongs.WritePlaylist();

            Logger.Info($"Downloaded downloadCount songs from mapper \"{author}\" in {((DateTime.Now - startTime - idleTime).Seconds)} seconds. Skipped (totalSongs - downloadCount) songs.");
            _downloaderRunning = false;
        }

        private async Task DownloadBeastSaberFeeds(int feedToDownload)
        {
            _downloaderRunning = true;
            var startTime = DateTime.Now;
            int downloadCount = 0;
            int totalSongs = 0;
            int pageIndex = 0;

            while (true) {
                int totalSongsForPage = 0;
                int downloadCountForPage = 0;

                DisplayNotification($"Checking page {pageIndex} of {_beastSaberFeeds.ElementAt(feedToDownload).Key} feed from BeastSaber!");

                try {
                    var res = await WebClient.GetAsync($"{_beastSaberFeeds.ElementAt(feedToDownload).Value}/feed/?acpage={pageIndex}", new CancellationTokenSource().Token);
                    if (!res.IsSuccessStatusCode) {
                        return;
                    }
                    string beastSaberFeed = res.ContentToString();
                    XmlDocument doc = new XmlDocument();
                    try {
                        doc.LoadXml(beastSaberFeed);
                    }
                    catch (Exception ex) {
                        Logger.Info(ex.ToString());
                        _downloaderRunning = false;
                        return;
                    }

                    XmlNodeList nodes = doc.DocumentElement.SelectNodes("/rss/channel/item");
                    if (nodes?.Count != 0) {
                        foreach (XmlNode node in nodes) {
                            while (_isInGame || Loader.AreSongsLoading) {
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
                            string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels", Regex.Replace($"{key} ({songName} - {node["LevelAuthorName"].InnerText})", "[:*/?\"<>|]", ""));
                            bool downloadFailed = false;
                            if (PluginConfig.Instance.AutoDownloadSongs && !SongDownloadHistory.Contains(key) && Loader.GetLevelByHash(hash.ToUpper()) == null) {
                                DisplayNotification($"Downloading {songName}");

                                string localPath = Path.Combine(Path.GetTempPath(), $"{hash}.zip");
                                await Utilities.DownloadFile($"{downloadUrl}", localPath);
                                if (File.Exists(localPath)) {
                                    await Utilities.ExtractZip(localPath, currentSongDirectory);
                                    downloadCount++;
                                    downloadCountForPage++;
                                    _didDownloadAnySong = true;
                                }
                                else
                                    downloadFailed = true;
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

                            await Task.Delay(200);
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
            _downloaderRunning = false;
        }
    }
}
