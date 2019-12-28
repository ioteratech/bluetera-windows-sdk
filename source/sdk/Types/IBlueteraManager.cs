using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Bluetera
{
    public interface IBlueteraManager
    {
        #region Events
        event TypedEventHandler<IBlueteraManager, BlueteraAdvertisement> AdvertismentReceived;
        #endregion

        #region Methods
        void StartScan();
        void StopScan();
        Task<IBlueteraDevice> Connect(ulong addr, bool autopair = false);
        #endregion
    }
}
