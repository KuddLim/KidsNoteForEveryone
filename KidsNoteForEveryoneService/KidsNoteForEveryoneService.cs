using LibKidsNoteForEveryone;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace KidsNoteForEveryoneService
{
    public enum ServiceState
    {
        SERVICE_STOPPED = 0x00000001,
        SERVICE_START_PENDING = 0x00000002,
        SERVICE_STOP_PENDING = 0x00000003,
        SERVICE_RUNNING = 0x00000004,
        SERVICE_CONTINUE_PENDING = 0x00000005,
        SERVICE_PAUSE_PENDING = 0x00000006,
        SERVICE_PAUSED = 0x00000007,
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ServiceStatus
    {
        public long dwServiceType;
        public ServiceState dwCurrentState;
        public long dwControlsAccepted;
        public long dwWin32ExitCode;
        public long dwServiceSpecificExitCode;
        public long dwCheckPoint;
        public long dwWaitHint;
    };


    public partial class KidsNoteForEveryoneService : ServiceBase
    {
        private KidsNoteNotifierManager Manager;
        private HashSet<ContentType> MonitoringTypes;

#if !MONO
        private ServiceStatus Status = new ServiceStatus();

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool SetServiceStatus(IntPtr handle, ref ServiceStatus serviceStatus);
#endif

        public KidsNoteForEveryoneService()
        {
            InitializeComponent();

            MonitoringTypes = new HashSet<ContentType>() {
                ContentType.REPORT, ContentType.NOTICE, ContentType.ALBUM
            };
        }

        protected override void OnStart(string[] args)
        {
            System.Console.WriteLine("starting...");
            try
            {
                StartService();
            }
            catch (TargetInvocationException e)
            {
                System.Console.WriteLine("TargetInvocationException occured!!");
                System.Console.WriteLine(e.InnerException);
                System.Console.WriteLine(e.StackTrace);
            }
            catch (NotImplementedException e)
            {
                System.Console.WriteLine("NotImplementedException occured!!");
                System.Console.WriteLine(e.InnerException);
                System.Console.WriteLine(e.StackTrace);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("UnknownException occured!!");
                System.Console.WriteLine(e);
                System.Console.WriteLine(e.StackTrace);
            }
        }

        protected override void OnStop()
        {
            System.Console.WriteLine("stopping...");

            if (Manager != null)
            {
#if !MONO
                Status.dwCurrentState = ServiceState.SERVICE_PAUSE_PENDING;
                SetServiceStatus(this.ServiceHandle, ref Status);
#endif

                Manager.Cleanup();

#if !MONO
                Status.dwCurrentState = ServiceState.SERVICE_PAUSED;
                SetServiceStatus(this.ServiceHandle, ref Status);
#endif

                Manager = null;
            }
        }

        private void StartService()
        {
            if (Manager == null)
            {
#if !MONO
                Status.dwCurrentState = ServiceState.SERVICE_START_PENDING;
                SetServiceStatus(this.ServiceHandle, ref Status);
#endif

                Manager = new KidsNoteNotifierManager(MonitoringTypes);
                Manager.Startup();

#if !MONO
                Status.dwCurrentState = ServiceState.SERVICE_RUNNING;
                SetServiceStatus(this.ServiceHandle, ref Status);
#endif
            }
        }
    }
}
