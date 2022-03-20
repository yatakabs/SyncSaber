using SyncSaber.Views;
using Zenject;

namespace SyncSaber.Installers
{
    public class SyncSaberInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            this.Container.BindInterfacesAndSelfTo<SyncSaber>().FromNewComponentOnNewGameObject().AsSingle();
            this.Container.BindInterfacesAndSelfTo<SyncSaberController>().FromNewComponentAsViewController().AsSingle().NonLazy();
            this.Container.BindInterfacesAndSelfTo<SongListUtil>().AsCached();
        }
    }
}
