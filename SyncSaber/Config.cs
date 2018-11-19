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
        public static bool AutoUpdateSongs;
        public static string BeastSaberUsername;
        public static int MaxBeastSaberPages;
        public static bool DeleteOldVersions;

        public static void Write()
        {
            ModPrefs.SetBool("SyncSaber", "AutoDownloadSongs", AutoDownloadSongs);
            ModPrefs.SetBool("SyncSaber", "AutoUpdateSongs", AutoUpdateSongs);
            ModPrefs.SetString("SyncSaber", "BeastSaberUsername", BeastSaberUsername);
            ModPrefs.SetInt("SyncSaber", "MaxBeastSaberPages", MaxBeastSaberPages);
            ModPrefs.SetBool("SyncSaber", "DeleteOldVersions", DeleteOldVersions);
        }

        public static void Read()
        {
            AutoDownloadSongs = ModPrefs.GetBool("SyncSaber", "AutoDownloadSongs", true);
            AutoUpdateSongs = ModPrefs.GetBool("SyncSaber", "AutoUpdateSongs", true);
            BeastSaberUsername = ModPrefs.GetString("SyncSaber", "BeastSaberUsername", "");
            MaxBeastSaberPages = ModPrefs.GetInt("SyncSaber", "MaxBeastSaberPages", 0);
            DeleteOldVersions = ModPrefs.GetBool("SyncSaber", "DeleteOldVersions", true);
        }
    }
}
