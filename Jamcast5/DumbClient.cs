using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jamcast5
{
    /// <summary>
    /// The simplest of the run modes.  It makes sure that OBS is installed, running, and has the obs-websockets plugin.
    /// </summary>
    class DumbClient : IRunMode
    {
        ProgressForm progress = new ProgressForm();

        public ProgressForm Progress {
            get => progress ?? (progress = new ProgressForm());
            set => progress = value;
        }

        EventWaitHandle waitEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        public void Run()
        {
            var obs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio");
            var ws_plugin = Path.Combine(obs, "obs-plugins", "64bit", "obs-websocket.dll");
            if (!File.Exists(ws_plugin))
            {
                InstallOBS(obs);
                return;
            }
            LaunchObs(obs);
        }

        private void InstallOBS(string obs)
        {
            var verb = Directory.Exists(obs) ? "Updating" : "Installing";
            Progress.SetProgress($"{verb} OBS", 0);
            var wc = new WebClient();
            wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
            wc.DownloadFileCompleted += (s,e) => {
                //Progress.UnsetProgress();
                Process.Start("obs.exe", "/S").WaitForExit();
                InstallOBSWS(obs);
            };
            wc.DownloadFileAsync(new Uri("https://github.com/jp9000/obs-studio/releases/download/21.0.1/OBS-Studio-21.0.1-Full-Installer.exe"), "obs.exe");
        }

        private void InstallOBSWS(string obs)
        {
            var wc = new WebClient();
            wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
            wc.DownloadFileCompleted += (s, e) =>
            {
                //Progress.UnsetProgress();
                Process.Start("obs-websocket.exe", "/SILENT").WaitForExit();
                LaunchObs(obs);
            };
            Progress.SetProgress($"Installing obs-websocket plugin", 0);
            wc.DownloadFileAsync(new Uri("https://github.com/Palakis/obs-websocket/releases/download/4.3.1/obs-websocket-4.3.1-Windows-Installer.exe"), "obs-websocket.exe");
        }

        private void LaunchObs(string obs)
        {
            if (Process.GetProcessesByName("obs64").Length == 0)
            {
                Progress.SetProgress("Launching OBS", 0);
                //Progress.UnsetProgress();
                AcceptLicence();
                Thread.Sleep(TimeSpan.FromSeconds(1));
                string sixtyfour = Path.Combine(obs, "bin", "64bit");
                string obs_exe = Path.Combine(sixtyfour, "obs64.exe");
                Process.Start(new ProcessStartInfo(obs_exe)
                {
                    WorkingDirectory = sixtyfour,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            Progress.Close();
            Progress = null;
        }

        private void AcceptLicence()
        {
            var ini = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio", "global.ini");
            if (!File.Exists(ini))
            {
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio"));
                File.WriteAllLines(ini, new string[]
                {
                    "[General]",
                    "LicenseAccepted=true",
                    "FirstRun=true",
                });
            }
        }

        private void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Progress.SetProgress(null, e.ProgressPercentage);
        }
    }
}
