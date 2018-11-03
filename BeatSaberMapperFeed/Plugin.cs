using IllusionPlugin;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberMapperFeed {
    public class Plugin : IPlugin {
        public string Name => "MapperFeed";
        public string Version => "1.1.0";

        public static bool IsInGame = false;

        private bool initialized = false;

        private MapperFeed _mapperFeed = null;

        public void OnApplicationStart() {
            Config.Read();
            Config.Write();

            _mapperFeed = new GameObject().AddComponent<MapperFeed>();

            SceneManager.activeSceneChanged += SceneManagerOnActiveSceneChanged;
        }

        public void OnApplicationQuit() {
            SceneManager.activeSceneChanged -= SceneManagerOnActiveSceneChanged;
        }

        public void OnLevelWasLoaded(int level) {

        }

        public void OnLevelWasInitialized(int level) {
        }

        void SceneManagerOnActiveSceneChanged(Scene arg0, Scene scene)
        {
            if (scene.name == "Menu") IsInGame = false;
            else if (scene.name.Contains("Environment")) IsInGame = true;
        }

        public void OnUpdate() {
        }

        public void OnFixedUpdate() {
        }

        public static void Log(string msg) {
            msg = $"[MapperFeed] {msg}";
            Console.WriteLine(msg);
        }
    }
}
