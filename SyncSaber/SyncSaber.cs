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

namespace SyncSaber {
    class SyncSaber : MonoBehaviour {
        private readonly Regex _digitRegex = new Regex("^[0-9]+$", RegexOptions.Compiled);

        private bool _downloaderRunning = false;
        private bool _downloaderComplete = false;
        private bool _isRefreshing = false;
        private string _historyPath = null;
        private int _beatSaberFeedToDownload = 0;

        private Stack<string> _authorDownloadQueue = new Stack<string>();
        private ConcurrentStack<string> _updateQueue = new ConcurrentStack<string>();
        private ConcurrentDictionary<string, string> _updateInformation = new ConcurrentDictionary<string, string>();
        private List<string> _songDownloadHistory = new List<string>();
        private Playlist _syncSaberSongs = new Playlist("SyncSaber Playlist", "brian91292", "1");



        private IStandardLevel _lastLevel;
        private StandardLevelSelectionFlowCoordinator _standardLevelSelectionFlowCoordinator;
        private StandardLevelListViewController _standardLevelListViewController;

        private TMP_Text _notificationText;
        private DateTime _uiResetTime;

        private void Awake() {
            UnityEngine.Object.DontDestroyOnLoad(this.gameObject);

            SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;

            _historyPath = $"{Environment.CurrentDirectory}\\UserData\\SyncSaberHistory.txt";
            var _oldHistoryPath = $"{Environment.CurrentDirectory}\\UserData\\MapperFeedHistory.txt";
            try { if (File.Exists(_oldHistoryPath)) File.Move(_oldHistoryPath, _historyPath); }
            catch (Exception) { File.Delete(_oldHistoryPath); }

            try { if (File.Exists(_oldHistoryPath + ".bak")) File.Move(_oldHistoryPath + ".bak", _historyPath); }
            catch (Exception) { File.Delete(_oldHistoryPath + ".bak"); }

            if (File.Exists(_historyPath + ".bak"))
            {
                // Something went wrong when the history file was being written previously, restore it from backup
                if (File.Exists(_historyPath)) File.Delete(_historyPath); 
                File.Move(_historyPath + ".bak", _historyPath);
            }
            if (File.Exists(_historyPath))
            {
                _songDownloadHistory = File.ReadAllLines(_historyPath).ToList();
            }
            if (!Directory.Exists("CustomSongs")) Directory.CreateDirectory("CustomSongs");
            
            string favoriteMappersPath = $"{Environment.CurrentDirectory}\\UserData\\FavoriteMappers.ini";
            if (!File.Exists(favoriteMappersPath)) {
                File.WriteAllLines(favoriteMappersPath, new string[] { "" }); // "freeek", "purphoros", "bennydabeast", "rustic", "greatyazer"
            }
            
            foreach (string mapper in File.ReadAllLines(favoriteMappersPath)) {
                Plugin.Log($"Mapper: {mapper}");
                _authorDownloadQueue.Push(mapper);
            }

            if (!_syncSaberSongs.ReadPlaylist())
            {
                _syncSaberSongs.WritePlaylist();
            }

            _notificationText = Utilities.CreateNotificationText(String.Empty);
            DisplayNotification("SyncSaber Initialized!");
        }

        private void DisplayNotification(string text)
        {
            _uiResetTime = DateTime.Now.AddSeconds(5);
            _notificationText.text = "SyncSaber- " + text;
        }

        private void Update() {
            if (!_downloaderRunning)
            {
                if (_updateQueue.Count > 0)
                {
                    if (_updateQueue.TryPop(out var songId))
                    {
                        Plugin.Log($"Updating {songId}");
                        StartCoroutine(UpdateSong(songId));
                    }
                }
                else if (Config.BeastSaberUsername != "" && _beatSaberFeedToDownload < 2)
                {
                    StartCoroutine(DownloadBeastSaberFeed(Config.BeastSaberUsername, _beatSaberFeedToDownload));
                    _beatSaberFeedToDownload++;
                    Plugin.Log("Downloading beastsaber feed!");

                }
                else if (_authorDownloadQueue.Count > 0)
                {
                    StartCoroutine(DownloadAllSongsByAuthor(_authorDownloadQueue.Pop()));
                    Plugin.Log("Downloading songs by author!");
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
            {
                _notificationText.text = String.Empty;
            }
        }
        
        private void SceneManagerOnActiveSceneChanged(Scene arg0, Scene scene)
        {
            if (scene.name != "Menu") return;

            _standardLevelSelectionFlowCoordinator = Resources.FindObjectsOfTypeAll<StandardLevelSelectionFlowCoordinator>().First();
            if (!_standardLevelSelectionFlowCoordinator) return;

            _standardLevelListViewController = ReflectionUtil.GetPrivateField<StandardLevelListViewController>(_standardLevelSelectionFlowCoordinator, "_levelListViewController");
            if (!_standardLevelListViewController) return;

            _standardLevelListViewController.didSelectLevelEvent += PluginUI_didSelectSongEvent;
        }

        private void PluginUI_didSelectSongEvent(StandardLevelListViewController sender, IStandardLevel level)
        {
            if (level != _lastLevel && level is CustomLevel)
            {
                _lastLevel = level;
                StartCoroutine(CheckIfLevelNeedsUpdate(level.levelID));
            }
        }

        private string GetAuthorID(string author, string data) {
            string search = $">{author.ToLower()}<";

            while (true) {
                int index = data.IndexOf(search);
                if (index != -1) {
                    string tmp = data.Substring(0, index - 1);
                    return tmp.Substring(tmp.LastIndexOf('/') + 1);
                }
                else {
                    break;
                }
            }
            return String.Empty;
        }

        private void UpdateSongBrowser()
        {
            var _songBrowserUI = SongBrowserApplication.Instance.GetPrivateField<SongBrowserPlugin.UI.SongBrowserUI>("_songBrowserUI");
            if (_songBrowserUI)
            {
                _songBrowserUI.UpdateSongList();
                _songBrowserUI.RefreshSongList();
            }
        }

        private IEnumerator RefreshSongs(bool fullRefresh = false)
        {
            if (_isRefreshing) yield break;
            _isRefreshing = true;

            // Grab the currently selected level id so we can restore it after refreshing
            string selectedLevelId = _standardLevelListViewController.selectedLevel.levelID;
            Plugin.Log($"Grabbing update for song {selectedLevelId}");

            // Wait until song loader is finished loading, then refresh the song list
            while (SongLoader.AreSongsLoading) yield return null;
            SongLoader.Instance.RefreshSongs(fullRefresh);
            while (SongLoader.AreSongsLoading) yield return null;

            var table = ReflectionUtil.GetPrivateField<StandardLevelListTableView>(_standardLevelListViewController, "_levelListTableView");
            if (table)
            {
                // If song browser is installed, update/refresh it
                if (Utilities.IsModInstalled("Song Browser"))
                {
                    UpdateSongBrowser();
                }

                // Set the row index to the previously selected song
                int row = table.RowNumberForLevelID(selectedLevelId);
                TableView tableView = table.GetComponentInChildren<TableView>();
                tableView.SelectRow(row, true);
                tableView.ScrollToRow(row, true);
            }
            _isRefreshing = false;
        }

        private IEnumerator UpdateSong(string songIndex)
        {
            Utilities.EmptyDirectory(".songcache", false);
            
            // Download and extract the update
            string localPath = $"{Environment.CurrentDirectory}\\.songcache\\{songIndex}.zip";
            yield return Utilities.DownloadFile($"https://beatsaver.com/download/{songIndex}", localPath);
            yield return Utilities.ExtractZip(localPath, $"{Environment.CurrentDirectory}\\CustomSongs\\{songIndex}");

            // Only delete the old song after the new one is downloaded and extracted
            string levelPath = _updateInformation[songIndex];
            Directory.Delete(levelPath, true);

            // Disable our didSelectLevel event, then refresh the song list
            _standardLevelListViewController.didSelectLevelEvent -= PluginUI_didSelectSongEvent;
            yield return RefreshSongs();
            _standardLevelListViewController.didSelectLevelEvent += PluginUI_didSelectSongEvent;

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

        private void OnGotLatestVersionRetrieved(string levelId, string levelPath, string oldBeatSaverId, string newBeatSaverId)
        {
            Plugin.Log($"OldBeatSaverId: {oldBeatSaverId}, NewBeatSaverId: {newBeatSaverId}");

            if (oldBeatSaverId != newBeatSaverId)
            {
                DisplayNotification($"Updating song {newBeatSaverId}");
                
                _updateQueue.Push(newBeatSaverId);
                _updateInformation[newBeatSaverId] = levelPath;

                Plugin.Log($"Queued download for latest version of {levelId}");
            }
        }

        IEnumerator GetLatestVersion(string levelId, string levelPath, string songId, string oldBeatSaverId, Action<string, string, string, string> callback)
        {
            Plugin.Log($"Getting latest version for songId {songId}");

            string search = ">Version: ";
            using (UnityWebRequest www = UnityWebRequest.Get($"https://www.beatsaver.com/browse/detail/{songId}"))
            {
                yield return www.SendWebRequest();
                if (www.isNetworkError || www.isHttpError)
                {
                    Plugin.Log(www.error);
                }
                else
                {
                    string result = www.downloadHandler.text;
                    int index = result.IndexOf(search);
                    if (index != -1)
                    {
                        index += search.Length;
                        result = result.Substring(index);
                        string latestSongId = result.Substring(0, result.IndexOf("<"));

                        if (latestSongId.Contains("-"))
                        {
                            if (callback != null) callback(levelId, levelPath, oldBeatSaverId, latestSongId);
                        }
                    }
                }
            }
        }

        private IEnumerator CheckIfLevelNeedsUpdate(string levelId)
        {
            Plugin.Log($"Selected level {levelId}");
            IStandardLevel[] _levelsForGamemode = ReflectionUtil.GetPrivateField<IStandardLevel[]>(ReflectionUtil.GetPrivateField<StandardLevelListViewController>(_standardLevelSelectionFlowCoordinator, "_levelListViewController"), "_levels");

            if (levelId.Length > 32 && _levelsForGamemode.Any(x => x.levelID == levelId))
            {
                int currentSongIndex = _levelsForGamemode.ToList().FindIndex(x => x.levelID == levelId);
                currentSongIndex += (currentSongIndex == 0) ? 1 : -1;

                bool zippedSong = false;
                string _songPath = SongLoader.CustomLevels.First(x => x.levelID == levelId).customSongInfo.path;

                if (!string.IsNullOrEmpty(_songPath) && _songPath.Contains("/.cache/")) zippedSong = true;

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

                if (!zippedSong) {
                    Plugin.Log($"Attempting to get beatsaver id from path \"{_songPath}\"");

                    string directoryName = Path.GetFileName(_songPath);

                retry:
                    string id = String.Empty;
                    string version = String.Empty;
                    bool hasVersion = false;

                    // If the folder name is a full beatsaver id, including version
                    if (directoryName.Contains("-")) {
                        string[] parts = directoryName.Split(new char[] { '-' }, 2);
                        id = parts[0];
                        version = parts[1];

                        // Make sure the id and version are only digits
                        if (_digitRegex.IsMatch(id) && _digitRegex.IsMatch(version))
                        {
                            hasVersion = true;
                        }
                        // If not, this isn't a valid beatsaver id
                        else
                        {
                            Plugin.Log($"Directory \"{directoryName}\" is not a valid beatsaver id");
                            yield break;
                        }
                    }
                    else {
                        // If the folder contains only digits, assume its a beatsaver id
                        if (_digitRegex.IsMatch(directoryName))
                        {
                            id = directoryName;
                        }
                        // Try checking one more level up
                        else
                        {
                            directoryName = Path.GetFileName(Path.GetDirectoryName(_songPath));
                            if (directoryName != "CustomSongs")
                            {
                                Plugin.Log("Checking one more level up!");
                                goto retry;
                            }
                            Plugin.Log($"Couldn't locate valid BeatSaver ID for song at path \"{_songPath}\"");
                            yield break;
                        }
                    }

                    yield return GetLatestVersion(levelId, _songPath, id, directoryName, OnGotLatestVersionRetrieved);
                }
                else {
                    // TODO: IMPLEMENT HANDLING FOR ZIPPED SONG UPDATING
                }
            }
            else
            {
                yield return null;
            }

        }

        private void RemoveOldVersions(string songIndex)
        {
            string[] customSongDirectories = Directory.GetDirectories($"{Environment.CurrentDirectory}\\CustomSongs");
            string id = songIndex.Substring(0, songIndex.IndexOf("-"));
            string version = songIndex.Substring(songIndex.IndexOf("-") + 1);

            foreach (string directory in customSongDirectories)
            {
                string directoryName = directory.Substring(directory.LastIndexOf('\\') + 1);
                bool hasDash = directoryName.Contains("-");

                if (hasDash && directoryName.StartsWith($"{id}-") && directoryName != songIndex)
                {
                    string directoryVersion = directoryName.Substring(directoryName.IndexOf("-") + 1);

                    try
                    {
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
                    catch (Exception e)
                    {

                    }
                }
                else if (!hasDash && directoryName == id)
                {
                    Directory.Delete(directory, true);
                    Plugin.Log($"Deleting old song with identifier \"{directoryName}\" (current version: {id}-{version})");
                }
            }
        }

        private IEnumerator DownloadAllSongsByAuthor(string author)
        {
            _downloaderRunning = true;
            using (UnityWebRequest www = UnityWebRequest.Get($"https://beatsaver.com/index.php/search/all?key={author}"))
            {
                yield return www.SendWebRequest();
                if (www.isNetworkError || www.isHttpError)
                {
                    Plugin.Log(www.error);
                }
                else
                {
                    // Grab the current mappers beatsaver user id so we can use it to get a full listing of their songs
                    string authorID = GetAuthorID(author, www.downloadHandler.text);
                    if (authorID != String.Empty)
                    {
                        yield return DownloadSongs(author, authorID);
                    }
                    else Plugin.Log($"Couldn't find id for mapper {author}");
                }
            }
            _downloaderRunning = false;
        }

        private IEnumerator DownloadSongs(string author, string authorID) {
            var startTime = DateTime.Now;
            int downloadCount = 0;
            int totalSongs = 0;
            int currentSongIndex = 0;

            Plugin.Log($"Checking for new releases and updates from \"{author}\"");
            DisplayNotification($"Checking for new songs from \"{author}\"");


            while (true) {
                string url = $"https://beatsaver.com/browse/byuser/{authorID}/{currentSongIndex.ToString()}";
                using (UnityWebRequest www = UnityWebRequest.Get(url)) {
                    yield return www.SendWebRequest();
                    if (www.isNetworkError || www.isHttpError) {
                        Plugin.Log(www.error);
                    }
                    else {
                        string searchResult = www.downloadHandler.text;

                        string searchQuery = "/browse/detail/";
                        List<KeyValuePair<string, string>> songInfoList = new List<KeyValuePair<string, string>>();
                        while (true) {
                            try
                            {
                                int index = searchResult.IndexOf(searchQuery);
                                if (index != -1)
                                {
                                    // Grab the song index from the BeatSaver URL
                                    searchResult = searchResult.Substring(index + searchQuery.Length);
                                    string songIndex = searchResult.Substring(0, searchResult.IndexOf('"'));

                                    // Check if our song list already contains the current songIndex
                                    bool foundKey = false;
                                    songInfoList.ForEach(s =>
                                    {
                                        if (s.Key == songIndex) foundKey = true;
                                    });
                                    if (!foundKey)
                                    {
                                        // Add the newly found key/song name into our list of songs to be evaluated
                                        string songName = "Undefined";
                                        int songNameIndex = searchResult.IndexOf("Song: ");
                                        if (songNameIndex != -1)
                                        {
                                            songNameIndex += 6;
                                            songName = WebUtility.HtmlDecode(searchResult.Substring(songNameIndex, searchResult.IndexOf("</td>") - songNameIndex));
                                            //Plugin.Log($"SongName: {songName}");
                                        }
                                        songInfoList.Add(new KeyValuePair<string, string>(songIndex, songName));
                                    }
                                }
                                else
                                {
                                    break;
                                }
                            }
                            catch (Exception e)
                            {
                                Plugin.Log($"Exception when parsing songs {e.ToString()}");
                            }
                        }

                        // No need to evaluate this any further if our song list is empty
                        if (songInfoList.Count == 0) {
                            break;
                        }
                        
                        foreach (KeyValuePair<string, string> kvp in songInfoList) {
                            string songIndex = kvp.Key;
                            string songName = kvp.Value;

                            // Wait until we aren't in game to continue evaluating songs
                            while (Plugin.IsInGame) yield return null;
                            
                            // Attempt to download the current song
                            string currentSongDirectory = $"{Environment.CurrentDirectory}\\CustomSongs\\{songIndex}";
                            if (Config.AutoDownloadSongs && !_songDownloadHistory.Contains(songIndex) && !Directory.Exists(currentSongDirectory)) {
                                Utilities.EmptyDirectory(".songcache", false);

                                DisplayNotification($"Downloading {songName}");

                                string localPath = $"{Environment.CurrentDirectory}\\.songcache\\{songIndex}.zip";
                                yield return Utilities.DownloadFile($"https://beatsaver.com/download/{songIndex}", localPath);
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
                        }
                        currentSongIndex += 20;
                    }
                }
            }
            // Write our download history to file
            Utilities.WriteStringListSafe(_historyPath, _songDownloadHistory.Distinct().ToList());

            // Write to the SyncSaber playlist
            _syncSaberSongs.WritePlaylist();

            Utilities.EmptyDirectory(".songcache");

            Plugin.Log($"Downloaded {downloadCount.ToString()} songs from mapper \"{author}\" in {((DateTime.Now - startTime).Seconds.ToString())} seconds. Skipped {(totalSongs-downloadCount).ToString()} songs.");
        }

        private IEnumerator DownloadBeastSaberFeed(string beastSaberUsername, int feedToDownload)
        {
            var startTime = DateTime.Now;
            int downloadCount = 0;
            int totalSongs = 0;
            int pageIndex = 1;

            while (true)
            {
                int totalSongsForPage = 0;
                int downloadCountForPage = 0;

                Plugin.Log($"Downloading page {pageIndex.ToString()} of {(feedToDownload == 0 ? "followings feed" : "bookmarks feed")} from BeastSaber!");
                _downloaderRunning = true;
                using (UnityWebRequest www = UnityWebRequest.Get(feedToDownload == 0 ? $"https://bsaber.com/members/{beastSaberUsername}/wall/followings/feed/?acpage={pageIndex}" : $"https://bsaber.com/members/{beastSaberUsername}/bookmarks/feed/?acpage={pageIndex}"))
                {
                    yield return www.SendWebRequest();
                    if (www.isNetworkError || www.isHttpError)
                    {
                        Plugin.Log(www.error);
                    }
                    else
                    {
                        string beastSaberFeed = www.downloadHandler.text;

                        XmlDocument doc = new XmlDocument();
                        doc.LoadXml(beastSaberFeed);

                        XmlNodeList nodes = doc.DocumentElement.SelectNodes("/rss/channel/item");
                        foreach (XmlNode n in nodes)
                        {
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
                        }
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
            }
            Utilities.EmptyDirectory(".songcache");

            Plugin.Log($"Downloaded {downloadCount.ToString()} songs from BeatSaber {(feedToDownload == 0 ? "followings feed" : "bookmarks feed")} in {((DateTime.Now - startTime).Seconds.ToString())} seconds. Skipped {(totalSongs - downloadCount).ToString()} songs.");
            _downloaderRunning = false;
        }
    }
}
