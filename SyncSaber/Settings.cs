using CustomUI.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncSaber
{
    public class Settings
    {
        public static void OnLoad()
        {
            var menu = SettingsUI.CreateSubMenu("SyncSaber");
            var autoDownload = menu.AddBool("Auto-Download Songs", "Determines if SyncSaber should download new songs or just add them to the SyncSaber playlist.");
            autoDownload.GetValue += () => { return Config.AutoDownloadSongs; };
            autoDownload.SetValue += (value) => { Config.AutoDownloadSongs = value; Config.Write(); };

            var autoUpdate = menu.AddBool("Auto-Update Songs", "Determines if SyncSaber should update songs when you select them in the song list.");
            autoUpdate.GetValue += () => { return Config.AutoUpdateSongs; };
            autoUpdate.SetValue += (value) => { Config.AutoUpdateSongs = value; Config.Write(); };

            var deleteOldVersions = menu.AddBool("Delete Old Songs", "Determines if SyncSaber should delete old versions of songs when auto-updating.");
            deleteOldVersions.GetValue += () => { return Config.DeleteOldVersions; };
            deleteOldVersions.SetValue += (value) => { Config.DeleteOldVersions = value; Config.Write(); };

            var beastSaberUsername = menu.AddString("Beast Saber Username", "Your username from www.bsaber.com. Note: Only required if you want SyncSaber to automatically download content from the feeds listed below.");
            beastSaberUsername.GetValue += () => { return Config.BeastSaberUsername; };
            beastSaberUsername.SetValue += (username) => { Config.BeastSaberUsername = username; Config.Write(); };

            var downloadBookmarksFeed = menu.AddBool("Bookmarks Feed", "Determines if SyncSaber should download your BeastSaber bookmarks feed.");
            downloadBookmarksFeed.GetValue += () => { return Config.SyncBookmarksFeed; };
            downloadBookmarksFeed.SetValue += (value) => { Config.SyncBookmarksFeed = value; Config.Write(); };
            downloadBookmarksFeed.EnabledText = "Sync";
            downloadBookmarksFeed.DisabledText = "Don't Sync";

            var downloadFollowingsFeed = menu.AddBool("Followings Feed", "Determines if SyncSaber should download your BeastSaber followings feed.");
            downloadFollowingsFeed.GetValue += () => { return Config.SyncFollowingsFeed; };
            downloadFollowingsFeed.SetValue += (value) => { Config.SyncFollowingsFeed = value; Config.Write(); };
            downloadFollowingsFeed.EnabledText = "Sync";
            downloadFollowingsFeed.DisabledText = "Don't Sync";

            var downloadCuratorRecommendedFeed = menu.AddBool("Curator Recommended Feed", "Determines if SyncSaber should download the BeastSaber curator recommended feed.");
            downloadCuratorRecommendedFeed.GetValue += () => { return Config.SyncCuratorRecommendedFeed; };
            downloadCuratorRecommendedFeed.SetValue += (value) => { Config.SyncCuratorRecommendedFeed = value; Config.Write(); };
            downloadCuratorRecommendedFeed.EnabledText = "Sync";
            downloadCuratorRecommendedFeed.DisabledText = "Don't Sync";
        }
     }
}
