using SongCore;
using System;
using System.Linq;

namespace SyncSaber.Extentions
{
    public static class LoaderExtention
    {
        public static void RemoveSongWithLevelID(this Loader loader, string levelID)
        {
            var path = Loader.CustomLevels.FirstOrDefault(x => x.Value.levelID.ToUpper() == levelID.ToUpper()).Key;
            if (path == null) {
                return;
            }
            try {
                loader.DeleteSong(path);
                Loader.CustomLevels.TryRemove(path, out _);
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
    }
}
