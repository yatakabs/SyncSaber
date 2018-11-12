using IllusionInjector;
using IllusionPlugin;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

namespace SyncSaber
{
    class Utilities
    {
        public static TextMeshProUGUI CreateNotificationText(string text)
        {
            var gameObject = new GameObject();
            GameObject.DontDestroyOnLoad(gameObject);
            gameObject.transform.position = new Vector3(0, 0f, 2.5f);
            gameObject.transform.eulerAngles = new Vector3(0, 0, 0);
            gameObject.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

            var _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var rectTransform = _canvas.transform as RectTransform;
            rectTransform.sizeDelta = new Vector2(200, 50);

            var _notificationText = new GameObject().AddComponent<TextMeshProUGUI>();
            rectTransform = _notificationText.transform as RectTransform;
            rectTransform.SetParent(_canvas.transform, false);
            rectTransform.anchoredPosition = new Vector2(0, 45);
            rectTransform.sizeDelta = new Vector2(400, 20);
            _notificationText.text = text;
            _notificationText.fontSize = 10f;
            _notificationText.alignment = TextAlignmentOptions.Center;
            return _notificationText;
        }

        public static IEnumerator DownloadFile(string url, string path)
        {
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                yield return www.SendWebRequest();
                if (www.isNetworkError || www.isHttpError)
                {
                    Plugin.Log(www.error);
                }
                else
                {
                    try
                    {
                        if (!Directory.Exists(Path.GetDirectoryName(path)))
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(path));
                        }
                        File.WriteAllBytes(path, www.downloadHandler.data);
                    }
                    catch (Exception e)
                    {
                        Plugin.Log($"Exception when writing file! {e.ToString()}");
                    }
                }
            }
        }

        public static void EmptyDirectory(string directory, bool delete = true)
        {
            if (Directory.Exists(directory))
            {
                var directoryInfo = new DirectoryInfo(directory);
                foreach (System.IO.FileInfo file in directoryInfo.GetFiles()) file.Delete();
                foreach (System.IO.DirectoryInfo subDirectory in directoryInfo.GetDirectories()) subDirectory.Delete(true);

                if (delete) Directory.Delete(directory);
            }
        }

        public static IEnumerator ExtractZip(string zipPath, string extractPath)
        {
            if (File.Exists(zipPath))
            {
                bool extracted = false;
                try
                {
                    ZipFile.ExtractToDirectory(zipPath, ".songcache");
                    extracted = true;
                }
                catch (Exception)
                {
                    Plugin.Log($"An error occured while trying to extract \"{zipPath}\"!");
                    yield break;
                }

                yield return new WaitForSeconds(0.2f);

                File.Delete(zipPath);

                try
                {
                    if (extracted)
                    {
                        string[] directories = Directory.GetDirectories($"{Environment.CurrentDirectory}\\.songcache");
                        foreach (var directory in directories)
                        {
                            Directory.Move(directory, extractPath);
                        }
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log($"An exception occured while trying to move files into their final directory! {e.ToString()}");
                }
            }
        }

        public static bool IsModInstalled(string modName)
        {
            foreach (IPlugin p in PluginManager.Plugins)
            {
                if (p.Name == modName)
                {
                    return true;
                }
            }
            return false;
        }

        public static void WriteStringListSafe(string path, List<string> data)
        {
            if (File.Exists(path))
            {
                File.Copy(path, path + ".bak", true);
            }
            data.Sort();
            File.WriteAllLines(path, data);
            File.Delete(path + ".bak");
        }
    }
}
