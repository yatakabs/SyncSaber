using BeatSaberMarkupLanguage.FloatingScreen;
using SyncSaber.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Zenject;

namespace SyncSaber.Services
{
    public class SyncSaberService : MonoBehaviour, IInitializable
    {
        public FloatingScreen Screen { get; set; }

        void Awake()
        {
            DontDestroyOnLoad(this.gameObject);
        }

        [Inject]
        SyncSaberController syncSaberController;

        public void Initialize()
        {
            
        }
    }
}
