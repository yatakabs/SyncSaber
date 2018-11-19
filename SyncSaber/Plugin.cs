using IllusionPlugin;
using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SyncSaber
{
    public class Plugin : IPlugin
    {
        public string Name => "SyncSaber";
        public string Version => "1.2.5";
        
        private SyncSaber _syncSaber = null;
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

            _syncSaber = new GameObject().AddComponent<SyncSaber>();
        }

        public void OnApplicationStart()
        {
            SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;

            SharedCoroutineStarter.instance.StartCoroutine(DelayedStartup());
        }

        public void OnApplicationQuit()
        {
            SceneManager.activeSceneChanged -= SceneManagerOnActiveSceneChanged;
        }

        public void OnLevelWasLoaded(int level)
        {
        }

        public void OnLevelWasInitialized(int level)
        {
        }

        void SceneManagerOnActiveSceneChanged(Scene arg0, Scene scene)
        {
            if (scene.name.Contains("Environment"))
            {
                if (_mapperFeedNotification != null)
                {
                    GameObject.Destroy(_mapperFeedNotification);
                    _mapperFeedNotification = null;
                }
            }
        }

        public void OnUpdate()
        {
        }

        public void OnFixedUpdate()
        {
        }

        public static void Log(string msg)
        {
            msg = $"[SyncSaber] {msg}";
            Console.WriteLine(msg);
        }
    }
}
