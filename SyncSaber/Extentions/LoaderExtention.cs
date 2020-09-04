using SongCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                Loader.CustomLevels.Remove(path);
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
    }
}
