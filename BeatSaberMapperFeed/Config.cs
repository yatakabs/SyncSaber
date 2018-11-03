using IllusionPlugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BeatSaberMapperFeed
{
    class Config
    {
        public static bool AutoDownloadSongs;
        public static string BeastSaberUsername;
        public static void Write()
        {
            ModPrefs.SetBool("MapperFeed", "AutoDownloadSongs", AutoDownloadSongs);
            ModPrefs.SetString("MapperFeed", "BeastSaberUsername", BeastSaberUsername);
        }

        public static void Read()
        {
            AutoDownloadSongs = ModPrefs.GetBool("MapperFeed", "AutoDownloadSongs", true);
            BeastSaberUsername = ModPrefs.GetString("MapperFeed", "BeastSaberUsername", "");
        }
    }
}
