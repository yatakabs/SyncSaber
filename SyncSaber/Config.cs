//using IPA.Old;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace SyncSaber
//{
//    class Config
//    {
        
//        public static bool AutoDownloadSongs;
//        public static bool AutoUpdateSongs;
//        public static string BeastSaberUsername;
//        public static int MaxFollowingsPages;
//        public static int MaxBookmarksPages;
//        public static int MaxCuratorRecommendedPages;
//        public static bool DeleteOldVersions;
//        public static bool SyncBookmarksFeed;
//        public static bool SyncCuratorRecommendedFeed;
//        public static bool SyncFollowingsFeed;

//        public static void Write()
//        {
//            IPA.Config.ModPrefs.SetBool(Plugin.instance.Name, "AutoDownloadSongs", AutoDownloadSongs);
//            IPA.Config.ModPrefs.SetBool(Plugin.instance.Name, "AutoUpdateSongs", AutoUpdateSongs);
//            IPA.Config.ModPrefs.SetString(Plugin.instance.Name, "BeastSaberUsername", BeastSaberUsername);
//            IPA.Config.ModPrefs.SetBool(Plugin.instance.Name, "DeleteOldVersions", DeleteOldVersions);
//            IPA.Config.ModPrefs.SetBool(Plugin.instance.Name, "SyncBookmarksFeed", SyncBookmarksFeed);
//            IPA.Config.ModPrefs.SetBool(Plugin.instance.Name, "SyncCuratorRecommendedFeed", SyncCuratorRecommendedFeed);
//            IPA.Config.ModPrefs.SetBool(Plugin.instance.Name, "SyncFollowingsFeed", SyncFollowingsFeed);
//            IPA.Config.ModPrefs.SetInt(Plugin.instance.Name, "MaxBookmarksPages", MaxBookmarksPages);
//            IPA.Config.ModPrefs.SetInt(Plugin.instance.Name, "MaxCuratorRecommendedPages", MaxCuratorRecommendedPages);
//            IPA.Config.ModPrefs.SetInt(Plugin.instance.Name, "MaxFollowingsPages", MaxFollowingsPages);
//        }

//        public static void Read()
//        {
//            AutoDownloadSongs = IPA.Config.ModPrefs.GetBool(Plugin.instance.Name, "AutoDownloadSongs", true);
//            AutoUpdateSongs = IPA.Config.ModPrefs.GetBool(Plugin.instance.Name, "AutoUpdateSongs", true);
//            BeastSaberUsername = IPA.Config.ModPrefs.GetString(Plugin.instance.Name, "BeastSaberUsername", "");
//            DeleteOldVersions = IPA.Config.ModPrefs.GetBool(Plugin.instance.Name, "DeleteOldVersions", true);
//            SyncBookmarksFeed = IPA.Config.ModPrefs.GetBool(Plugin.instance.Name, "SyncBookmarksFeed", true);
//            SyncCuratorRecommendedFeed = IPA.Config.ModPrefs.GetBool(Plugin.instance.Name, "SyncCuratorRecommendedFeed", false);
//            SyncFollowingsFeed = IPA.Config.ModPrefs.GetBool(Plugin.instance.Name, "SyncFollowingsFeed", true);
//            MaxFollowingsPages = IPA.Config.ModPrefs.GetInt(Plugin.instance.Name, "MaxFollowingsPages", 0);
//            MaxCuratorRecommendedPages = IPA.Config.ModPrefs.GetInt(Plugin.instance.Name, "MaxCuratorRecommendedPages", 0);
//            MaxBookmarksPages = IPA.Config.ModPrefs.GetInt(Plugin.instance.Name, "MaxBookmarksPages", 0);
//        }
//    }
//}
