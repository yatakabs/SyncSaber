using SongCore;
using System.Collections;
using UnityEngine;

namespace SyncSaber
{
    class SongListUtils
    {
        public static IEnumerator RefreshSongs(bool fullRefresh = false)
        {
            yield return new WaitWhile(() => !Loader.AreSongsLoaded || Loader.AreSongsLoading || Plugin.instance.IsInGame);
            Loader.Instance.RefreshSongs(fullRefresh);
            yield return new WaitForSeconds(1f);
            yield return new WaitWhile(() => !Loader.AreSongsLoaded || Loader.AreSongsLoading || Plugin.instance.IsInGame);
        }
    }
}
