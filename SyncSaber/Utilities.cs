using CustomUI.BeatSaber;
using IPA.Old;
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

            var _notificationText = BeatSaberUI.CreateText(_canvas.transform as RectTransform, text, new Vector2(0, -20), new Vector2(400, 20));

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
                    Plugin.Log($"DownloadFile failed with error {www.error}, HttpResponseCode: {www.responseCode}");
                    yield break;
                }
                
                try
                {
                    if (!Directory.Exists(Path.GetDirectoryName(path)))
                        Directory.CreateDirectory(Path.GetDirectoryName(path));

                    File.WriteAllBytes(path, www.downloadHandler.data);
                }
                catch (Exception e)
                {
                    Plugin.Log($"Exception when writing file! {e.ToString()}");
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

                if (delete) Directory.Delete(directory, true);
            }
        }

        public static void MoveFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                MoveFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
            {
                string newFilePath = Path.Combine(target.FullName, file.Name);
                if (File.Exists(newFilePath)) {
                    try
                    {
                        File.Delete(newFilePath);
                    }
                    catch(Exception)
                    {
                        //Plugin.Log($"Failed to delete file {Path.GetFileName(newFilePath)}! File is in use!");
                        string filesToDelete = Path.Combine(Environment.CurrentDirectory, "FilesToDelete");
                        if (!Directory.Exists(filesToDelete))
                            Directory.CreateDirectory(filesToDelete);
                        File.Move(newFilePath, Path.Combine(filesToDelete, file.Name));
                        //Plugin.Log("Moved file into FilesToDelete directory!");
                    }
                }
                file.MoveTo(newFilePath);
            }
        }

        public static IEnumerator ExtractZip(string zipPath, string extractPath)
        {
            if (File.Exists(zipPath))
            {
                bool extracted = false;
                try
                {
                    if (Directory.Exists(".syncsabertemp"))
                        Directory.CreateDirectory(".syncsabertemp");

                    ZipFile.ExtractToDirectory(zipPath, ".syncsabertemp");
                    extracted = true;
                }
                catch (Exception)
                {
                    Plugin.Log($"An error occured while trying to extract \"{zipPath}\"!");
                    yield break;
                }
                yield return new WaitForSeconds(0.25f);
                File.Delete(zipPath);

                try
                {
                    if (extracted)
                    {
                        if (!Directory.Exists(extractPath))
                            Directory.CreateDirectory(extractPath);

                        MoveFilesRecursively(new DirectoryInfo(Path.Combine(Environment.CurrentDirectory, ".syncsabertemp")), new DirectoryInfo(extractPath));
                    }
                }
                catch (Exception e)
                {
                    Plugin.Log($"An exception occured while trying to move files into their final directory! {e.ToString()}");
                }
                EmptyDirectory(".syncsabertemp");
            }
        }

        public static bool IsModInstalled(string modName)
        {
            foreach (IPlugin p in IPA.Loader.PluginManager.Plugins)
            {
                if (p.Name == modName)
                {
                    return true;
                }
            }
            foreach (var p in IPA.Loader.PluginManager.AllPlugins)
            {
                if (p.Metadata.Id == modName)
                {
                    return true;
                }
            }
            return false;
        }

        public static void WriteStringListSafe(string path, List<string> data, bool sort = true)
        {
            if (File.Exists(path))
                File.Copy(path, path + ".bak", true);

            if(sort) 
                data.Sort();

            File.WriteAllLines(path, data);
            File.Delete(path + ".bak");
        }
    }
}
