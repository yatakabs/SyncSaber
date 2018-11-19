# Mod Info
This mod keeps all your songs up to date, and additionally downloads all the newest releases from your favorite mappers, as well as content from your followings/bookmarks feed on BeastSaber (https://bsaber.com)!

# Getting Started
All you have to do is copy the mod into the Plugins directory inside of your Beat Saber folder, whenever you select a song in your list it SyncSaber will automatically check for updates and download them if any are found. Also, SyncSaber will notify you on screen when it's downloading new releases!

# MapperFeed
Additionally, SyncSaber can download songs from any of your favorite mappers! All you have to do is create a file called FavoriteMappers.ini in the UserData folder inside your Beat saber director, then enter your favorite mappers BeatSaver usernames (one mapper per line) into FavoriteMappers.ini.

# BeastSaberFeed
Finally, if you want SyncSaber to automatically download content from your BeastSaber bookmarks/followings feeds, all you have to do is enter your BeastSaber username into the SyncSaber config section in the UserData\modprefs.ini file.

# Config
MapperFeed settings are stored in the modprefs.ini which you can find in the UserData folder inside your Beat Saber install directory. The following config options are available:

| Option                     | Description                                                                                                                  |
|----------------------------|------------------------------------------------------------------------------------------------------------------------------|
| **AutoDownloadSongs** | Whether or not SyncSaber should automatically download any new songs it finds, or just add them to the SyncSaber playlist. |
| **AutoUpdateSongs**| Whether or not SyncSaber will attempt to auto-update songs when you click on them in the song browser. |
| **BeastSaberUsername**| Your username from BeastSaber (https://bsaber.com), this config option is optional. Only enter this if you want to automatically download songs posted to your followings/bookmarks feed on BeastSaber. |
| **MaxBeastSaberPages**| The maximum number of pages to scan for new songs on BeastSaber, 0 is unlimited. |
| **DeleteOldVersions**| Whether or not SyncSaber should keep or delete old versions of songs upon updating them, or when a new release is downloaded from one of your favorite mappers. |

# Download
[Click here to download the latest SyncSaber.dll](https://github.com/brian91292/SyncSaber/releases)
