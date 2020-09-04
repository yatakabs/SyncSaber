using BeatSaverDownloader.UI;
using HMUI;
using IPA.Utilities;
using SongBrowser;
using SongBrowser.DataAccess;
using SongCore;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SyncSaber
{
    class SongListUtils
    {
        private static LevelCollectionViewController _standardLevelListViewController = null;
        private static LevelPackDetailViewController _standardLevelDetailViewController = null;
        private static bool _initialized = false;
        private static bool _songBrowserInstalled = false;
        private static bool _songDownloaderInstalled = false;

        //private static List<IBeatmapLevel> CurrentLevels
        //{
        //    get
        //    {
        //        return ReflectionUtil.GetPrivateField<IBeatmapLevel[]>(_standardLevelListViewController, "_levels").ToList();
        //    }
        //    set
        //    {
        //        _standardLevelListViewController.SetLevels(value.ToArray());
        //    }
        //}

        public static void Initialize()
        {
            _standardLevelListViewController = Resources.FindObjectsOfTypeAll<LevelCollectionViewController>().FirstOrDefault();
            _standardLevelDetailViewController = Resources.FindObjectsOfTypeAll<LevelPackDetailViewController>().FirstOrDefault();

            if (!_initialized)
            {
                try
                {
                    _songBrowserInstalled = Utilities.IsModInstalled("Song Browser");
                    _songDownloaderInstalled = Utilities.IsModInstalled("BeatSaver Downloader");
                    _initialized = true;
                }
                catch (Exception e)
                {
                    Logger.Info($"Exception {e}");
                }
            }
        }

        private enum SongBrowserAction { ResetFilter = 1 }
        private static void ExecuteSongBrowserAction(SongBrowserAction action)
        {
            var _songBrowserUI = SongBrowserApplication.Instance.GetPrivateField<SongBrowser.UI.SongBrowserUI>("_songBrowserUI");
            if (_songBrowserUI)
            {
                if (action.HasFlag(SongBrowserAction.ResetFilter))
                {
                    _songBrowserUI.Model.Settings.filterMode = SongFilterMode.None;
                }
            }
        }

        private enum SongDownloaderAction { ResetFilter = 1 }
        //private static void ExecuteSongDownloaderAction(SongDownloaderAction action)
        //{
        //    if (action.HasFlag(SongDownloaderAction.ResetFilter))
        //    {
        //        SongListTweaks.Instance.SetLevels(SortMode.Newest, "");
        //    }
        //}

        //public static void RemoveDuplicates()
        //{
        //   _standardLevelListViewController.SetLevels(CurrentLevels.Distinct().ToArray());
        //}

        //public static IEnumerator RetrieveNewSong(string songFolderName, bool resetFilterMode = false)
        //{
        //    if (!Loader.AreSongsLoaded) yield break;

        //    if (!_standardLevelListViewController) yield break;
            
        //    Loader.Instance.RetrieveNewSong(songFolderName);
            
        //    // If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
        //    if (resetFilterMode)
        //    {
        //        // If song browser is installed, update/refresh it
        //        if (_songBrowserInstalled)
        //            ExecuteSongBrowserAction(SongBrowserAction.ResetFilter);
        //        // If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
        //        else if (_songDownloaderInstalled)
        //            ExecuteSongDownloaderAction(SongDownloaderAction.ResetFilter);
        //    }

        //    //// Set the row index to the previously selected song
        //    //if (selectOldLevel)
        //    //    ScrollToLevel(selectedLevelId);
        //}

        public static IEnumerator RefreshSongs(bool fullRefresh = false, bool selectOldLevel = true)
        {
            if (!Loader.AreSongsLoaded) yield break;
            if (!_standardLevelListViewController) yield break;

            // // Grab the currently selected level id so we can restore it after refreshing
            // string selectedLevelId = _standardLevelListViewController.selectedLevel?.levelID;

            // Wait until song loader is finished loading, then refresh the song list
            while (Loader.AreSongsLoading) yield return null;
            Loader.Instance.RefreshSongs(fullRefresh);
            while (Loader.AreSongsLoading) yield return null;
            

            //// Set the row index to the previously selected song
            //if (selectOldLevel)
            //    ScrollToLevel(selectedLevelId);
        }

        public static void SelectCustomSongPack(bool resetFilters = true)
        {
            var LevelCollectionTableView = Resources.FindObjectsOfTypeAll<LevelCollectionTableView>().First();
            var tableView = LevelCollectionTableView.GetPrivateField<TableView>("_tableView");
            
            var packsCollection = LevelCollectionTableView.GetPrivateField<IBeatmapLevelPackCollection>("_levelPackCollection");
            int customSongPackIndex = -1;
            for(int i=0; i< packsCollection.beatmapLevelPacks.Length; i++)
                if(packsCollection.beatmapLevelPacks[i].packName == "Custom Maps")
                    customSongPackIndex = i;

            if (customSongPackIndex != -1 && LevelCollectionTableView.GetPrivateField<int>("_selectedColumn") != customSongPackIndex)
            {
                tableView.SelectCellWithIdx(customSongPackIndex, true);
                tableView.ScrollToCellWithIdx(0, TableViewScroller.ScrollPositionType.Beginning, false);
                //for (int i = 0; i < Mathf.FloorToInt(customSongPackIndex / 4); i++)
                //    tableView.PageScrollDown();
            }

            // If song browser is installed, update/refresh it
            //if (_songBrowserInstalled)
            //    ExecuteSongBrowserAction(SongBrowserAction.ResetFilter);
            //// If beatsaver downloader is installed and songbrowser isnt, then we need to change the filter mode through it
            //else if (_songDownloaderInstalled)
            //    ExecuteSongDownloaderAction(SongDownloaderAction.ResetFilter);
        }
        
        //public static int GetLevelIndex(LevelCollectionViewController table, string levelID)
        //{
        //    for (int i = 0; i < table.levelPack.beatmapLevelCollection.beatmapLevels.Length; i++)
        //    {
        //        if (table.levelPack.beatmapLevelCollection.beatmapLevels[i].levelID == levelID)
        //        {
        //            return i + 1;
        //        }
        //    }
        //    return -1;
        //}
        
        public static IEnumerator ScrollToLevel(string levelID, Action<bool> callback, bool animated, bool isRetry = false)
        {
            if (_standardLevelListViewController)
            {
                Logger.Info($"Scrolling to {levelID}! Retry={isRetry}");

                // handle if song browser is present
                if (Plugin.SongBrowserPluginPresent) {
                    Plugin.SongBrowserCancelFilter();
                }

                // Make sure our custom songpack is selected
                SelectCustomSongPack();

                yield return null;

                int songIndex = 0;

                // get the table view
                var levelsTableView = _standardLevelListViewController.GetField<LevelCollectionTableView, LevelCollectionViewController>("_levelCollectionTableView");

                //RequestBot.Instance.QueueChatMessage($"selecting song: {levelID} pack: {packIndex}");
                yield return null;

                // get the table view
                var tableView = levelsTableView.GetField<TableView, LevelCollectionTableView>("_tableView");

                // get list of beatmaps, this is pre-sorted, etc
                var beatmaps = levelsTableView.GetField<IPreviewBeatmapLevel[], LevelCollectionTableView>("_previewBeatmapLevels").ToList();

                // get the row number for the song we want
                songIndex = beatmaps.FindIndex(x => (x.levelID.Split('_')[2] == levelID));

                // bail if song is not found, shouldn't happen
                if (songIndex >= 0) {
                    // if header is being shown, increment row
                    if (levelsTableView.GetField<bool, LevelCollectionTableView>("_showLevelPackHeader")) {
                        songIndex++;
                    }

                    Logger.Info($"Selecting row {songIndex}");

                    // scroll to song
                    tableView.ScrollToCellWithIdx(songIndex, TableViewScroller.ScrollPositionType.Beginning, animated);

                    // select song, and fire the event
                    tableView.SelectCellWithIdx(songIndex, true);

                    Logger.Info("Selected song with index " + songIndex);
                    callback?.Invoke(true);

                    try {
                        // disable no fail gamepaly modifier
                        var gameplayModifiersPanelController = Resources.FindObjectsOfTypeAll<GameplayModifiersPanelController>().First();
                        gameplayModifiersPanelController.gameplayModifiers.noFail = false;

                        //gameplayModifiersPanelController.gameplayModifiers.ResetToDefault();

                        gameplayModifiersPanelController.Refresh();
                    }
                    catch (Exception e){
                        Logger.Error(e);
                    }

                    yield break;
                }
            }

            if (!isRetry)
            {
                yield return SongListUtils.RefreshSongs(false, false);
                yield return ScrollToLevel(levelID, callback, animated, true);
                yield break;
            }

            var tempLevels = Loader.CustomLevels.Where(l => l.Value.levelID == levelID).ToArray();
            foreach (var l in tempLevels)
                Loader.CustomLevels.Remove(l.Key);

            Logger.Info($"Failed to scroll to {levelID}!");
            callback?.Invoke(false);
        }
    }
}
