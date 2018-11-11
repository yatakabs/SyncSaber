using IllusionPlugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncSaber
{
    class Config
    {
        public static bool AutoDownloadSongs;
        public static string BeastSaberUsername;
        public static void Write()
        {
            ModPrefs.SetBool("SyncSaber", "AutoDownloadSongs", AutoDownloadSongs);
            ModPrefs.SetString("SyncSaber", "BeastSaberUsername", BeastSaberUsername);
        }

        public static void Read()
        {
            AutoDownloadSongs = ModPrefs.GetBool("SyncSaber", "AutoDownloadSongs", true);
            BeastSaberUsername = ModPrefs.GetString("SyncSaber", "BeastSaberUsername", "");
        }
    }
}
