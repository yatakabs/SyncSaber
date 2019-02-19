using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CustomUI.BeatSaber;
using CustomUI.Utilities;
using HMUI;
using IllusionInjector;
using SimpleJSON;
using UnityEngine;

namespace SyncSaber
{
    public class ModListViewController : CustomListViewController
    {
        private LevelListTableCell _songListTableCellInstance;
        private static ModListViewController _instance;
        private void Awake()
        {
            _instance = this;
        }

        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            try
            {
                if (firstActivation)
                    _songListTableCellInstance = Resources.FindObjectsOfTypeAll<LevelListTableCell>().First(x => (x.name == "LevelListTableCell"));

                base.DidActivate(firstActivation, type);

                if (firstActivation)
                    _customListTableView.didSelectRowEvent += _customListTableView_didSelectRowEvent;
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION IN ModListViewController.DidActivate: " + e);
            }
        }

        protected override void DidDeactivate(DeactivationType type)
        {
            base.DidDeactivate(type);
        }

        public static void ReloadData()
        {
            ModUpdater.Instance.CurrentModInfo = ModUpdater.Instance.CurrentModInfo.OrderBy(m => m.UpdateInfo == null).ThenBy(m => m.CurrentInfo["details"]["title"].Value).ToList();
            _instance?._customListTableView.ReloadData();
        }

        private void _customListTableView_didSelectRowEvent(TableView table, int row)
        {
            ModInfo modInfo = ModUpdater.Instance.CurrentModInfo[row];
            if (modInfo.UpdateInfo != null)
            {
                Plugin.Log($"Downloading update for plugin {modInfo.Name}");
                ModUpdater.Instance.CurrentModInfo[row].UpdateInitiated = true;
                StartCoroutine(ModUpdater.Instance.DownloadUpdate(modInfo, () => ReloadData()));
            }
        }

        public override int NumberOfRows()
        {
            return ModUpdater.Instance.CurrentModInfo.Count;
        }

        public override TableCell CellForRow(int row)
        {
            LevelListTableCell _tableCell = _customListTableView.DequeueReusableCellForIdentifier("LevelListTableCell") as LevelListTableCell;
            if(!_tableCell)
                _tableCell = Instantiate(_songListTableCellInstance);

            ModInfo modInfo = ModUpdater.Instance.CurrentModInfo[row];
            
            _tableCell.songName = $"{modInfo.CurrentInfo["details"]["title"].Value} {modInfo.CurrentInfo["version"].Value}";
            if (modInfo.UpdateInitiated)
                _tableCell.author = "<color=#ffff00ff>Update complete! Restart the game to apply.</color>";
            else if (modInfo.UpdateInfo == null)
                _tableCell.author = "<color=#00ff00ff>Up to date</color>";
            else
                _tableCell.author = $"<color=#ff0000ff>Version {modInfo.UpdateInfo["version"].Value} is available!</color>";

            _tableCell.coverImage = UIUtilities.BlankSprite;
            _tableCell.reuseIdentifier = "LevelListTableCell";
            return _tableCell;
        }
    }
}
