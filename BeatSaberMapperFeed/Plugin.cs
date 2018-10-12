using IllusionPlugin;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BeatSaberMapperFeed {
    public class Plugin : IPlugin {
        public string Name => "MapperFeed";
        public string Version => "1.0.1";
        public void OnApplicationStart() {
            new GameObject().AddComponent<MapperFeed>();
        }

        public void OnApplicationQuit() {
        }

        public void OnLevelWasLoaded(int level) {

        }

        public void OnLevelWasInitialized(int level) {
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
