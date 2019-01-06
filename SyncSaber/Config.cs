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
        public static int MaxFollowingsPages;
        public static int MaxBookmarksPages;
        public static int MaxCuratorRecommendedPages;
        public static bool DeleteOldVersions;
        public static bool SyncBookmarksFeed;
        public static bool SyncCuratorRecommendedFeed;
        public static bool SyncFollowingsFeed;

        public static void Write()
        {
            ModPrefs.SetBool(Plugin.Instance.Name, "AutoDownloadSongs", AutoDownloadSongs);
            ModPrefs.SetBool(Plugin.Instance.Name, "AutoUpdateSongs", AutoUpdateSongs);
            ModPrefs.SetString(Plugin.Instance.Name, "BeastSaberUsername", BeastSaberUsername);
            ModPrefs.SetBool(Plugin.Instance.Name, "DeleteOldVersions", DeleteOldVersions);
            ModPrefs.SetBool(Plugin.Instance.Name, "SyncBookmarksFeed", SyncBookmarksFeed);
            ModPrefs.SetBool(Plugin.Instance.Name, "SyncCuratorRecommendedFeed", SyncCuratorRecommendedFeed);
            ModPrefs.SetBool(Plugin.Instance.Name, "SyncFollowingsFeed", SyncFollowingsFeed);
            ModPrefs.SetInt(Plugin.Instance.Name, "MaxBookmarksPages", MaxBookmarksPages);
            ModPrefs.SetInt(Plugin.Instance.Name, "MaxCuratorRecommendedPages", MaxCuratorRecommendedPages);
            ModPrefs.SetInt(Plugin.Instance.Name, "MaxFollowingsPages", MaxFollowingsPages);
        }

        public static void Read()
        {
            AutoDownloadSongs = ModPrefs.GetBool(Plugin.Instance.Name, "AutoDownloadSongs", true);
            AutoUpdateSongs = ModPrefs.GetBool(Plugin.Instance.Name, "AutoUpdateSongs", true);
            BeastSaberUsername = ModPrefs.GetString(Plugin.Instance.Name, "BeastSaberUsername", "");
            DeleteOldVersions = ModPrefs.GetBool(Plugin.Instance.Name, "DeleteOldVersions", true);
            SyncBookmarksFeed = ModPrefs.GetBool(Plugin.Instance.Name, "SyncBookmarksFeed", true);
            SyncCuratorRecommendedFeed = ModPrefs.GetBool(Plugin.Instance.Name, "SyncCuratorRecommendedFeed", false);
            SyncFollowingsFeed = ModPrefs.GetBool(Plugin.Instance.Name, "SyncFollowingsFeed", true);
            MaxFollowingsPages = ModPrefs.GetInt(Plugin.Instance.Name, "MaxFollowingsPages", 0);
            MaxCuratorRecommendedPages = ModPrefs.GetInt(Plugin.Instance.Name, "MaxCuratorRecommendedPages", 0);
            MaxBookmarksPages = ModPrefs.GetInt(Plugin.Instance.Name, "MaxBookmarksPages", 0);
        }
    }
}
