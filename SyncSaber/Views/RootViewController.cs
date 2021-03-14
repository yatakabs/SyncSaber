using UnityEngine;
using Zenject;

namespace SyncSaber.Views
{
    public class RootViewController : MonoBehaviour, IInitializable
    {
        [Inject]
        private readonly SyncSaberController syncSaber;

        public void Initialize()
        {
            this.syncSaber.Initialize();
        }
    }
}
