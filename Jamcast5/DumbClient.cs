using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

        public ProgressForm Progress
        {
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
            Task.Factory.StartNew(() =>
            {
                Thread.CurrentThread.IsBackground = true;
                var wc = new WebClient();
                var tc = new TcpClient();

                JToken lastendpoint = null;

                while (true)
                {
                    var json = wc.DownloadString("https://melb18.jamhost.org/jamcast/ip");
                    var endpoint = JToken.Parse(json);
                    if (endpoint.Equals(lastendpoint))
                    {
                        Thread.Sleep(TimeSpan.FromMinutes(5));
                    }
                    else if (endpoint.Type != JTokenType.Null)
                    {
                        try
                        {

                            tc.Dispose();
                            tc = new TcpClient();
                            IPEndPoint remoteEP = ParseIPEndPoint(endpoint.Value<string>());
                            tc.Connect(remoteEP);
                            WriteProfile(remoteEP);
                        }
                        catch (Exception)
                        {
                            // Shrug!
                        }
                    }
                    lastendpoint = endpoint;
                }
            });
        }

        private void WriteProfile(IPEndPoint remoteEP)
        {
            var name = $"Jamcast-{remoteEP.Address.ToString()}";
            var dname = $"Jamcast{remoteEP.Address.ToString().Replace(".", "")}";
            var profiledir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio", "basic", "profiles");

            Directory.CreateDirectory(Path.Combine(profiledir, dname));
            File.WriteAllLines(Path.Combine(profiledir, dname, "basic.ini"),
                new string[]
                {
                    "[General]",
                    $"Name={name}",
                    "[Video]",
                    "BaseCX=1920",
                    "BaseCY=1080",
                    "OutputCX=1280",
                    "OutputCY=720",
                    "[Output]",
                    "Mode=Advanced",
                    "[AdvOut]",
                    "TrackIndex=1",
                    "RecType=FFmpeg",
                    "RecTracks=1",
                    "FFOutputToFile=false",
                    "FFURL=udp://10.17.39.23:1234",
                    "FFFormat=mpegts",
                    "FFFormatMimeType=video/MP2T",
                    "FFExtension=ts",
                    "FFIgnoreCompat=true",
                    "FFVEncoderId=28",
                    "FFVEncoder=libx264",
                    "FFAEncoderId=86018",
                    "FFAEncoder=aac",
                    "FFAudioTrack=1",
                });
        }

        private void InstallOBS(string obs)
        {
            var verb = Directory.Exists(obs) ? "Updating" : "Installing";
            Progress.SetProgress($"{verb} OBS", 0);
            var wc = new WebClient();
            wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
            wc.DownloadFileCompleted += (s, e) =>
            {
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

        // https://stackoverflow.com/questions/2727609/best-way-to-create-ipendpoint-from-string
        public static IPEndPoint ParseIPEndPoint(string endPoint)
        {
            string[] ep = endPoint.Split(':');
            if (ep.Length < 2) throw new FormatException("Invalid endpoint format");
            IPAddress ip;
            if (ep.Length > 2)
            {
                if (!IPAddress.TryParse(string.Join(":", ep, 0, ep.Length - 1), out ip))
                {
                    throw new FormatException("Invalid ip-adress");
                }
            }
            else
            {
                if (!IPAddress.TryParse(ep[0], out ip))
                {
                    throw new FormatException("Invalid ip-adress");
                }
            }
            int port;
            if (!int.TryParse(ep[ep.Length - 1], NumberStyles.None, NumberFormatInfo.CurrentInfo, out port))
            {
                throw new FormatException("Invalid port");
            }
            return new IPEndPoint(ip, port);
        }
    }
}