using HMUI;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
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

namespace SyncSaber
{
    class SyncSaber : MonoBehaviour
    {
        private readonly Regex _digitRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);

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
            _uiResetTime = DateTime.Now.AddSeconds(5);
            if(_notificationText)
                _notificationText.text = "SyncSaber- " + text;
        }

        private void FixedUpdate()
        {
            if (!_initialized) return;

            if (!_downloaderRunning) {
                if (_updateQueue.Count > 0) {
                    if (_updateQueue.TryPop(out var songUpdateInfo)) {
                        //Logger.Info($"Updating {songUpdateInfo.Key}");
                        //StartCoroutine(UpdateSong(songUpdateInfo));
                    }
                }
                else if (_authorDownloadQueue.Count > 0) {
                    StartCoroutine(DownloadAllSongsByAuthor(_authorDownloadQueue.Pop()));
                }
                else if (PluginConfig.Instance.BeastSaberUsername != "" && _beastSaberFeedIndex < _beastSaberFeeds.Count) {
                    StartCoroutine(DownloadBeastSaberFeeds(_beastSaberFeedIndex));
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
            
            _standardLevelSelectionFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
            if (!_standardLevelSelectionFlowCoordinator) yield break;
            
            _standardLevelListViewController = ReflectionUtil.GetPrivateField<LevelCollectionViewController>(_standardLevelSelectionFlowCoordinator, "_levelCollectionViewController");
            if (!_standardLevelListViewController) yield break;
            
            _standardLevelListViewController.didSelectLevelEvent -= standardLevelListViewController_didSelectLevelEvent;
            _standardLevelListViewController.didSelectLevelEvent += standardLevelListViewController_didSelectLevelEvent;
        }

        private void standardLevelListViewController_didSelectLevelEvent(LevelCollectionViewController sender, IPreviewBeatmapLevel level)
        {
            if (PluginConfig.Instance.AutoUpdateSongs && level != _lastLevel && level.levelID.Length > 32)
            {
                _lastLevel = level;
                StartCoroutine(HandleDidSelectLevelEvent(level.levelID));
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

        private void UpdatePlaylist(Playlist playlist, string songIndex, string songName)
        {
            // Update our playlist with the new song if it doesn't exist, or replace the old song id/name with the updated info if it does
            bool playlistSongFound = false;
            foreach (PlaylistSong s in playlist.Songs)
            {
                string id = songIndex.Substring(0, songIndex.IndexOf("-"));
                string version = songIndex.Substring(songIndex.IndexOf("-") + 1);

                if (s.key.StartsWith(id))
                {
                    if (_beatSaverRegex.IsMatch(s.key))
                    {
                        string oldVersion = s.key.Substring(s.key.IndexOf("-") + 1);
                        if (Convert.ToInt32(oldVersion) < Convert.ToInt32(version))
                        {
                            s.key = songIndex;
                            s.songName = songName;
                            Logger.Info($"Success updating playlist {playlist.Title}! Updated \"{songName}\" with index {id} from version {oldVersion} to {version}");
                        }
                    }
                    else if(_digitRegex.IsMatch(s.key))
                    {
                        s.key = songIndex;
                        s.songName = songName;
                        Logger.Info($"Success updating playlist {playlist.Title}! Song \"{songName}\" with index {id} was missing version! Adding version {version}");
                    }
                    playlistSongFound = true;
                }
            }
            if (!playlistSongFound)
            {
                playlist.Add(songIndex, songName);
                Logger.Info($"Success adding new song \"{songName}\" with BeatSaver index {songIndex} to playlist {playlist.Title}!");
            }
        }

        private IEnumerator HandleDidSelectLevelEvent(string levelId)
        {
            Logger.Info($"Selected level {levelId}");
            if (levelId.Length > 32)// && CurrentLevels.Any(x => x.levelID == levelId))
            {
                var level = Loader.CustomLevels.FirstOrDefault(x => x.Value.levelID.ToUpper() == levelId.ToUpper());
                if (level.Key == null)
                {
                    Logger.Info("Level does not exist!");
                    yield break;
                }

                bool zippedSong = false;
                string _songPath = level.Key;
                if (!string.IsNullOrEmpty(_songPath) && _songPath.Contains("/.cache/"))
                    zippedSong = true;

                if (string.IsNullOrEmpty(_songPath))
                {
                    Logger.Info("Song path is null or empty!");
                    yield break;
                }
                if (!Directory.Exists(_songPath))
                {
                    Logger.Info("Song folder does not exist!");
                    yield break;
                }

                if (!zippedSong)
                {
                    string songHash = levelId.Substring(0, 32);
                    if (!_updateCheckTracker.ContainsKey(songHash) || (_updateCheckTracker.ContainsKey(songHash) && (DateTime.Now -_updateCheckTracker[songHash]).TotalSeconds >= _updateCheckIntervalMinutes * 60))
                    {
                        _updateCheckTracker[songHash] = DateTime.Now;
                        //Logger.Info($"Getting latest version for song with hash {songHash}");
                        using (UnityWebRequest www = UnityWebRequest.Get($"https://beatsaver.com/api/maps/by-hash/{songHash}"))
                        {
                            yield return www.SendWebRequest();
                            if (www.isNetworkError || www.isHttpError)
                            {
                                Logger.Info(www.error);
                                yield break;
                            }
                            JSONNode result = JSON.Parse(www.downloadHandler.text);
                            if (result["total"].AsInt == 0) yield break;

                            foreach (JSONObject song in result["songs"].AsArray)
                            {
                                if ((song["hashMd5"].Value).ToLower() != songHash.ToLower())
                                {
                                    Logger.Info("Downloading update for " + level.Value.songName);
                                    DisplayNotification($"Updating song {level.Value.songName}");

                                    _updateQueue.Push(new KeyValuePair<JSONObject, KeyValuePair<string, CustomPreviewBeatmapLevel>>(song, level));
                                    break;
                                }
                            }
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
        
        private IEnumerator DownloadAllSongsByAuthor(string author)
        {
            Logger.Info($"Downloading all songs from {author}");

            _downloaderRunning = true;
            var startTime = DateTime.Now;
            TimeSpan idleTime = new TimeSpan();

            string mapperId = String.Empty;
            using (UnityWebRequest www = UnityWebRequest.Get($"https://beatsaver.com/api/songs/search/user/{author}"))
            {
                yield return www.SendWebRequest();
                if (www.isNetworkError || www.isHttpError)
                {
                    Logger.Info(www.error);
                    _downloaderRunning = false;
                    yield break;
                }

                JSONNode result = JSON.Parse(www.downloadHandler.text);
                if (result["total"].AsInt == 0)
                {
                    _downloaderRunning = false;
                    yield break;
                }

                foreach (JSONObject song in result["songs"].AsArray)
                {
                    mapperId = song["uploaderId"].Value;
                    break;
                }

                if (mapperId == String.Empty)
                {
                    Logger.Info($"Failed to find mapper \"{author}\"");
                    _downloaderRunning = false;
                    yield break;
                }
            }

            int downloadCount = 0, currentSongIndex = 0, totalSongs = 1;

            DisplayNotification($"Checking for new songs from \"{author}\"");

            while (currentSongIndex < totalSongs)
            {
                using (UnityWebRequest www = UnityWebRequest.Get($"https://beatsaver.com/api/search/text/{currentSongIndex}?q={mapperId}/"))
                {
                    yield return www.SendWebRequest();
                    if (www.isNetworkError || www.isHttpError)
                    {
                        Logger.Info(www.error);
                        _downloaderRunning = false;
                        yield break;
                    }

                    JSONArray result = JSON.Parse(www.downloadHandler.text)["docs"].AsArray;
                    if (result.Count == 0)
                    {
                        _downloaderRunning = false;
                        yield break;
                    }

                    totalSongs = result.Count;

                    foreach (JSONObject song in result)
                    {
                        while (_isInGame || Loader.AreSongsLoading)
                            yield return null;

                        var hash = song["hash"].Value;
                        var songName = song["songName"].Value;
                        string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "CustomLevels", $"{song["id"].Value} ({song["songName"].Value} - {song["levelAuthor"].Value})");
                        bool downloadFailed = false;
                        if (PluginConfig.Instance.AutoDownloadSongs && Loader.GetLevelByHash(hash) == null)
                        {
                            DisplayNotification($"Downloading {songName}");

                            string localPath = Path.Combine(Path.GetTempPath(), $"{hash}.zip");
                            yield return Utilities.DownloadFile($"https://beatsaver.com{song["downloadURL"].Value}", localPath);
                            if (File.Exists(localPath))
                            {
                                yield return Utilities.ExtractZip(localPath, currentSongDirectory);
                                downloadCount++;
                                _didDownloadAnySong = true;
                            }
                            else
                                downloadFailed = true;
                        }

                        // Keep a history of all the songs we download- count it as downloaded even if the user already had it downloaded previously so if they delete it it doesn't get redownloaded
                        if (!downloadFailed && !_songDownloadHistory.Contains(hash))
                        {
                            _songDownloadHistory.Add(hash);

                            // Update our playlist with the latest song info
                            UpdatePlaylist(_syncSaberSongs, hash, songName);

                            // Delete any duplicate songs that we've downloaded
                            RemoveOldVersions(hash);
                        }

                        currentSongIndex++;

                        yield return null;
                    }
                }
            }

            // Write our download history to file
            Utilities.WriteStringListSafe(_historyPath, _songDownloadHistory.Distinct().ToList());

            // Write to the SyncSaber playlist
            _syncSaberSongs.WritePlaylist();
            
            Logger.Info($"Downloaded {downloadCount} songs from mapper \"{author}\" in {((DateTime.Now - startTime - idleTime).Seconds)} seconds. Skipped {(totalSongs - downloadCount)} songs.");
            _downloaderRunning = false;
        }

        private IEnumerator DownloadBeastSaberFeeds(int feedToDownload)
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

                using (UnityWebRequest www = UnityWebRequest.Get($"{_beastSaberFeeds.ElementAt(feedToDownload).Value}/feed/?acpage={pageIndex.ToString()}"))
                {
                    yield return www.SendWebRequest();
                    if (www.isNetworkError || www.isHttpError)
                    {
                        Logger.Info(www.error);
                        _downloaderRunning = false;
                        yield break;
                    }
                    
                    string beastSaberFeed = www.downloadHandler.text;
                    XmlDocument doc = new XmlDocument();
                    try
                    {
                        doc.LoadXml(beastSaberFeed);
                    }
                    catch (Exception ex)
                    {
                        Logger.Info(ex.ToString());
                        _downloaderRunning = false;
                        yield break;
                    }

                    XmlNodeList nodes = doc.DocumentElement.SelectNodes("/rss/channel/item");
                    foreach (XmlNode node in nodes)
                    {
                        while (_isInGame || Loader.AreSongsLoading)
                            yield return null;


                        if (node["DownloadURL"] == null || node["SongTitle"] == null)
                        {
                            Logger.Info("Essential node was missing! Skipping!");
                            continue;
                        }
                            
                        string songName = node["SongTitle"].InnerText;
                        string downloadUrl = node["DownloadURL"].InnerText;

                        if (downloadUrl.Contains("dl.php"))
                        {
                            Logger.Info("Skipping BeastSaber download with old url format!");
                            totalSongs++;
                            totalSongsForPage++;
                            continue;
                        }
                        
                        string songIndex = downloadUrl.Substring(downloadUrl.LastIndexOf('/') + 1);
                        string currentSongDirectory = Path.Combine(Environment.CurrentDirectory, "CustomSongs", songIndex);
                        bool downloadFailed = false;
                        if (PluginConfig.Instance.AutoDownloadSongs && !_songDownloadHistory.Contains(songIndex) && !Directory.Exists(currentSongDirectory))
                        {
                            DisplayNotification($"Downloading {songName}");

                            string localPath = Path.Combine(Path.GetTempPath(), $"{songIndex}.zip");
                            yield return Utilities.DownloadFile($"https://beatsaver.com/download/{songIndex}", localPath);
                            if (File.Exists(localPath))
                            {
                                yield return Utilities.ExtractZip(localPath, currentSongDirectory);
                                downloadCount++;
                                downloadCountForPage++;
                                _didDownloadAnySong = true;
                            }
                            else
                                downloadFailed = true;
                        }

                        // Keep a history of all the songs we download- count it as downloaded even if the user already had it downloaded previously so if they delete it it doesn't get redownloaded
                        if (!downloadFailed && !_songDownloadHistory.Contains(songIndex))
                        {
                            _songDownloadHistory.Add(songIndex);

                            // Update our playlist with the latest song info
                            UpdatePlaylist(_syncSaberSongs, songIndex, songName);
                            UpdatePlaylist(GetPlaylistForFeed(feedToDownload), songIndex, songName);

                            // Delete any duplicate songs that we've downloaded
                            RemoveOldVersions(songIndex);
                        }

                        totalSongs++;
                        totalSongsForPage++;

                        yield return null;
                    }

                    if (totalSongsForPage == 0)
                    {
                        //Logger.Info("Reached end of feed!");
                        break;
                    }
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
