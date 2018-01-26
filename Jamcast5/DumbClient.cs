using Jamcast5.Properties;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Jamcast5
{
    /// <summary>
    /// The simplest of the run modes.  It makes sure that OBS is installed, running, and has the obs-websockets plugin.
    /// </summary>
    class DumbClient : ApplicationContext, IRunMode
    {
        ProgressForm progress = new ProgressForm();
        NotifyIcon trayIcon;

        private bool hasWrittenProfile = false;

        public ProgressForm Progress
        {
            get => progress ?? (progress = new ProgressForm());
            set => progress = value;
        }

        public DumbClient()
        {
            trayIcon = new NotifyIcon()
            {
                Icon = Resources.AppIcon,
                ContextMenu = new ContextMenu(new MenuItem[]
                {
                    new MenuItem("Exit", Exit)
                }),
                Visible = true
            };
        }

        private void Exit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;

            Application.Exit();
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
            Task.Factory.StartNew(async () =>
            {
                Thread.CurrentThread.IsBackground = true;
                var wc = new WebClient();
                var tc = new TcpClient();

                JToken currentEndpoint = null;
                DateTime lastPull = new DateTime();

                while (true)
                {
                    JToken endpoint = currentEndpoint;
                    if (DateTime.Now.Subtract(lastPull).TotalMinutes > 5)
                    {
                        var json = await wc.DownloadStringTaskAsync("https://melb18.jamhost.org/jamcast/ip");
                        endpoint = JToken.Parse(json);
                    }

                    if (tc.Connected)
                    {
                        if (endpoint != currentEndpoint)
                        {
                            // Endpoint changed, reconnect.
                            tc.Client.Disconnect(true);
                            tc = new TcpClient();
                            continue;
                        }
                        else if (tc.Client.Poll(0, SelectMode.SelectRead))
                        {
                            byte[] buff = new byte[1];
                            if (tc.Client.Receive(buff, SocketFlags.Peek) == 0)
                            {
                                // We have actively lost connection to the server, attempt reconnect.
                                tc.Client.Disconnect(true);
                                tc = new TcpClient();
                                continue;
                            }
                        }

                        await Task.Delay(500);
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
                            hasWrittenProfile = true;
                        }
                        catch (Exception)
                        {
                            // Shrug!
                            await Task.Delay(5000);
                        }
                    }
                    currentEndpoint = endpoint;
                }
            });
        }

        private void WriteProfile(IPEndPoint remoteEP)
        {
            var ip = remoteEP.Address.ToString();
            WriteProfile("Primary", ip, 1234);
            WriteProfile("Secondary", ip, 1235);
            WriteScene("Untitled");
        }

        private static void WriteProfile(string suffix, string ip, int port)
        {
            var name = $"Jamcast-{suffix}";
            var dname = $"Jamcast{suffix}";
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
                    $"FFURL=udp://{ip}:{port}",
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

        private static void WriteScene(string name)
        {
            var scenedir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio", "basic", "scenes");
            var sceneJson = Path.Combine(scenedir, name + ".json");
            if (!File.Exists(sceneJson))
            {
                File.WriteAllLines(sceneJson, new[]
                {
                    "{\r\n    \"DesktopAudioDevice1\": {\r\n        \"deinterlace_field_order\": 0,\r\n        \"deinterlace_mode\": 0,\r\n        \"enabled\": true,\r\n        \"flags\": 0,\r\n        \"hotkeys\": {\r\n            \"libobs.mute\": [],\r\n            \"libobs.push-to-mute\": [],\r\n            \"libobs.push-to-talk\": [],\r\n            \"libobs.unmute\": []\r\n        },\r\n        \"id\": \"wasapi_output_capture\",\r\n        \"mixers\": 255,\r\n        \"monitoring_type\": 0,\r\n        \"muted\": false,\r\n        \"name\": \"Desktop Audio\",\r\n        \"private_settings\": {},\r\n        \"push-to-mute\": false,\r\n        \"push-to-mute-delay\": 0,\r\n        \"push-to-talk\": false,\r\n        \"push-to-talk-delay\": 0,\r\n        \"settings\": {\r\n            \"device_id\": \"default\"\r\n        },\r\n        \"sync\": 0,\r\n        \"volume\": 1.0\r\n    },\r\n    \"current_program_scene\": \"Scene\",\r\n    \"current_scene\": \"Scene\",\r\n    \"current_transition\": \"Fade\",\r\n    \"modules\": {\r\n        \"auto-scene-switcher\": {\r\n            \"active\": false,\r\n            \"interval\": 300,\r\n            \"non_matching_scene\": \"\",\r\n            \"switch_if_not_matching\": false,\r\n            \"switches\": []\r\n        },\r\n        \"captions\": {\r\n            \"enabled\": false,\r\n            \"lang_id\": 1033,\r\n            \"provider\": \"mssapi\",\r\n            \"source\": \"\"\r\n        },\r\n        \"output-timer\": {\r\n            \"autoStartRecordTimer\": false,\r\n            \"autoStartStreamTimer\": false,\r\n            \"recordTimerHours\": 0,\r\n            \"recordTimerMinutes\": 0,\r\n            \"recordTimerSeconds\": 30,\r\n            \"streamTimerHours\": 0,\r\n            \"streamTimerMinutes\": 0,\r\n            \"streamTimerSeconds\": 30\r\n        },\r\n        \"scripts-tool\": []\r\n    },\r\n    \"name\": \"Untitled\",\r\n    \"preview_locked\": false,\r\n    \"quick_transitions\": [\r\n        {\r\n            \"duration\": 300,\r\n            \"hotkeys\": [],\r\n            \"id\": 1,\r\n            \"name\": \"Cut\"\r\n        },\r\n        {\r\n            \"duration\": 300,\r\n            \"hotkeys\": [],\r\n            \"id\": 2,\r\n            \"name\": \"Fade\"\r\n        }\r\n    ],\r\n    \"saved_multiview_projectors\": [\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        }\r\n    ],\r\n    \"saved_preview_projectors\": [\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        }\r\n    ],\r\n    \"saved_projectors\": [\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        }\r\n    ],\r\n    \"saved_studio_preview_projectors\": [\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        }\r\n    ],\r\n    \"scaling_enabled\": false,\r\n    \"scaling_level\": 0,\r\n    \"scaling_off_x\": 0.0,\r\n    \"scaling_off_y\": 0.0,\r\n    \"scene_order\": [\r\n        {\r\n            \"name\": \"Scene\"\r\n        }\r\n    ],\r\n    \"sources\": [\r\n        {\r\n            \"deinterlace_field_order\": 0,\r\n            \"deinterlace_mode\": 0,\r\n            \"enabled\": true,\r\n            \"flags\": 0,\r\n            \"hotkeys\": {\r\n                \"OBSBasic.SelectScene\": [],\r\n                \"libobs.hide_scene_item.Display Capture\": [],\r\n                \"libobs.show_scene_item.Display Capture\": []\r\n            },\r\n            \"id\": \"scene\",\r\n            \"mixers\": 0,\r\n            \"monitoring_type\": 0,\r\n            \"muted\": false,\r\n            \"name\": \"Scene\",\r\n            \"private_settings\": {},\r\n            \"push-to-mute\": false,\r\n            \"push-to-mute-delay\": 0,\r\n            \"push-to-talk\": false,\r\n            \"push-to-talk-delay\": 0,\r\n            \"settings\": {\r\n                \"id_counter\": 1,\r\n                \"items\": [\r\n                    {\r\n                        \"align\": 5,\r\n                        \"bounds\": {\r\n                            \"x\": 0.0,\r\n                            \"y\": 0.0\r\n                        },\r\n                        \"bounds_align\": 0,\r\n                        \"bounds_type\": 0,\r\n                        \"crop_bottom\": 0,\r\n                        \"crop_left\": 0,\r\n                        \"crop_right\": 0,\r\n                        \"crop_top\": 0,\r\n                        \"id\": 1,\r\n                        \"locked\": false,\r\n                        \"name\": \"Display Capture\",\r\n                        \"pos\": {\r\n                            \"x\": 0.0,\r\n                            \"y\": 0.0\r\n                        },\r\n                        \"private_settings\": {},\r\n                        \"rot\": 0.0,\r\n                        \"scale\": {\r\n                            \"x\": 1.0,\r\n                            \"y\": 1.0\r\n                        },\r\n                        \"scale_filter\": \"disable\",\r\n                        \"visible\": true\r\n                    }\r\n                ]\r\n            },\r\n            \"sync\": 0,\r\n            \"volume\": 1.0\r\n        },\r\n        {\r\n            \"deinterlace_field_order\": 0,\r\n            \"deinterlace_mode\": 0,\r\n            \"enabled\": true,\r\n            \"flags\": 0,\r\n            \"hotkeys\": {},\r\n            \"id\": \"monitor_capture\",\r\n            \"mixers\": 0,\r\n            \"monitoring_type\": 0,\r\n            \"muted\": false,\r\n            \"name\": \"Display Capture\",\r\n            \"private_settings\": {},\r\n            \"push-to-mute\": false,\r\n            \"push-to-mute-delay\": 0,\r\n            \"push-to-talk\": false,\r\n            \"push-to-talk-delay\": 0,\r\n            \"settings\": {\r\n                \"monitor\": 1\r\n            },\r\n            \"sync\": 0,\r\n            \"volume\": 1.0\r\n        }\r\n    ],\r\n    \"transition_duration\": 300,\r\n    \"transitions\": []\r\n}",
                });
            }
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
            Task.Run(async () =>
            {
                while (!hasWrittenProfile)
                {
                    await Task.Delay(1000);
                    continue;
                }

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
            });
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