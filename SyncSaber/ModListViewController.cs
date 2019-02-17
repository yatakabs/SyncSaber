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

namespace SyncSaber
{
    public class ModListViewController : CustomListViewController
    {
        protected override void DidActivate(bool firstActivation, ActivationType type)
        {
            try
            {
                base.DidActivate(firstActivation, type);
                if (firstActivation)
                {
                    _customListTableView.didSelectRowEvent += _customListTableView_didSelectRowEvent;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("EXCEPTION IN ModListViewController.DidActivate: " + e);
            }
        }
        
        private void _customListTableView_didSelectRowEvent(TableView table, int row)
        {
            ModInfo modInfo = ModUpdater.Instance.CurrentModInfo[row];
            if (modInfo.UpdateInfo != null)
            {
                Plugin.Log($"Downloading update for plugin {modInfo.Name}");
                ModUpdater.Instance.CurrentModInfo[row].UpdatePending = true;
                StartCoroutine(ModUpdater.Instance.DownloadUpdate(modInfo, () => _customListTableView.ReloadData()));
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

            Plugin.Log("CellForRow");

            _tableCell.songName = modInfo.CurrentInfo["details"]["title"].Value;
            if (modInfo.UpdatePending)
                _tableCell.author = "<color=#ffff00ff>Update pending! Restart the game to complete.</color>";
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
