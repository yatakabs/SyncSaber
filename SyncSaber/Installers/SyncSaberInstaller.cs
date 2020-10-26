using SiraUtil;
using SyncSaber.Services;
using SyncSaber.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Zenject;

namespace SyncSaber.Installers
{
    public class SyncSaberInstaller : MonoInstaller
    {
        public override void InstallBindings()
        {
            this.Container.Bind<SyncSaber.SyncSaberFactory>().AsSingle();
            this.Container.BindViewController<SyncSaberController>();
            this.Container.Bind<SyncSaberService>().FromNewComponentOnNewGameObject().AsCached().NonLazy();
        }
    }
}
