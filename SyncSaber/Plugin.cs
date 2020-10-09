using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using IPA;
using IPA.Config;
using IPA.Config.Stores;
using UnityEngine.SceneManagement;
using UnityEngine;
using IPALogger = IPA.Logging.Logger;
using BS_Utils.Utilities;
using SongCore;
using System.Reflection;
using System.IO;
using TMPro;
using IPA.Loader;
using IPA.Utilities;
using BeatSaberMarkupLanguage.Settings;
using SyncSaber.UI;
using System.Threading.Tasks;
using BeatSaberMarkupLanguage.MenuButtons;

namespace SyncSaber
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        public bool IsInGame { get; private set; }
        public static HashSet<string> SongDownloadHistory { get; } = new HashSet<string>();
        internal static Plugin instance { get; private set; }
        public string Name => "SyncSaber";

        [Init]
        /// <summary>
        /// Called when the plugin is first loaded by IPA (either when the game starts or when the plugin is enabled if it starts disabled).
        /// [Init] methods that use a Constructor or called before regular methods like InitWithConfig.
        /// Only use [Init] with one Constructor.
        /// </summary>
        public void Init(IPALogger logger)
        {
            instance = this;
            Logger.log = logger;
            Logger.log.Debug("Logger initialized.");
        }

        #region BSIPA Config
        //Uncomment to use BSIPA's config
        [Init]
        public void InitWithConfig(IPA.Config.Config conf)
        {
            Configuration.PluginConfig.Instance = conf.Generated<Configuration.PluginConfig>();
            Logger.log.Debug("Config loaded");
        }
        #endregion

        
        

        private TextMeshProUGUI _mapperFeedNotification = null;

        private async Task DelayedStartup()
        {
            await Task.Delay(500);
            if (Utilities.IsModInstalled("MapperFeed"))
            {
                File.Move("Plugins\\BeatSaberMapperFeed.dll", "Plugins\\BeatSaberMapperFeed.dll.delete-me");
                _mapperFeedNotification = Utilities.CreateNotificationText("Old version of MapperFeed detected! Restart the game now to enable SyncSaber!");
                Logger.Info("Old MapperFeed detected!");
            }
            SyncSaber.OnLoad();
        }

        [OnStart]
        public void OnApplicationStart()
        {
            instance = this;
            
            BSEvents.earlyMenuSceneLoadedFresh += this.BSEvents_earlyMenuSceneLoadedFresh;
            BSEvents.lateMenuSceneLoadedFresh += this.BSEvents_lateMenuSceneLoadedFresh;
            
            SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
            _ = DelayedStartup();
        }

        private async void BSEvents_lateMenuSceneLoadedFresh(ScenesTransitionSetupDataSO obj)
        {
            await Task.Delay(1000);
            while (!Loader.AreSongsLoaded || Loader.AreSongsLoading) {
                await Task.Delay(200);
            }
            await SyncSaber.Instance.Sync();
            SyncSaber.Instance._timer.Start();
        }

        private void BSEvents_earlyMenuSceneLoadedFresh(ScenesTransitionSetupDataSO obj)
        {
            try {
                BSMLSettings.instance.AddSettingsMenu("SYNC SABER", SettingViewController.instance.ResourceName, SettingViewController.instance);
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }


        [OnExit]
        public void OnApplicationQuit()
        {
            SceneManager.activeSceneChanged -= SceneManagerOnActiveSceneChanged;
            BSEvents.earlyMenuSceneLoadedFresh -= this.BSEvents_earlyMenuSceneLoadedFresh;
        }

        void SceneManagerOnActiveSceneChanged(Scene arg0, Scene scene)
        {
            this.IsInGame = scene.name == "GameCore";
        }
    }
}
