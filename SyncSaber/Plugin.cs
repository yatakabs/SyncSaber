using IllusionPlugin;
using System;
using System.Collections;
using System.IO;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SyncSaber
{
    public class Plugin : IPlugin
    {
        public string Name => "SyncSaber";
        public string Version => "1.3.4";

        public static Plugin Instance;
        
        private TextMeshProUGUI _mapperFeedNotification = null;

        private IEnumerator DelayedStartup()
        {
            yield return new WaitForSeconds(0.5f);
            if (Utilities.IsModInstalled("MapperFeed"))
            {
                File.Move("Plugins\\BeatSaberMapperFeed.dll", "Plugins\\BeatSaberMapperFeed.dll.delete-me");
                _mapperFeedNotification = Utilities.CreateNotificationText("Old version of MapperFeed detected! Restart the game now to enable SyncSaber!");
                Plugin.Log("Old MapperFeed detected!");

                yield break;
            }

            Config.Read();
            Config.Write();
            
            //ModUpdater.OnLoad();
            SyncSaber.OnLoad();
        }

        public void OnApplicationStart()
        {
            Instance = this;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
            SharedCoroutineStarter.instance.StartCoroutine(DelayedStartup());
        }

        public void OnApplicationQuit()
        {
            SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
            SceneManager.activeSceneChanged -= SceneManagerOnActiveSceneChanged;
        }

        private void SceneManager_sceneLoaded(Scene arg0, LoadSceneMode arg1)
        {
            if (arg0.name == "MenuCore")
            {
                Settings.OnLoad();
            }
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
        
        public static void Log(string text,
                        [CallerFilePath] string file = "",
                        [CallerMemberName] string member = "",
                        [CallerLineNumber] int line = 0)
        {
            Console.WriteLine($"[SyncSaber] {Path.GetFileName(file)}->{member}({line}): {text}");
        }
    }
}
