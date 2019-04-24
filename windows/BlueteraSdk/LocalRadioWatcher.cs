using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;

namespace Bluetera
{
    // Note - experimental code - use at your own risk
    public class LocalRadioWatcher
    {
        #region Types / Constants
        private const string BleServiceName = "BthEnum";
        public enum StateType { Attached, Detached };
        #endregion

        #region Fields
        private bool _isRunning = false;
        private ManagementEventWatcher _attachWatcher;
        private ManagementEventWatcher _detachWatcher;
        private static readonly Lazy<LocalRadioWatcher> _instance = new Lazy<LocalRadioWatcher>(() => new LocalRadioWatcher());
        #endregion

        #region Events
        public event TypedEventHandler<LocalRadioWatcher, StateType> LocalRadioStateChanged;
        #endregion

        #region Properties
        public static LocalRadioWatcher Instance
        {
            get { return _instance.Value; }
        }
        #endregion

        #region Methods
        public void Start()
        {
            if (_isRunning)
                return;

            // subscribe to events
            _attachWatcher = new ManagementEventWatcher("SELECT * FROM __InstanceCreationEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity' and TargetInstance.service ='BthEnum'");
            _detachWatcher = new ManagementEventWatcher("SELECT * FROM __InstanceDeletionEvent WITHIN 2 WHERE TargetInstance ISA 'Win32_PnPEntity' and TargetInstance.service ='BthEnum'");

            _attachWatcher.EventArrived += new EventArrivedEventHandler(OnInstanceCreation);
            _detachWatcher.EventArrived += new EventArrivedEventHandler(OnInstanceDeletion);

            _attachWatcher.Start();
            _detachWatcher.Start();

            _isRunning = true;

            // see if already attached
            if (IsLocalRadioAttached())
                LocalRadioStateChanged?.Invoke(this, StateType.Attached);
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _attachWatcher.Dispose();
            _detachWatcher.Dispose();
            LocalRadioStateChanged = null;
        }

        public bool IsLocalRadioAttached()
        {
            SelectQuery sq = new SelectQuery("SELECT DeviceId FROM Win32_PnPEntity WHERE service='BthLEEnum'");
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(sq))
            {
                return (searcher.Get().Count > 0);
            }
        }
        #endregion

        #region Event handlers
        private void OnInstanceCreation(object obj, EventArrivedEventArgs e)
        {
            try
            {
                LocalRadioStateChanged?.Invoke(this, StateType.Attached);
            }
            catch (Exception) { /* Currently ignore all errors*/ }
        }

        private void OnInstanceDeletion(object obj, EventArrivedEventArgs e)
        {
            try
            {
                LocalRadioStateChanged?.Invoke(this,StateType.Detached);
            }
            catch (Exception) { /* Currently ignore all errors*/ }
        }
        #endregion        
    }
}
