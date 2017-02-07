using AzureSync.Controllers;
using Microsoft.Owin.Hosting;
using System;
using System.Configuration;
using System.Configuration.Install;
using System.Linq;
using System.Reflection;
using System.ServiceProcess;
using System.IO;

namespace AzureSync
{
    public partial class Service1 : ServiceBase
    {
        public static readonly string InstallServiceName = "AzureSync";

        private IDisposable _webapp;

        public Service1()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            _webapp = WebApp.Start<Startup>(AzureSyncConfiguration.Current.ServiceUrl);

            var controller = new DefaultController();

            if (!controller.FolderExists())
                controller.CreateFolder();

            FolderWatcher.Current.Changed += Current_Changed;
            FolderWatcher.Current.Renamed += Current_Renamed;

            FolderWatcher.Current.Start();

            int count;

            count = controller.SyncAzureFiles();
            Program.ConsoleWriteLine("[{0:yyyy-MM-dd HH:mm:ss}] Azure sync: Files downloaded: {1}", DateTime.Now, count);

            //count = controller.SyncFolderFiles();
            //Program.ConsoleWriteLine("[{0:yyyy-MM-dd HH:mm:ss}] Folder sync: Files uploaded: {1}", DateTime.Now, count);
        }

        private void Current_Changed(object sender, FileSystemEventArgs e)
        {
            Program.ConsoleWriteLine("[{0:yyyy-MM-dd HH:mm:ss}] File: {1} {2}", DateTime.Now, e.FullPath, e.ChangeType);
        }

        private void Current_Renamed(object sender, RenamedEventArgs e)
        {
            Program.ConsoleWriteLine("File: {0} renamed to {1}", e.OldFullPath, e.FullPath);
        }

        protected override void OnStop()
        {
            if (_webapp != null)
                _webapp.Dispose();

            FolderWatcher.Current.Stop();
            FolderWatcher.Current.Dispose();
        }

        public void Start(string[] args)
        {
            OnStart(args);
            Program.ConsoleWriteLine("endpoint: {0}", AzureSyncConfiguration.Current.ServiceUrl);
            Program.ConsoleWriteLine("Press any key to exit.");
        }

        public static void InstallService()
        {
            if (IsServiceInstalled())
            {
                UninstallService();
            }

            ManagedInstallerClass.InstallHelper(new string[] { Assembly.GetExecutingAssembly().Location });
        }

        public static void UninstallService()
        {
            ManagedInstallerClass.InstallHelper(new string[] { "/u", Assembly.GetExecutingAssembly().Location });
        }

        public static bool IsServiceInstalled()
        {
            return ServiceController.GetServices().Any(s => s.ServiceName == InstallServiceName);
        }
    }
}
