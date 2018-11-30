using HMUI;
using SongLoaderPlugin;
using SongLoaderPlugin.OverrideClasses;
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
using SongBrowserPlugin;
using SimpleJSON;

namespace SyncSaber
{
    class SyncSaber : MonoBehaviour
    {
        private readonly Regex _digitRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);
        private readonly Regex _beatSaverRegex = new Regex("^[0-9]+-[0-9]+$", RegexOptions.Compiled);

        private bool _downloaderRunning = false;
        private bool _downloaderComplete = false;
        private bool _isRefreshing = false;
        private bool _isInGame = false;
        private string _historyPath = null;
        private int _beatSaberFeedToDownload = 0;

        private Stack<string> _authorDownloadQueue = new Stack<string>();
        private ConcurrentStack<KeyValuePair<string,CustomLevel>> _updateQueue = new ConcurrentStack<KeyValuePair<string,CustomLevel>>();
        private List<string> _songDownloadHistory = new List<string>();
        private Playlist _syncSaberSongs = new Playlist("SyncSaber Playlist", "brian91292", "1");

        private IBeatmapLevel _lastLevel;
        private SoloFreePlayFlowCoordinator _standardLevelSelectionFlowCoordinator;
        private LevelListViewController _standardLevelListViewController;

        private TMP_Text _notificationText;
        private DateTime _uiResetTime;
        private bool _songBrowserInstalled = false;

        private List<IBeatmapLevel> CurrentLevels
        {
            get
            {
                return ReflectionUtil.GetPrivateField<IBeatmapLevel[]>(_standardLevelListViewController, "_levels").ToList();
            }
            set
            {
                ReflectionUtil.SetPrivateField(_standardLevelListViewController, "_levels", value);
            }
        }

        private void Awake()
        {
            UnityEngine.Object.DontDestroyOnLoad(this.gameObject);

            _songBrowserInstalled = Utilities.IsModInstalled("Song Browser");
            SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;

            _historyPath = $"{Environment.CurrentDirectory}\\UserData\\SyncSaberHistory.txt";
            if (File.Exists(_historyPath + ".bak"))
            {
                // Something went wrong when the history file was being written previously, restore it from backup
                if (File.Exists(_historyPath)) File.Delete(_historyPath);
                File.Move(_historyPath + ".bak", _historyPath);
            }
            if (File.Exists(_historyPath))
                _songDownloadHistory = File.ReadAllLines(_historyPath).ToList();

            if (!Directory.Exists("CustomSongs")) Directory.CreateDirectory("CustomSongs");

            string favoriteMappersPath = $"{Environment.CurrentDirectory}\\UserData\\FavoriteMappers.ini";
            if (!File.Exists(favoriteMappersPath))
                File.WriteAllLines(favoriteMappersPath, new string[] { "" }); // "freeek", "purphoros", "bennydabeast", "rustic", "greatyazer"

            foreach (string mapper in File.ReadAllLines(favoriteMappersPath))
            {
                Plugin.Log($"Mapper: {mapper}");
                _authorDownloadQueue.Push(mapper);
            }

            if (!_syncSaberSongs.ReadPlaylist())
                _syncSaberSongs.WritePlaylist();
            
            _notificationText = Utilities.CreateNotificationText(String.Empty);
            DisplayNotification("SyncSaber Initialized!");
        }

        private void DisplayNotification(string text)
        {
            _uiResetTime = DateTime.Now.AddSeconds(5);
            _notificationText.text = "SyncSaber- " + text;
        }

        private void Update()
        {
            if (_updateQueue.Count > 0)
            {
                if (_updateQueue.TryPop(out var songInfo))
                {
                    Plugin.Log($"Updating {songInfo.Key}");
                    StartCoroutine(UpdateSong(songInfo));
                }
            }

            if (!_downloaderRunning)
            {
                if (Config.BeastSaberUsername != "" && _beatSaberFeedToDownload < 2)
                {
                    StartCoroutine(DownloadBeastSaberFeeds(Config.BeastSaberUsername, _beatSaberFeedToDownload));
                    _beatSaberFeedToDownload++;
                    Plugin.Log("Downloading beastsaber feed!");

                }
                else if (_authorDownloadQueue.Count > 0)
                {
                    StartCoroutine(DownloadAllSongsByAuthor(_authorDownloadQueue.Pop()));
                }
                else if (_authorDownloadQueue.Count == 0 && !_downloaderComplete)
                {
                    if (!SongLoader.AreSongsLoading)
                    {
                        StartCoroutine(RefreshSongs());
                        Plugin.Log("Finished updating songs from all mappers!");
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
            if (scene.name == "GameCore") _isInGame = true;
            if (scene.name != "Menu") return;
            _isInGame = false;

            _standardLevelSelectionFlowCoordinator = Resources.FindObjectsOfTypeAll<SoloFreePlayFlowCoordinator>().First();
            if (!_standardLevelSelectionFlowCoordinator) return;

            _standardLevelListViewController = ReflectionUtil.GetPrivateField<LevelListViewController>(_standardLevelSelectionFlowCoordinator, "_levelListViewController");
            if (!_standardLevelListViewController) return;

            _standardLevelListViewController.didSelectLevelEvent += standardLevelListViewController_didSelectLevelEvent;
        }

        private void standardLevelListViewController_didSelectLevelEvent(LevelListViewController sender, IBeatmapLevel level)
        {
            if (Config.AutoUpdateSongs && level != _lastLevel && level is CustomLevel)
            {
                _lastLevel = level;
                StartCoroutine(HandleDidSelectLevelEvent(level.levelID));
            }
        }
        
        private void RefreshSongBrowser()
        {
           var _songBrowserUI = SongBrowserApplication.Instance.GetPrivateField<SongBrowserPlugin.UI.SongBrowserUI>("_songBrowserUI");
            if (_songBrowserUI)
            {
                _songBrowserUI.UpdateSongList();
                _songBrowserUI.RefreshSongList();
            }
        }

        private IEnumerator RefreshSongs(bool fullRefresh = false, bool selectOldLevel = true)
        {
            if (_isRefreshing) yield break;
            _isRefreshing = true;

            if (!SongLoader.AreSongsLoaded) yield break;

            // Grab the currently selected level id so we can restore it after refreshing
            string selectedLevelId = _standardLevelListViewController.selectedLevel?.levelID;

            // Wait until song loader is finished loading, then refresh the song list
            while (SongLoader.AreSongsLoading) yield return null;
            SongLoader.Instance.RefreshSongs(fullRefresh);
            while (SongLoader.AreSongsLoading) yield return null;

            // If song browser is installed, update/refresh it
            if (_songBrowserInstalled)
                RefreshSongBrowser();

            var table = ReflectionUtil.GetPrivateField<LevelListTableView>(_standardLevelListViewController, "_levelListTableView");
            if (table)
            {
                // Set the row index to the previously selected song
                if (selectOldLevel)
                {
                    int row = table.RowNumberForLevelID(selectedLevelId);
                    TableView tableView = table.GetComponentInChildren<TableView>();
                    tableView.SelectRow(row, true);
                    tableView.ScrollToRow(row, true);
                }
            }
            _isRefreshing = false;
        }

        private IEnumerator UpdateSong(KeyValuePair<string, CustomLevel> songInfo)
        {
            string songIndex = songInfo.Key;
            CustomLevel oldLevel = songInfo.Value;

            Utilities.EmptyDirectory(".songcache", false);

            // Download and extract the update
            string localPath = $"{Environment.CurrentDirectory}\\.songcache\\{songIndex}.zip";
            yield return Utilities.DownloadFile($"https://beatsaver.com/download/{songIndex}", localPath);
            yield return Utilities.ExtractZip(localPath, $"{Environment.CurrentDirectory}\\CustomSongs\\{songIndex}");

            var table = ReflectionUtil.GetPrivateField<LevelListTableView>(_standardLevelListViewController, "_levelListTableView");
            if (Config.DeleteOldVersions)
            {
                // Only delete the old song after the new one is downloaded and extracted
                Directory.Delete(oldLevel.customSongInfo.path, true);
                SongLoader.Instance.RemoveSongWithLevelID(oldLevel.levelID);

                if (_songBrowserInstalled)
                {
                    if (table)
                    {
                        var levels = CurrentLevels;
                        levels.Remove(oldLevel);
                        table.SetLevels(levels.ToArray());
                    }
                }
            }
            
            // Disable our didSelectLevel event, then refresh the song list
            _standardLevelListViewController.didSelectLevelEvent -= standardLevelListViewController_didSelectLevelEvent;
            yield return RefreshSongs(!_songBrowserInstalled, false);
            _standardLevelListViewController.didSelectLevelEvent += standardLevelListViewController_didSelectLevelEvent;

            Plugin.Log("Finished refreshing songs!");
            try
            {
                // Try to scroll to the newly updated level, if it exists in the list
                CustomLevel newLevel = (CustomLevel)CurrentLevels.Where(x => x is CustomLevel && ((CustomLevel)x).customSongInfo.path.Contains(songIndex))?.FirstOrDefault();
                if (newLevel)
                {
                    Plugin.Log("Found new level!");
                    if (table)
                    {
                        // Set the row index to the previously selected song
                        int row = table.RowNumberForLevelID(newLevel.levelID);
                        TableView tableView = table.GetComponentInChildren<TableView>();
                        tableView.SelectRow(row, true);
                        tableView.ScrollToRow(row, true);
                    }
                }
                else
                {
                    Plugin.Log("Failed to find new level!");
                }
            }
            catch (Exception ex)
            {
                Plugin.Log($"Exception when attempting to find new song! {ex.ToString()}");
            }

            // Write our download history to file
            if (!_songDownloadHistory.Contains(songIndex)) _songDownloadHistory.Add(songIndex);
            Utilities.WriteStringListSafe(_historyPath, _songDownloadHistory.Distinct().ToList());

            DisplayNotification("Song update complete!");
            Plugin.Log($"Success updating song {songIndex}");
        }

        private void UpdatePlaylist(string songIndex, string songName)
        {
            // Update our playlist with the new song if it doesn't exist, or replace the old song id/name with the updated info if it does
            bool playlistSongFound = false;
            foreach (PlaylistSong s in _syncSaberSongs.Songs)
            {
                string id = songIndex.Substring(0, songIndex.IndexOf("-"));
                string version = songIndex.Substring(songIndex.IndexOf("-") + 1);

                if (s.key.StartsWith(id))
                {
                    if (s.key.Contains("-"))
                    {
                        string oldVersion = s.key.Substring(s.key.IndexOf("-") + 1);
                        if (Convert.ToInt32(oldVersion) < Convert.ToInt32(version))
                        {
                            s.key = songIndex;
                            s.songName = songName;
                            Plugin.Log($"Updated old playlist song \"{songName}\" with index {id} to version {version}");
                        }
                    }
                    else
                    {
                        s.key = songIndex;
                        s.songName = songName;
                        Plugin.Log($"Playlist song was missing version number! Updated old playlist song \"{songName}\" with index {id} to version {version}");
                    }
                    playlistSongFound = true;
                }
            }
            if (!playlistSongFound)
            {
                _syncSaberSongs.Add(songIndex, songName);
                Plugin.Log($"Added new playlist song \"{songName}\" with BeatSaver index {songIndex}");
            }
        }
        
        private IEnumerator HandleDidSelectLevelEvent(string levelId)
        {
            Plugin.Log($"Selected level {levelId}");
            if (levelId.Length > 32 && CurrentLevels.Any(x => x.levelID == levelId))
            {
                CustomLevel level = SongLoader.CustomLevels.First(x => x.levelID == levelId);
                if (!level)
                {
                    Plugin.Log("Level does not exist!");
                    yield break;
                }

                bool zippedSong = false;
                string _songPath = level.customSongInfo.path;
                if (!string.IsNullOrEmpty(_songPath) && _songPath.Contains("/.cache/"))
                    zippedSong = true;

                if (string.IsNullOrEmpty(_songPath))
                {
                    Plugin.Log("Song path is null or empty!");
                    yield break;
                }
                if (!Directory.Exists(_songPath))
                {
                    Plugin.Log("Song folder does not exist!");
                    yield break;
                }

                if (!zippedSong)
                {
                    string songHash = levelId.Substring(0, 32);
                    Plugin.Log($"Getting latest version for song with hash {songHash}");
                    using (UnityWebRequest www = UnityWebRequest.Get($"https://beatsaver.com/api/songs/search/hash/{songHash}"))
                    {
                        yield return www.SendWebRequest();
                        if (www.isNetworkError || www.isHttpError)
                        {
                            Plugin.Log(www.error);
                            yield break;
                        }
                        JSONNode result = JSON.Parse(www.downloadHandler.text);
                        if (result["total"].AsInt == 0) yield break;

                        foreach (JSONObject song in result["songs"].AsArray)
                        {
                            if (((string)song["hashMd5"]).ToLower() != songHash.ToLower())
                            {
                                Plugin.Log("Downloading update for " + level.customSongInfo.songName);
                                DisplayNotification($"Updating song {level.customSongInfo.songName}");

                                _updateQueue.Push(new KeyValuePair<string, CustomLevel>(song["version"], level));
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // TODO: IMPLEMENT HANDLING FOR ZIPPED SONG UPDATING
                }
            }
        }

        private void RemoveOldVersions(string songIndex)
        {
            if (!Config.DeleteOldVersions) return;

            string[] customSongDirectories = Directory.GetDirectories($"{Environment.CurrentDirectory}\\CustomSongs");
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
                                directoryToRemove = $"{Environment.CurrentDirectory}\\CustomSongs\\{songIndex}";
                                currentVersion = directoryName;
                                oldVersion = songIndex;
                            }
                            Directory.Delete(directoryToRemove, true);
                            Plugin.Log($"Deleting old song with identifier \"{oldVersion}\" (current version: {currentVersion})");
                        }
                    }
                    else if (_digitRegex.IsMatch(directoryName) && directoryName == id)
                    {
                        Directory.Delete(directory, true);
                        Plugin.Log($"Deleting old song with identifier \"{directoryName}\" (current version: {id}-{version})");
                    }
                }
                catch (Exception e)
                {
                    //Plugin.Log($"Exception when trying to remove old versions {e.ToString()}");
                }
            }
        }
        
        private IEnumerator DownloadAllSongsByAuthor(string author)
        {
            _downloaderRunning = true;
            var startTime = DateTime.Now;
            int downloadCount = 0;
            int totalSongs = 0;

            Plugin.Log($"Checking for new releases and updates from \"{author}\"");
            DisplayNotification($"Checking for new songs from \"{author}\"");
            
            string url = $"https://beatsaver.com/api/songs/search/user/{author}";
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();
                if (www.isNetworkError || www.isHttpError)
                {
                    Plugin.Log(www.error);
                    _downloaderRunning = false;
                    yield break;
                }

                JSONNode result = JSON.Parse(www.downloadHandler.text);
                if (result["total"].AsInt == 0) yield break;

                foreach (JSONObject song in result["songs"].AsArray)
                {
                    while (_isInGame)
                        yield return null;

                    string songIndex = song["version"], songName = song["name"];
                    string currentSongDirectory = $"{Environment.CurrentDirectory}\\CustomSongs\\{songIndex}";
                    if (Config.AutoDownloadSongs && !_songDownloadHistory.Contains(songIndex) && !Directory.Exists(currentSongDirectory))
                    {
                        Utilities.EmptyDirectory(".songcache", false);

                        DisplayNotification($"Downloading {songName}");

                        string localPath = $"{Environment.CurrentDirectory}\\.songcache\\{songIndex}.zip";
                        yield return Utilities.DownloadFile(song["downloadUrl"], localPath);
                        yield return Utilities.ExtractZip(localPath, currentSongDirectory);
                        downloadCount++;
                    }

                    // Keep a history of all the songs we download- count it as downloaded even if the user already had it downloaded previously so if they delete it it doesn't get redownloaded
                    if (!_songDownloadHistory.Contains(songIndex))
                    {
                        _songDownloadHistory.Add(songIndex);

                        // Update our playlist with the latest song info
                        UpdatePlaylist(songIndex, songName);
                    }

                    // Delete any duplicate songs that we've downloaded
                    RemoveOldVersions(songIndex);

                    totalSongs++;

                    yield return null;
                }
            }

            // Write our download history to file
            Utilities.WriteStringListSafe(_historyPath, _songDownloadHistory.Distinct().ToList());

            // Write to the SyncSaber playlist
            _syncSaberSongs.WritePlaylist();

            Utilities.EmptyDirectory(".songcache");

            Plugin.Log($"Downloaded {downloadCount.ToString()} songs from mapper \"{author}\" in {((DateTime.Now - startTime).Seconds.ToString())} seconds. Skipped {(totalSongs - downloadCount).ToString()} songs.");
            _downloaderRunning = false;
        }

        private IEnumerator DownloadBeastSaberFeeds(string beastSaberUsername, int feedToDownload)
        {
            _downloaderRunning = true;
            var startTime = DateTime.Now;
            int downloadCount = 0;
            int totalSongs = 0;
            int pageIndex = 1;

            while (true)
            {
                int totalSongsForPage = 0;
                int downloadCountForPage = 0;

                string notificationMessage = $"Checking page {pageIndex.ToString()} of {(feedToDownload == 0 ? "followings feed" : "bookmarks feed")} from BeastSaber!";
                Plugin.Log(notificationMessage);
                DisplayNotification(notificationMessage);
                _downloaderRunning = true;
                using (UnityWebRequest www = UnityWebRequest.Get(feedToDownload == 0 ? $"https://bsaber.com/members/{beastSaberUsername}/wall/followings/feed/?acpage={pageIndex}" : $"https://bsaber.com/members/{beastSaberUsername}/bookmarks/feed/?acpage={pageIndex}"))
                {
                    yield return www.SendWebRequest();
                    if (www.isNetworkError || www.isHttpError)
                    {
                        Plugin.Log(www.error);
                        _downloaderRunning = false;
                        yield break;
                    }

                    string beastSaberFeed = www.downloadHandler.text;
                    XmlDocument doc = new XmlDocument();
                    doc.LoadXml(beastSaberFeed);

                    XmlNodeList nodes = doc.DocumentElement.SelectNodes("/rss/channel/item");
                    foreach (XmlNode n in nodes)
                    {
                        while (_isInGame)
                            yield return null;

                        string songName = String.Empty, downloadUrl = String.Empty;
                        try
                        {
                            songName = n.SelectNodes("SongTitle")[0].InnerText;
                            downloadUrl = n.SelectNodes("DownloadURL")[0].InnerText;
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        if (downloadUrl.Contains("dl.php"))
                        {
                            //Plugin.Log("Skipping BeastSaber download with old url format!");
                            totalSongs++;
                            totalSongsForPage++;
                            continue;
                        }

                        string songIndex = downloadUrl.Substring(downloadUrl.LastIndexOf('/') + 1);
                        string currentSongDirectory = $"{Environment.CurrentDirectory}\\CustomSongs\\{songIndex}";
                        if (Config.AutoDownloadSongs && !_songDownloadHistory.Contains(songIndex) && !Directory.Exists(currentSongDirectory))
                        {
                            Utilities.EmptyDirectory(".songcache", false);

                            DisplayNotification($"Downloading {songName}");

                            string localPath = $"{Environment.CurrentDirectory}\\.songcache\\{songIndex}.zip";
                            yield return Utilities.DownloadFile($"https://beatsaver.com/download/{songIndex}", localPath);
                            yield return Utilities.ExtractZip(localPath, currentSongDirectory);
                            downloadCount++;
                            downloadCountForPage++;
                        }

                        // Keep a history of all the songs we download- count it as downloaded even if the user already had it downloaded previously so if they delete it it doesn't get redownloaded
                        if (!_songDownloadHistory.Contains(songIndex))
                        {
                            _songDownloadHistory.Add(songIndex);

                            // Update our playlist with the latest song info
                            UpdatePlaylist(songIndex, songName);
                        }

                        // Check for/remove any duplicate songs
                        RemoveOldVersions(songIndex);

                        totalSongs++;
                        totalSongsForPage++;

                        yield return null;
                    }

                    if (totalSongsForPage == 0)
                    {
                        Plugin.Log("Reached end of feed!");
                        break;
                    }
                }

                // Write our download history to file
                Utilities.WriteStringListSafe(_historyPath, _songDownloadHistory.Distinct().ToList());

                // Write to the SynCSaber playlist
                _syncSaberSongs.WritePlaylist();

                Plugin.Log($"Reached end of page! Found {totalSongsForPage.ToString()} songs total, downloaded {downloadCountForPage.ToString()}!");
                pageIndex++;

                if (pageIndex > Config.MaxBeastSaberPages + 1 && Config.MaxBeastSaberPages != 0)
                    break;
            }
            Utilities.EmptyDirectory(".songcache");

            Plugin.Log($"Downloaded {downloadCount.ToString()} songs from BeatSaber {(feedToDownload == 0 ? "followings feed" : "bookmarks feed")} in {((DateTime.Now - startTime).Seconds.ToString())} seconds. Skipped {(totalSongs - downloadCount).ToString()} songs.");
            _downloaderRunning = false;
        }
    }
}
