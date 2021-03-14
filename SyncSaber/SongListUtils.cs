using SongCore;
using System.Collections;
using UnityEngine;

namespace SyncSaber
{
    internal class SongListUtils
    {
        public static IEnumerator RefreshSongs(bool fullRefresh = false)
        {
            yield return new WaitWhile(() => !Loader.AreSongsLoaded || Loader.AreSongsLoading || Plugin.Instance.IsInGame);
            Loader.Instance.RefreshSongs(fullRefresh);
        }
    }
}
