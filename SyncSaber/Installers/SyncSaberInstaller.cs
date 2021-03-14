using SiraUtil;
using SyncSaber.Views;
using System;
using Zenject;

namespace SyncSaber.Installers
{
    public class SyncSaberInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            try {
                this.Container.BindInterfacesAndSelfTo<SyncSaber>().FromNewComponentOnNewGameObject(nameof(SyncSaber)).AsSingle();
                this.Container.BindInterfacesAndSelfTo<SyncSaberController>().FromNewComponentAsViewController().AsSingle().NonLazy(); ;
                //this.Container.BindInterfacesAndSelfTo<RootViewController>().FromNewComponentOnNewGameObject().AsSingle();
            }
            catch (Exception e) {
                Logger.Error(e);
            }
        }
    }
}
