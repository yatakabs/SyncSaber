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

namespace SyncSaber
{
    [Plugin(RuntimeOptions.SingleStartInit)]
    public class Plugin
    {
        public static bool SongBrowserPluginPresent { get; set; }

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

        private IEnumerator DelayedStartup()
        {
            yield return new WaitForSeconds(0.5f);
            if (Utilities.IsModInstalled("MapperFeed"))
            {
                File.Move("Plugins\\BeatSaberMapperFeed.dll", "Plugins\\BeatSaberMapperFeed.dll.delete-me");
                _mapperFeedNotification = Utilities.CreateNotificationText("Old version of MapperFeed detected! Restart the game now to enable SyncSaber!");
                Logger.Info("Old MapperFeed detected!");

                yield break;
            }
            SyncSaber.OnLoad();
        }

        [OnStart]
        public void OnApplicationStart()
        {
            instance = this;
            SongBrowserPluginPresent = PluginManager.GetPlugin("Song Browser") != null;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
            DelayedStartup();
            //SharedCoroutineStarter.instance.StartCoroutine(DelayedStartup());
        }

        [OnExit]
        public void OnApplicationQuit()
        {
            SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
            SceneManager.activeSceneChanged -= SceneManagerOnActiveSceneChanged;
        }

        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            //if (arg0.name == "MenuCore")
            //{
            //    Settings.OnLoad();
            //}
        }

        void SceneManagerOnActiveSceneChanged(Scene arg0, Scene scene)
        {
            if (scene.name == "GameCore")
            {
                if (_mapperFeedNotification != null)
                {
                    GameObject.Destroy(_mapperFeedNotification);
                    _mapperFeedNotification = null;
                }
            }
        }

        public void OnLevelWasLoaded(int level)
        {
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        public void OnUpdate()
        {
        }

        public void OnFixedUpdate()
        {
        }

        public static void SongBrowserCancelFilter()
        {
            if (SongBrowserPluginPresent) {
                var songBrowserUI = SongBrowser.SongBrowserApplication.Instance.GetField<SongBrowser.UI.SongBrowserUI, SongBrowser.SongBrowserApplication>("_songBrowserUI");
                if (songBrowserUI) {
                    if (songBrowserUI.Model.Settings.filterMode != SongBrowser.DataAccess.SongFilterMode.None && songBrowserUI.Model.Settings.sortMode != SongBrowser.DataAccess.SongSortMode.Original) {
                        songBrowserUI.CancelFilter();
                    }
                }
                else {
                    Logger.Info("There was a problem obtaining SongBrowserUI object, unable to reset filters");
                }
            }
        }
    }
}
