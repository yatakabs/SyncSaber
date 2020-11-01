using SiraUtil;
using SyncSaber.Utilities;
using SyncSaber.Views;
using System;
using UnityEngine;
using Zenject;

namespace SyncSaber.Installers
{
    public class SyncSaberInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            try {
                this.Container.BindInterfacesAndSelfTo<SyncSaber>().FromNewComponentOnNewGameObject().AsSingle();
                this.Container.BindViewController<SyncSaberController>();
                this.Container.BindInterfacesAndSelfTo<RootViewController>().FromNewComponentOnNewGameObject().AsSingle();
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
    }
}
