using BeatSaverDownloader.UI;
using BS_Utils.Utilities;
using HMUI;
using IPA.Loader;
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
            yield return new WaitWhile(() => !Loader.AreSongsLoaded || Loader.AreSongsLoading || Plugin.instance.IsInGame);
            new HMTask(
                async () =>
                {
                    Loader.Instance.RefreshSongs(fullRefresh);
                    await Task.Delay(1000);
                    while (!Loader.AreSongsLoaded || Loader.AreSongsLoading || Plugin.instance.IsInGame) {
                        await Task.Delay(1000);
                    }
                }).Run();
            yield return new WaitWhile(() => !Loader.AreSongsLoaded || Loader.AreSongsLoading || Plugin.instance.IsInGame);
        }
    }
}
