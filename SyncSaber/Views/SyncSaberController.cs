using System;
using System.Collections.Generic;
using System.Linq;

using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using IPA.Loader;
using PlaylistDownLoader;
using PlaylistLoaderLite.HarmonyPatches;
using UnityEngine;
using Zenject;

namespace SyncSaber.Views
{
    [HotReload]
    internal class SyncSaberController : BSMLAutomaticViewController
    {
        /// <summary>説明 を取得、設定</summary>
        private string notificationText_;
        /// <summary>説明 を取得、設定</summary>
        [UIValue("notification-text")]
        public string NotificationText
        {
            get => this.notificationText_ ?? "";

            set
            {
                this.notificationText_ = value;
                this.NotifyPropertyChanged();
            }
        }

        private SyncSaber.SyncSaberFactory _syncSaberFactory;
        private PlaylistDownLoaderController playlistDownLoaderController;
        private SyncSaber _syncSaber;
        private DateTime _uiResetTime;
        private FloatingScreen _floatingScreen;

        [Inject]
        private void Constractor(SyncSaber.SyncSaberFactory sync, DiContainer diContainer)
        {
            this._syncSaberFactory = sync;
            if (PluginManager.GetPlugin("PlaylistDownLoader") != null) {
                this.playlistDownLoaderController = diContainer.Resolve<PlaylistDownLoaderController.PlaylistDownLoaderControllerFactory>().Create();
            }
        }

        async void Start()
        {
            Logger.Debug("SyncSaberService Initialize");
            try {
                this._floatingScreen = FloatingScreen.CreateFloatingScreen(new Vector2(100f, 20f), false, new Vector3(0f, 0.3f, 2.8f), new Quaternion(0f, 0f, 0f, 0f));
                DontDestroyOnLoad(this._floatingScreen.gameObject);
                this._floatingScreen.SetRootViewController(this, AnimationType.None);

                _syncSaber = _syncSaberFactory.Create();
                _syncSaber.NotificationTextChange -= this.NotificationTextChange;
                _syncSaber.NotificationTextChange += this.NotificationTextChange;
                await _syncSaber.Sync();
                if (playlistDownLoaderController != null) {
                    playlistDownLoaderController.ChangeNotificationText -= this.NotificationTextChange;
                    playlistDownLoaderController.ChangeNotificationText += this.NotificationTextChange;
                    await playlistDownLoaderController.CheckPlaylistsSong();
                    if (playlistDownLoaderController.AnyDownloaded) {
                        PlaylistCollectionOverride.RefreshPlaylists();
                    }
                }
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            _syncSaber._timer.Start();
        }

        private void NotificationTextChange(string obj)
        {
            this.NotificationText = obj;
            this._uiResetTime = DateTime.Now.AddSeconds(5);
        }

        void FixedUpdate()
        {
            if (!string.IsNullOrEmpty(this.NotificationText) && _uiResetTime <= DateTime.Now)
                this.NotificationText = "";
        }
    }
}
