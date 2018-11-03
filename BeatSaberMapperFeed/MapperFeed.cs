using SongLoaderPlugin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UnityEngine;
using UnityEngine.Networking;

namespace BeatSaberMapperFeed {
    class MapperFeed : MonoBehaviour {
        private bool _downloaderRunning = false;
        private Stack<string> _authorDownloadQueue = new Stack<string>();
        private List<string> _songDownloadHistory = new List<string>();
        private Playlist _mapperFeedSongs = new Playlist("MapperFeed Playlist", "brian91292", "1");

        private string _historyPath = null;
        private int _beatSaberFeedToDownload = 0;

        void Awake() {
            UnityEngine.Object.DontDestroyOnLoad(this.gameObject);

            _historyPath = $"{Environment.CurrentDirectory}\\UserData\\MapperFeedHistory.txt";
            if (File.Exists(_historyPath + ".bak"))
            {
                // Something went wrong when the history file was being written previously, restore it from backup
                if (File.Exists(_historyPath))
                {
                    File.Delete(_historyPath);
                }
                File.Move(_historyPath + ".bak", _historyPath);
            }

            if (File.Exists(_historyPath))
            {
                _songDownloadHistory = File.ReadAllLines(_historyPath).ToList();
            }

            string favoriteMappersPath = $"{Environment.CurrentDirectory}\\UserData\\FavoriteMappers.ini";
            if (!File.Exists(favoriteMappersPath)) {
                File.WriteAllLines(favoriteMappersPath, new string[] { "freeek", "purphoros", "bennydabeast", "rustic", "greatyazer" });
            }
            
            foreach (string mapper in File.ReadAllLines(favoriteMappersPath)) {
                Plugin.Log($"Mapper: {mapper}");
                _authorDownloadQueue.Push(mapper);
            }

            if (!_mapperFeedSongs.ReadPlaylist())
            {
                _mapperFeedSongs.WritePlaylist();
            }

            if (!Directory.Exists("CustomSongs"))
            {
                Directory.CreateDirectory("CustomSongs");
            }
        }

        void Update() {
            if (!_downloaderRunning)
            {
                if (Config.BeastSaberUsername != "" && _beatSaberFeedToDownload < 2)
                {
                    StartCoroutine(DownloadBeastSaberFeed(Config.BeastSaberUsername, _beatSaberFeedToDownload));
                    _beatSaberFeedToDownload++;

                }
                else if (_authorDownloadQueue.Count > 0)
                {
                    StartCoroutine(DownloadAllSongsByAuthor(_authorDownloadQueue.Pop()));
                }
                else if (_authorDownloadQueue.Count == 0)
                {
                    SongLoader.Instance.RefreshSongs(false);
                    Plugin.Log("Finished updating songs from all mappers!");
                    Destroy(this.gameObject);
                }
            }
        }

        void EmptySongCache(bool delete = true)
        {
            if (Directory.Exists(".songcache"))
            {
                Utilities.EmptyDirectory(new DirectoryInfo(".songcache"));
                if (delete)
                {
                    Directory.Delete(".songcache");
                }
            }
        }

        string GetAuthorID(string author, byte[] rawData) {
            string searchResult = System.Text.Encoding.Default.GetString(rawData);
            string search = $">{author.ToLower()}<";

            while (true) {
                int index = searchResult.IndexOf(search);
                if (index != -1) {
                    string tmp = searchResult.Substring(0, index - 1);
                    return tmp.Substring(tmp.LastIndexOf('/') + 1);
                }
                else {
                    break;
                }
            }
            return String.Empty;
        }


        IEnumerator DownloadAllSongsByAuthor(string author) {
            _downloaderRunning = true;
            using (UnityWebRequest www = UnityWebRequest.Get($"https://beatsaver.com/search/all/{author}")) {
                yield return www.SendWebRequest();
                if (www.isNetworkError || www.isHttpError) {
                    Plugin.Log(www.error);
                }
                else {
                    byte[] data = www.downloadHandler.data;
                    string authorID = GetAuthorID(author, data);
                    if (authorID != String.Empty) {
                        yield return DownloadSongs(author, authorID);
                    }
                }
            }
            _downloaderRunning = false;
        }

        void UpdatePlaylist(string songIndex, string songName)
        {
            // Update our playlist with the new song if it doesn't exist, or replace the old song id/name with the updated info if it does
            bool playlistSongFound = false;
            foreach (PlaylistSong s in _mapperFeedSongs.Songs)
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
                _mapperFeedSongs.Add(songIndex, songName);
                Plugin.Log($"Added new playlist song \"{songName}\" with BeatSaver index {songIndex}");
            }
        }

        void RemoveOldVersions(string songIndex)
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
                        Utilities.EmptyDirectory(new DirectoryInfo(directoryToRemove));
                        Directory.Delete(directoryToRemove);

                        Plugin.Log($"Deleting old song with identifier \"{oldVersion}\" (current version: {currentVersion})");
                    }
                    catch (Exception e)
                    {

                    }
                }
                else if (!hasDash && directoryName == id)
                {
                    Utilities.EmptyDirectory(new DirectoryInfo(directory));
                    Directory.Delete(directory);
                    Plugin.Log($"Deleting old song with identifier \"{directoryName}\" (current version: {id}-{version})");
                }
            }
        }

        IEnumerator DownloadSongs(string author, string authorID) {
            var startTime = DateTime.Now;
            int downloadCount = 0;
            int totalSongs = 0;
            int currentSongIndex = 0;

            Plugin.Log($"Checking for new releases and updates from \"{author}\"");


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
                                            songName = searchResult.Substring(songNameIndex, searchResult.IndexOf("</td>") - songNameIndex);
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
                                EmptySongCache(false);

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
            Utilities.WriteStringListSafe(_historyPath, _songDownloadHistory);

            // Write to the MapperFeed playlist
            _mapperFeedSongs.WritePlaylist();

            EmptySongCache();

            Plugin.Log($"Downloaded {downloadCount.ToString()} songs from mapper \"{author}\" in {((DateTime.Now - startTime).Seconds.ToString())} seconds. Skipped {(totalSongs-downloadCount).ToString()} songs.");
        }

        IEnumerator DownloadBeastSaberFeed(string beastSaberUsername, int feedToDownload)
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
                        string beastSaberFeed = System.Text.Encoding.Default.GetString(www.downloadHandler.data);
                        string searchStart = "<DownloadURL>", searchEnd = "</DownloadURL>";
                        while (true)
                        {
                            while (Plugin.IsInGame) yield return null;

                            // Find the DownloadURL tags
                            int startIndex = beastSaberFeed.IndexOf(searchStart);
                            if (startIndex == -1) break;
                            beastSaberFeed = beastSaberFeed.Substring(startIndex + searchStart.Length);
                            int endIndex = beastSaberFeed.IndexOf(searchEnd);
                            if (endIndex == -1) break;


                            string beatSaverUrl = beastSaberFeed.Substring(0, endIndex);
                            if (beatSaverUrl.StartsWith("dl.php"))
                            {
                                //Plugin.Log("Skipping BeastSaber download with old url format!");
                                totalSongs++;
                                totalSongsForPage++;
                                continue;
                            }

                            string songIndex = beatSaverUrl.Substring(beatSaverUrl.LastIndexOf('/') + 1);
                            string currentSongDirectory = $"{Environment.CurrentDirectory}\\CustomSongs\\{songIndex}";

                            if (Config.AutoDownloadSongs && !_songDownloadHistory.Contains(songIndex) && !Directory.Exists(currentSongDirectory))
                            {
                                EmptySongCache(false);

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

                                // TODO: Reimplement after beast saber makes it easier to access song name
                                //// Update our playlist with the latest song info
                                //UpdatePlaylist(songIndex, "BeastSaberFeed");
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
                Utilities.WriteStringListSafe(_historyPath, _songDownloadHistory);

                // TODO: Reimplement after beast saber makes it easier to access song name
                //// Write to the MapperFeed playlist
                //_mapperFeedSongs.WritePlaylist();

                Plugin.Log($"Reached end of page! Found {totalSongsForPage.ToString()} songs total, downloaded {downloadCountForPage.ToString()}!");
                pageIndex++;
            }
            EmptySongCache();

            Plugin.Log($"Downloaded {downloadCount.ToString()} songs from BeatSaber {(feedToDownload == 0 ? "followings feed" : "bookmarks feed")} in {((DateTime.Now - startTime).Seconds.ToString())} seconds. Skipped {(totalSongs - downloadCount).ToString()} songs.");
            _downloaderRunning = false;
        }
    }
}
