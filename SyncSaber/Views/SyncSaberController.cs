using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.FloatingScreen;
using BeatSaberMarkupLanguage.ViewControllers;
using SyncSaber.Interfaces;
using System;
using UnityEngine;
using Zenject;

namespace SyncSaber.Views
{
    [HotReload]
    internal class SyncSaberController : BSMLAutomaticViewController, IInitializable
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
        [Inject]
        private readonly ISyncSaber _syncSaber;
        private DateTime _uiResetTime;
        private FloatingScreen _floatingScreen;

        private void Awake()
        {
            Logger.Debug("Awake call");
            DontDestroyOnLoad(this);
        }

        protected override void OnDestroy()
        {
            Logger.Debug("OnDestroy call");
            base.OnDestroy();
        }

        private void NotificationTextChange(string obj)
        {
            this.NotificationText = obj;
            this._uiResetTime = DateTime.Now.AddSeconds(5);
        }

        private void Update()
        {
            if (!string.IsNullOrEmpty(this.NotificationText) && this._uiResetTime <= DateTime.Now) {
                this.NotificationText = "";
            }
        }

        public async void Initialize()
        {
            Logger.Debug("Initialize call");
            try {
                this._floatingScreen = FloatingScreen.CreateFloatingScreen(new Vector2(100f, 20f), false, new Vector3(0f, 0.3f, 2.8f), new Quaternion(0f, 0f, 0f, 0f));
                this._floatingScreen.SetRootViewController(this, AnimationType.None);
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            try {
                this._syncSaber.NotificationTextChange -= this.NotificationTextChange;
                this._syncSaber.NotificationTextChange += this.NotificationTextChange;
                if (Plugin.Instance.IsPlaylistDownlaoderInstalled) {
                    this._syncSaber.SetEvent();
                }
                await this._syncSaber.Sync();
            }
            catch (Exception e) {
                Logger.Error(e);
            }
            this._syncSaber.StartTimer();
        }
    }
}
