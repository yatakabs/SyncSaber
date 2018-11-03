using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace BeatSaberMapperFeed
{
    class Utilities
    {
        public static IEnumerator DownloadFile(string url, string path)
        {
            //Plugin.Log($"Downloading url {url}");
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
                        if (!Directory.Exists(Path.GetDirectoryName(path))) {
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

        public static void EmptyDirectory(System.IO.DirectoryInfo directory)
        {
            foreach (System.IO.FileInfo file in directory.GetFiles()) file.Delete();
            foreach (System.IO.DirectoryInfo subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
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

                yield return new WaitForSeconds(0.2f) ;

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
