using CustomUI.BeatSaber;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace SyncSaber
{
    public class ModInfo
    {
        public string Name = "";
        public JSONObject CurrentInfo = null;
        public JSONObject UpdateInfo = null;
        public bool UpdatePending = false;
    }

    public class ModUpdater : MonoBehaviour
    {
        public static ModUpdater Instance = null;
        public List<ModInfo> CurrentModInfo = new List<ModInfo>();
        public List<JSONObject> UpdatedModInfo = new List<JSONObject>();
        public CustomMenu ModUpdaterMenu = null;
        public static void OnLoad()
        {
            if (Instance) return;
            new GameObject("SyncSaberModUpdater").AddComponent<ModUpdater>();
        }

        public void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            ModUpdaterMenu = BeatSaberUI.CreateCustomMenu<CustomMenu>("Mod Updater");
            ModUpdaterMenu.SetMainViewController(BeatSaberUI.CreateViewController<ModListViewController>(), true);
            string oldPluginsPath = Path.Combine(Environment.CurrentDirectory, "FilesToDelete");
            if (Directory.Exists(oldPluginsPath))
                Utilities.EmptyDirectory(oldPluginsPath);
            StartCoroutine(CollectCurrentModInfo());
        }
        
        public IEnumerator AcquireDependencies(ModInfo info, Action OnCompleted)
        {
            foreach (JSONString dependency in info.UpdateInfo["links"]["dependencies"].AsArray)
            {
                bool updateRequired = true;
                string[] parts = dependency.Value.Split('@');
                string dependencyName = parts[0];
                string dependencyVersion = parts[1].Substring(1);

                string[] versionParts = dependencyVersion.Split('.');
                int major = int.Parse(versionParts[0]);
                int minor = int.Parse(versionParts[1]);
                int patch = int.Parse(versionParts[2]);

                //Plugin.Log($"Plugin {info.Name} depends on {dependencyName} (v{dependencyVersion})");
                
                int dependencyIndex = CurrentModInfo.FindIndex(i => i.CurrentInfo["name"].Value == dependencyName);
                ModInfo dependencyInfo = null;
                if (dependencyIndex != -1)
                {
                    dependencyInfo = CurrentModInfo[dependencyIndex];
                    //Plugin.Log($"Checking if dependency {dependencyName} needs an update!");

                    string installedVersion = dependencyInfo.CurrentInfo["version"].Value;
                    string[] installedVersionParts = installedVersion.Split('.');
                    int installedMajor = int.Parse(installedVersionParts[0]);
                    int installedMinor = int.Parse(installedVersionParts[1]);
                    int installedPatch = int.Parse(installedVersionParts[2]);
                    
                    if(major > installedMajor)
                    {
                        //Plugin.Log("Major version update!");
                    }
                    else if(major == installedMajor)
                    {
                        if(minor > installedMinor)
                        {
                            //Plugin.Log("Minor version update!");
                        }
                        else if(minor == installedMinor)
                        {
                            if(patch > installedPatch)
                            {
                                //Plugin.Log("Patch version update!");
                            }
                            else
                            {
                                //Plugin.Log("Dependency is up to date!");
                                updateRequired = false;
                            }
                        }
                    }
                }

                if(updateRequired)
                {
                    JSONNode updateJson = null;
                    //Plugin.Log($"Requesting update info for dependency {dependencyName} v{dependencyVersion}.");
                    using (UnityWebRequest updateRequest = UnityWebRequest.Get($"https://www.modsaber.org/api/v1.1/mods/versions/{dependencyName}"))
                    {
                        yield return updateRequest.SendWebRequest();

                        if (updateRequest.isNetworkError || updateRequest.isHttpError)
                        {
                            Plugin.Log($"Error when checking for dependency update! {updateRequest.error}");
                            continue;
                        }
                        updateJson = JSON.Parse(updateRequest.downloadHandler.text);
                    }
                    if (updateJson == null) continue;


                    if (UpdatedModInfo.FindIndex(u => u["name"].Value == updateJson["name"].Value) != -1)
                    {
                        Plugin.Log($"Dependency {updateJson["name"].Value} is already downloaded! Skipping!");
                        yield break;
                    }

                    if (dependencyInfo != null)
                    {
                        dependencyName = dependencyInfo.Name;
                        dependencyInfo.UpdatePending = true;
                        dependencyInfo.UpdateInfo = updateJson.AsArray[0].AsObject;
                        CurrentModInfo[dependencyIndex] = dependencyInfo;
                    }

                    Plugin.Log($"Downloading update for dependency {dependencyName}");
                    yield return DownloadUpdate(new ModInfo() {
                        Name = dependencyName,
                        UpdateInfo = updateJson.AsArray[0].AsObject
                    }, OnCompleted);
                }
            }
        }
        
        public IEnumerator DownloadUpdate(ModInfo modInfo, Action OnCompleted)
        {
            JSONObject updateInfo = modInfo.UpdateInfo;
            string localPath = Path.Combine(Path.GetTempPath(), $"{updateInfo["name"].Value}{updateInfo["version"].Value}.zip");
            yield return Utilities.DownloadFile(updateInfo["files"]["steam"]["url"].Value, localPath);
            if (!File.Exists(localPath))
            {
                Plugin.Log("Failed to download update! Aborting!");
                yield break;
            }

            string pluginPath = Path.Combine(Environment.CurrentDirectory, "Plugins", modInfo.Name);
            string filesToDelete = Path.Combine(Environment.CurrentDirectory, "FilesToDelete");
            if (!Directory.Exists(filesToDelete))
                Directory.CreateDirectory(filesToDelete);
            if(File.Exists(pluginPath))
                File.Move(pluginPath, Path.Combine(filesToDelete, modInfo.Name));
            yield return Utilities.ExtractZip(localPath, Environment.CurrentDirectory);

            if (UpdatedModInfo.FindIndex(m => m["name"].Value == modInfo.UpdateInfo["name"].Value) == -1)
            {
                UpdatedModInfo.Add(modInfo.UpdateInfo);
                //Plugin.Log($"Downloaded update for {modInfo.UpdateInfo["name"].Value}");
            }
            OnCompleted?.Invoke();
            yield return AcquireDependencies(modInfo, OnCompleted);
            Plugin.Log($"{modInfo.Name} updated to version {modInfo.UpdateInfo["version"].Value} successfully!");
        }

        private IEnumerator CollectCurrentModInfo()
        {
            List<string> plugins = Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "Plugins"), "*.dll", SearchOption.TopDirectoryOnly).ToList();
            //plugins.AddRange(Directory.GetFiles(Path.Combine(Environment.CurrentDirectory, "Beat Saber_Data", "Managed"), "*.dll", SearchOption.TopDirectoryOnly));
            foreach(string plugin in plugins)
            {
                if (!plugin.EndsWith(".dll")) continue;

                string dllName = Path.GetFileName(plugin);
                string hash = String.Empty;
                using (var sha1 = SHA1.Create())
                {
                    using (var stream = File.OpenRead(plugin))
                    {
                        hash = BitConverter.ToString(sha1.ComputeHash(stream)).Replace("-", "").ToLower();
                    }
                }
                if (hash == String.Empty) continue;

                JSONNode currentJson = null;
                //Plugin.Log($"Checking for newer version of plugin {dllName} (Hash: {hash})");
                using (UnityWebRequest checkVersionRequest = UnityWebRequest.Get($"https://www.modsaber.org/api/v1.1/mods/by-hash/{hash}"))
                {
                    yield return checkVersionRequest.SendWebRequest();
                    if (checkVersionRequest.isNetworkError || checkVersionRequest.isHttpError)
                    {
                        Plugin.Log($"Error when checking plugin version! {checkVersionRequest.error}");
                        continue;
                    }
                    currentJson = JSON.Parse(checkVersionRequest.downloadHandler.text);
                }
                if(currentJson == null || currentJson.IsNull || currentJson.AsArray.Count == 0)
                {
                    //Plugin.Log("Mod does not exist on modsaber, or was edited by the user.");
                    continue;
                }
                            
                foreach (JSONObject version in currentJson.AsArray)
                {
                    Plugin.Log($"Modsaber name is {version["name"].Value}, status is {version["approval"]["status"].Value}");
                    ModInfo currentModInfo = new ModInfo()
                    {
                        Name = dllName,
                        CurrentInfo = version
                    };

                    // Check if a newer update is available
                    if (version["approval"]["status"].Value == "denied" && version["approval"]["reason"].Value.StartsWith("Newer"))
                    {
                        JSONNode updateJson = null;
                        //Plugin.Log($"Requesting update info for {dllName}.");
                        using (UnityWebRequest updateRequest = UnityWebRequest.Get($"https://www.modsaber.org/api/v1.1/mods/versions/{version["name"].Value}"))
                        {
                            yield return updateRequest.SendWebRequest();

                            if (updateRequest.isNetworkError || updateRequest.isHttpError)
                            {
                                Plugin.Log($"Error when checking for plugin update! {updateRequest.error}");
                                continue;
                            }
                            updateJson = JSON.Parse(updateRequest.downloadHandler.text);
                        }
                        if (updateJson == null) continue;

                        foreach (JSONObject update in updateJson.AsArray)
                        {
                            if (update["approval"]["status"].Value == "approved")
                            {
                                currentModInfo.UpdateInfo = update;
                                break;
                            }
                        }
                    }
                    CurrentModInfo.Add(currentModInfo);
                    break;
                }
                yield return null;
            }
            if (ModUpdaterMenu.customFlowCoordinator.isActivated)
                (ModUpdaterMenu.mainViewController as ModListViewController)._customListTableView?.ReloadData();
            //Plugin.Log($"Found {CurrentModInfo.Count} mods!");
        }
    }
}
