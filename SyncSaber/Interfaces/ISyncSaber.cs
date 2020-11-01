using System;
using System.Threading.Tasks;
using Zenject;

namespace SyncSaber.Interfaces
{
    public interface ISyncSaber : IInitializable, IDisposable
    {
        event Action<string> NotificationTextChange;
        Task Sync();
        void StartTimer();
        void SetEvent();
    }
}
