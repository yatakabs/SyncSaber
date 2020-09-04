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
        private SynchronizationContext context;

        private bool _downloaderRunning = false;
        private bool _downloaderComplete = false;
        private bool _isInGame = false;
        private string _historyPath = null;
        private int _beastSaberFeedIndex = 0;

        private Stack<string> _authorDownloadQueue = new Stack<string>();
        private ConcurrentStack<KeyValuePair<JSONObject, KeyValuePair<string, CustomPreviewBeatmapLevel>>> _updateQueue = new ConcurrentStack<KeyValuePair<JSONObject, KeyValuePair<string, CustomPreviewBeatmapLevel>>>();
        private List<string> _songDownloadHistory = new List<string>();
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

            if (PluginConfig.Instance.SyncFollowingsFeed)
                _beastSaberFeeds.Add("followings", $"https://bsaber.com/members/{PluginConfig.Instance.BeastSaberUsername}/wall/followings");
            if (PluginConfig.Instance.SyncBookmarksFeed)
                _beastSaberFeeds.Add("bookmarks", $"https://bsaber.com/members/{PluginConfig.Instance.BeastSaberUsername}/bookmarks");
            if (PluginConfig.Instance.SyncCuratorRecommendedFeed)
                _beastSaberFeeds.Add("curator recommended", $"https://bsaber.com/members/curatorrecommended/bookmarks");

            _songBrowserInstalled = Utilities.IsModInstalled("Song Browser");
            SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;

            _historyPath = Path.Combine(Environment.CurrentDirectory, "UserData", "SyncSaberHistory.txt");
            if (File.Exists(_historyPath + ".bak"))
            {
                // Something went wrong when the history file was being written previously, restore it from backup
                if (File.Exists(_historyPath)) File.Delete(_historyPath);
                File.Move(_historyPath + ".bak", _historyPath);
            }
            if (File.Exists(_historyPath))
                _songDownloadHistory = File.ReadAllLines(_historyPath).ToList();

            if (!Directory.Exists(Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels"))) {
                Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels"));
            }

            string favoriteMappersPath = Path.Combine(Environment.CurrentDirectory, "UserData", "FavoriteMappers.ini");
            if (!File.Exists(favoriteMappersPath))
                File.WriteAllLines(favoriteMappersPath, new string[] { "" }); // "freeek", "purphoros", "bennydabeast", "rustic", "greatyazer"

            foreach (string mapper in File.ReadAllLines(favoriteMappersPath))
            {
                Logger.Info($"Mapper: {mapper}");
                _authorDownloadQueue.Push(mapper);
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
            context.Post(d =>
            {
                _uiResetTime = DateTime.Now.AddSeconds(5);
                if (_notificationText)
                    _notificationText.text = "SyncSaber- " + text;
            }, null);
        }

        private async void FixedUpdate()
        {
            try {
                if (!_initialized) return;

                if (!_downloaderRunning) {
                    //if (_updateQueue.Any()) {
                    //    if (_updateQueue.TryPop(out var songUpdateInfo)) {
                    //        //Logger.Info($"Updating {songUpdateInfo.Key}");
                    //        StartCoroutine(UpdateSong(songUpdateInfo));
                    //    }
                    //}
                    //else
                    if (_authorDownloadQueue.Any()) {
                        await DownloadAllSongsByAuthor(_authorDownloadQueue.Pop());
                    }
                    else if (!string.IsNullOrWhiteSpace(PluginConfig.Instance.BeastSaberUsername) && _beastSaberFeedIndex < _beastSaberFeeds.Count) {
                        await DownloadBeastSaberFeeds(_beastSaberFeedIndex);
                        _beastSaberFeedIndex++;
                    }
                    else if (_authorDownloadQueue.Count == 0 && !_downloaderComplete) {
                        if (!Loader.AreSongsLoading) {
                            if (_didDownloadAnySong) {
                                StartCoroutine(SongListUtils.RefreshSongs(false, false));
                            }
                            Logger.Info("Finished checking for updates!");
                            DisplayNotification("Finished checking for new songs!");
                            _downloaderComplete = true;
                        }
                    }
                }

                if (_uiResetTime <= DateTime.Now && _notificationText.text != String.Empty)
                    _notificationText.text = String.Empty;
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }

        private void SceneManagerOnActiveSceneChanged(Scene arg0, Scene scene)
        {
            StartCoroutine(DelayedActiveSceneChanged(scene));
        }

        private IEnumerator DelayedActiveSceneChanged(Scene scene)
        {
            yield return new WaitForSeconds(0.1f);

            SongListUtils.Initialize();

            if (scene.name == "GameCore") _isInGame = true;
            if (scene.name != "MenuCore") yield break;
            _isInGame = false;
            
            _standardLevelSelectionFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().FirstOrDefault();
            if (!_standardLevelSelectionFlowCoordinator) yield break;

            var selectionNavigate = _standardLevelSelectionFlowCoordinator.GetPrivateField<LevelSelectionNavigationController>("_levelSelectionNavigationController");


            _standardLevelListViewController = selectionNavigate.GetPrivateField<LevelCollectionViewController>("_levelCollectionViewController");
            if (!_standardLevelListViewController) yield break;
            
            _standardLevelListViewController.didSelectLevelEvent -= standardLevelListViewController_didSelectLevelEvent;
            _standardLevelListViewController.didSelectLevelEvent += standardLevelListViewController_didSelectLevelEvent;
        }

        private void standardLevelListViewController_didSelectLevelEvent(LevelCollectionViewController sender, IPreviewBeatmapLevel level)
        {
            if (PluginConfig.Instance.AutoUpdateSongs && level != _lastLevel && level.levelID.Length > 32)
            {
                _lastLevel = level;
                HandleDidSelectLevelEvent(level.levelID);
            }
        }

        private int GetMaxBeastSaberPages(int feedToDownload)
        {
            switch(_beastSaberFeeds.ElementAt(feedToDownload).Key)
            {
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
            switch (_beastSaberFeeds.ElementAt(feedToDownload).Key)
            {
                case "followings":
                    return _followingsSongs;
                case "bookmarks":
                    return _bookmarksSongs;
                case "curator recommended":
                    return _curatorRecommendedSongs;
            }
            return null;
        }
        
        //private IEnumerator UpdateSong(KeyValuePair<JSONObject, KeyValuePair<string, CustomPreviewBeatmapLevel>> songUpdateInfo)
        //{
        //    _downloaderRunning = true;
        //    JSONObject song = songUpdateInfo.Key;
        //    var oldLevel = songUpdateInfo.Value;
        //    string songIndex = song["version"].Value;
        //    string songHash = (song["hashMd5"].Value).ToUpper();
            
        //    if (PluginConfig.Instance.DeleteOldVersions)
        //    {
        //        string songPath = oldLevel.Key;
        //        DirectoryInfo parent = Directory.GetParent(songPath);
        //        while(parent.Name != "CustomSongs")
        //        {
        //            songPath = parent.FullName;
        //            parent = parent.Parent;
        //        }

        //        // Only delete the old song after the new one is downloaded and extracted
        //        Utilities.EmptyDirectory(songPath, true);
        //        Loader.Instance.RemoveSongWithLevelID(oldLevel.Value.levelID);
        //    }
            
        //    string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "CustomSongs", songIndex);
        //    string localPath = Path.Combine(Path.GetTempPath(), $"{songIndex}.zip");

        //    // Download and extract the update
        //    yield return Utilities.DownloadFile($"https://beatsaver.com/{song["downloadURL"].Value}", localPath);
        //    yield return Utilities.ExtractZip(localPath, currentSongDirectory);
        //    yield return new WaitUntil(() => Loader.AreSongsLoaded && !Loader.AreSongsLoading);
        //    Loader.Instance.RefreshSongs(false);
        //    yield return new WaitUntil(() => Loader.AreSongsLoaded && !Loader.AreSongsLoading);

        //    bool success = false;
        //    // Try to scroll to the newly updated level, if it exists in the list
        //    var levels = Loader.CustomLevels.Where(l => l.Value.levelID.Split('_').Last() == songHash).Select(x => x.Value).ToArray();
        //    if (levels.Length > 0)
        //    {
        //        Logger.Info($"Scrolling to level {levels[0].levelID}");
        //        yield return SongListUtils.ScrollToLevel(levels[0].levelID, (s) => success = s, false);
        //    }

        //    if(!success)
        //    {
        //        Logger.Info("Failed to find new level!");
        //        DisplayNotification("Song update failed.");
        //        _downloaderRunning = false;
        //        yield break;
        //    }

        //    // Write our download history to file
        //    if (!_songDownloadHistory.Contains(songIndex)) _songDownloadHistory.Add(songIndex);
        //    Utilities.WriteStringListSafe(_historyPath, _songDownloadHistory.Distinct().ToList());

        //    DisplayNotification("Song update complete!");
        //    Logger.Info($"Success updating song {songIndex}");
        //    _downloaderRunning = false;
        //}

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
            
            if (!playlistSongFound)
            {
                playlist.Add(hash, songName);
                Logger.Info($"Success adding new song \"{songName}\" with BeatSaver index {hash} to playlist {playlist.Title}!");
            }
        }

        private async void HandleDidSelectLevelEvent(string levelId)
        {
            Logger.Info($"Selected level {levelId}");
            if (levelId.Length > 32)// && CurrentLevels.Any(x => x.levelID == levelId))
            {
                if (Loader.GetLevelById(levelId) == null)
                {
                    Logger.Info("Level does not exist!");
                    return;
                }
                var level = Loader.CustomLevels.FirstOrDefault(x => x.Value.levelID.ToUpper() == levelId.ToUpper());

                bool zippedSong = false;
                string _songPath = level.Key;
                if (!string.IsNullOrEmpty(_songPath) && _songPath.Contains("/.cache/"))
                    zippedSong = true;

                if (string.IsNullOrEmpty(_songPath))
                {
                    Logger.Info("Song path is null or empty!");
                    return;
                }
                if (!Directory.Exists(_songPath))
                {
                    Logger.Info("Song folder does not exist!");
                    return;
                }

                if (!zippedSong)
                {
                    string songHash = levelId.Split('_').Last();
                    if (!_updateCheckTracker.ContainsKey(songHash) || (_updateCheckTracker.ContainsKey(songHash) && (DateTime.Now -_updateCheckTracker[songHash]).TotalSeconds >= _updateCheckIntervalMinutes * 60))
                    {
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

        private void RemoveOldVersions(string songIndex)
        {
            if (!PluginConfig.Instance.DeleteOldVersions) return;

            string[] customSongDirectories = Directory.GetDirectories(Path.Combine(Environment.CurrentDirectory, "CustomSongs"));
            string id = songIndex.Substring(0, songIndex.IndexOf("-"));
            string version = songIndex.Substring(songIndex.IndexOf("-") + 1);

            foreach (string directory in customSongDirectories)
            {
                try
                {
                    string directoryName = Path.GetFileName(directory);
                    if (_beatSaverRegex.IsMatch(directoryName) && directoryName != songIndex)
                    {
                        string directoryId = directoryName.Substring(0, directoryName.IndexOf("-"));
                        if (directoryId == id)
                        {
                            string directoryVersion = directoryName.Substring(directoryName.IndexOf("-") + 1);
                            string directoryToRemove = directory;
                            string currentVersion = songIndex;
                            string oldVersion = directoryName;
                            if (Convert.ToInt32(directoryVersion) > Convert.ToInt32(version))
                            {
                                directoryToRemove = Path.Combine(Environment.CurrentDirectory, "CustomSongs", songIndex);
                                currentVersion = directoryName;
                                oldVersion = songIndex;
                            }
                            Logger.Info($"Deleting old song with identifier \"{oldVersion}\" (current version: {currentVersion})");
                            Directory.Delete(directoryToRemove, true);
                        }
                    }
                    else if (_digitRegex.IsMatch(directoryName) && directoryName == id)
                    {
                        Logger.Info($"Deleting old song with identifier \"{directoryName}\" (current version: {id}-{version})");
                        Directory.Delete(directory, true);
                    }
                }
                catch (Exception e)
                {
                    Logger.Info($"Exception when trying to remove old versions {e.ToString()}");
                }
            }
        }
        
        private async Task DownloadAllSongsByAuthor(string author)
        {
            Logger.Info($"Downloading all songs from {author}");
            JSONArray result = null;
            _downloaderRunning = true;
            var startTime = DateTime.Now;
            TimeSpan idleTime = new TimeSpan();

            string mapperId = String.Empty;
            try {
                var res = await WebClient.GetAsync($"https://beatsaver.com/api/search/text/0?q={mapperId}/", new CancellationTokenSource().Token);
                if (res.IsSuccessStatusCode) {
                    Logger.Info($"{res.StatusCode}");
                    _downloaderRunning = false;
                    return;
                }
                result = JSON.Parse(res.ContentToString())["docs"].AsArray;
                Logger.log.Notice($"docs count : {result.Count}");
                if (result.Count == 0) {
                    _downloaderRunning = false;
                    return;
                }

                foreach (var song in result) {
                    mapperId = song.Value["uploader"].AsObject["username"];
                    Logger.Info($"mapperID : {mapperId}");
                    break;
                }

                if (string.IsNullOrEmpty(mapperId)) {
                    Logger.Info($"Failed to find mapper \"{author}\"");
                    _downloaderRunning = false;
                    return;
                }
                DisplayNotification($"Checking for new songs from \"{author}\"");
                Logger.Info($"Checking for new songs from \"{author}\"");
            }
            catch (Exception e) {
                Logger.Error($"{e}");
            }

            int pageCount = 0;


            do {
                try {
                    var res = await WebClient.GetAsync($"https://beatsaver.com/api/search/text/{pageCount}?q={mapperId}/", new CancellationTokenSource().Token);
                    if (res.IsSuccessStatusCode) {
                        Logger.Info($"{res.StatusCode}");
                        _downloaderRunning = false;
                        return;
                    }

                    result = JSON.Parse(res.ContentToString())["docs"].AsArray;
                    Logger.Debug($"{result}");
                    if (result.Count == 0) {
                        _downloaderRunning = false;
                        break;
                    }

                    foreach (var keyvalue in result) {
                        var song = keyvalue.Value as JSONObject;
                        while (_isInGame || Loader.AreSongsLoading) {
                            Logger.Debug($"{_isInGame}");
                            Logger.Debug($"{Loader.AreSongsLoading}");
                            await Task.Delay(200);
                        }

                        Logger.Debug($"{song}");
                        var hash = song["hash"].Value;
                        var songName = song["name"].Value;
                        var metaData = song["metadata"].AsObject;
                        Logger.Info($"{hash} : {songName} : {metaData}");
                        string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels", $"{song["key"].Value} ({songName} - {metaData["songAuthorName"].Value})");
                        bool downloadFailed = false;
                        Logger.Info($"{songName} : {currentSongDirectory}");
                        if (PluginConfig.Instance.AutoDownloadSongs && Loader.GetLevelByHash(hash.ToUpper()) == null) {
                            Logger.Info($"Downloading {songName}");
                            DisplayNotification($"Downloading {songName}");
                            string localPath = Path.Combine(Path.GetTempPath(), $"{hash}.zip");
                            var url = $"https://beatsaver.com{song["downloadURL"].Value}";
                            Logger.Info(url);
                            Logger.Info(localPath);
                            await Utilities.DownloadFile(url, localPath);
                            if (File.Exists(localPath)) {
                                await Utilities.ExtractZip(localPath, currentSongDirectory);
                                _didDownloadAnySong = true;
                            }
                            else
                                downloadFailed = true;
                        }
                        Logger.Info("Finish download");
                        // Keep a history of all the songs we download- count it as downloaded even if the user already had it downloaded previously so if they delete it it doesn't get redownloaded
                        if (!downloadFailed && !_songDownloadHistory.Contains(hash)) {
                            _songDownloadHistory.Add(hash);

                            // Update our playlist with the latest song info
                            UpdatePlaylist(_syncSaberSongs, hash, songName);

                            // Delete any duplicate songs that we've downloaded
                            RemoveOldVersions(hash);
                        }
                    }
                    pageCount++;
                }
                catch (Exception e) {
                    Logger.Error(e);
                }
                
            } while (result?.Count != 0);

            // Write our download history to file
            Utilities.WriteStringListSafe(_historyPath, _songDownloadHistory.Distinct().ToList());

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

            while (true)
            {
                int totalSongsForPage = 0;
                int downloadCountForPage = 0;
                
                DisplayNotification($"Checking page {pageIndex.ToString()} of {_beastSaberFeeds.ElementAt(feedToDownload).Key} feed from BeastSaber!");

                try {
                    var res = await WebClient.GetAsync($"{_beastSaberFeeds.ElementAt(feedToDownload).Value}/feed/?acpage={pageIndex.ToString()}", new CancellationTokenSource().Token);
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
                            string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels", $"{key} ({songName} - {node["LevelAuthorName"].InnerText})");
                            bool downloadFailed = false;
                            if (PluginConfig.Instance.AutoDownloadSongs && !_songDownloadHistory.Contains(hash) && Loader.GetLevelByHash(hash) == null) {
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
                            if (!downloadFailed && !_songDownloadHistory.Contains(key)) {
                                _songDownloadHistory.Add(key);

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
                Utilities.WriteStringListSafe(_historyPath, _songDownloadHistory.Distinct().ToList());

                // Write to the SynCSaber playlist
                _syncSaberSongs.WritePlaylist();
                GetPlaylistForFeed(feedToDownload).WritePlaylist();
                
                //Logger.Info($"Reached end of page! Found {totalSongsForPage.ToString()} songs total, downloaded {downloadCountForPage.ToString()}!");
                pageIndex++;

                if (pageIndex > GetMaxBeastSaberPages(feedToDownload) + 1 && GetMaxBeastSaberPages(feedToDownload) != 0)
                    break;
            }
            Logger.Info($"Downloaded {downloadCount} songs from BeastSaber {_beastSaberFeeds.ElementAt(feedToDownload).Key} feed in {((DateTime.Now - startTime).Seconds)} seconds. Checked {(pageIndex+1)} page{(pageIndex>0?"s":"")}, skipped {(totalSongs - downloadCount)} songs.");
            _downloaderRunning = false;
        }
    }
}
