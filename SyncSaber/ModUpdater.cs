using CustomUI.BeatSaber;
using SemVer;
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
using Version = SemVer.Version;

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
                Range dependencyRange = new Range(parts[1]);
                Version dependencyVersion = new Version(dependency.Value);

                //Plugin.Log($"Plugin {info.Name} depends on {dependencyName} (v{dependencyVersion})");

                Version installedVersion = new Version("0.0.0");
                int dependencyIndex = CurrentModInfo.FindIndex(i => i.CurrentInfo["name"].Value == dependencyName);
                ModInfo dependencyInfo = null;
                if (dependencyIndex != -1)
                {
                    dependencyInfo = CurrentModInfo[dependencyIndex];
                    installedVersion = new Version(info.CurrentInfo["version"].Value);
                }

                if (updateRequired)
                {
                    JSONNode updateJson = null;
                    //Plugin.Log($"Requesting update info for dependency {dependencyName} v{dependencyVersion}.");
                    using (UnityWebRequest updateRequest = UnityWebRequest.Get($"https://www.modsaber.org/api/v1.1/mods/semver/{dependencyName}/^{dependencyRange.ToString()}"))
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
                        Plugin.Log($"Dependency {updateJson["name"].Value} has already been updated! Skipping!");
                        yield break;
                    }

                    JSONObject updateObject = null;
                    foreach(JSONObject update in updateJson.AsArray)
                    {
                        Version updateVersion = new Version(update["version"].Value);
                        if (dependencyRange.IsSatisfied(updateVersion) && updateVersion > installedVersion && update["approval"]["status"].Value == "approved")
                        {
                            updateObject = update;
                            break;
                        }
                    }

                    if (updateObject == null)
                    {
                        Plugin.Log($"Couldn't find approved version for dependency {updateJson["name"].Value}. Aborting!");
                        yield break;
                    }

                    if (dependencyInfo != null)
                    {
                        dependencyName = dependencyInfo.Name;
                        dependencyInfo.UpdatePending = true;
                        dependencyInfo.UpdateInfo = updateObject;
                        CurrentModInfo[dependencyIndex] = dependencyInfo;
                    }

                    Plugin.Log($"Downloading update for dependency {dependencyName}");
                    yield return DownloadUpdate(new ModInfo() {
                        Name = dependencyName,
                        UpdateInfo = updateObject
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
                    
                    JSONNode ModSaberModInfo = null;
                    using (UnityWebRequest updateRequest = UnityWebRequest.Get($"https://www.modsaber.org/api/v1.1/mods/versions/{version["name"].Value}"))
                    {
                        yield return updateRequest.SendWebRequest();

                        if (updateRequest.isNetworkError || updateRequest.isHttpError)
                        {
                            Plugin.Log($"Error when checking for plugin update! {updateRequest.error}");
                            continue;
                        }
                        ModSaberModInfo = JSON.Parse(updateRequest.downloadHandler.text);
                    }
                    if (ModSaberModInfo == null) continue;

                    var currentVersion = new Version(version["version"].Value);
                    foreach (JSONObject update in ModSaberModInfo.AsArray)
                    {
                        var updateVersion = new Version(update["version"].Value);
                        if (updateVersion > currentVersion && update["approval"]["status"].Value == "approved")
                        {
                            currentModInfo.UpdateInfo = update;
                            break;
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
