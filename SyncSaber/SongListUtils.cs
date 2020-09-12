using BeatSaverDownloader.UI;
using BS_Utils.Utilities;
using HMUI;
using IPA.Utilities;
using PlaylistLoaderLite.HarmonyPatches;
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
        public static IEnumerator RefreshSongs(bool fullRefresh = false)
        {
            yield return new WaitWhile(() => Plugin.instance?.IsInGame == false && !Loader.AreSongsLoaded && Loader.AreSongsLoading);
            Loader.Instance.RefreshSongs(fullRefresh);
            yield return new WaitWhile(() => Plugin.instance?.IsInGame == false && Loader.AreSongsLoading);
            try {
                PlaylistCollectionOverride.refreshPlaylists();
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            yield return new WaitWhile(() => Loader.AreSongsLoading);
        }
    }
}
