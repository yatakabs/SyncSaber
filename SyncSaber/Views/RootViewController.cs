using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace SyncSaber.Views
{
    public class RootViewController : MonoBehaviour, IInitializable
    {
        [Inject]
        SyncSaberController syncSaber;

        public void Initialize()
        {
            syncSaber.Initialize();
        }
    }
}
