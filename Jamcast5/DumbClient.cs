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
        LogForm logForm;
        string logs;

        private bool hasWrittenProfile = false;

        public ProgressForm Progress
        {
            get => progress ?? (progress = new ProgressForm());
            set => progress = value;
        }

        private void Log(string log)
        {
            logs += log + Environment.NewLine;

            if (logForm != null)
            {
                if (logForm.InvokeRequired)
                {
                    logForm.Invoke(new Action(() =>
                    {
                        logForm.logBox.Text = logs;
                        logForm.logBox.SelectionLength = 0;
                        logForm.logBox.SelectionStart = logForm.logBox.Text.Length;
                        logForm.logBox.ScrollToCaret();
                    }));
                }
                else
                {
                    logForm.logBox.Text = logs;
                    logForm.logBox.SelectionLength = 0;
                    logForm.logBox.SelectionStart = logForm.logBox.Text.Length;
                    logForm.logBox.ScrollToCaret();
                }
            }
        }

        public DumbClient()
        {
            logs = "";

            Log("Creating tray icon");
            trayIcon = new NotifyIcon()
            {
                Icon = Resources.AppIcon,
                ContextMenu = new ContextMenu(new MenuItem[]
                {
                    new MenuItem("Show Logs", ShowLogs),
                    new MenuItem("Exit", Exit)
                }),
                Visible = true
            };
        }

        private void ShowLogs(object sender, EventArgs e)
        {
            if (logForm == null)
            {
                logForm = new LogForm();
            }

            logForm.FormClosed += (s, ee) =>
            {
                logForm.Dispose();
                logForm = null;
            };
            logForm.Show();

            Log("Showing log window");
        }

        internal void Notify(string boop)
        {
            try
            {
                trayIcon.ShowBalloonTip(1000, "Jamcast", boop, ToolTipIcon.Info);
            }
            catch (Exception)
            {

            }
        }



        private void Exit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;

            Application.Exit();
        }

        EventWaitHandle waitEvent = new EventWaitHandle(false, EventResetMode.AutoReset);

        public void Run()
        {
            Log("Starting launch sequence");
            var obs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "obs-studio");
            var ws_plugin = Path.Combine(obs, "obs-plugins", "64bit", "obs-websocket.dll");
            Log("Checking if OBS WebSocket plugin exists at: " + ws_plugin);
            if (!File.Exists(ws_plugin))
            {
                Log("It does not, installing or upgrading OBS");
                InstallOBS(obs);
                return;
            }
            Log("It does, scheduling launch of OBS after client connect");
            LaunchObs(obs, false);
            Task.Factory.StartNew(async () =>
            {
                var wc = new WebClient();
                var tc = new TcpClient();

                JToken currentEndpoint = null;
                DateTime lastPull = new DateTime();

                Log("Looping while trying to connect to controller");
                while (true)
                {
                    try
                    {
                        JToken endpoint = currentEndpoint;
                        if (DateTime.Now.Subtract(lastPull).TotalMinutes > 5)
                        {
                            var json = await wc.DownloadStringTaskAsync("https://melb18.jamhost.org/jamcast/ip");
                            endpoint = JToken.Parse(json);

                            Log("Controller endpoint is now: " + endpoint);
                        }

                        if (tc.Connected)
                        {
                            Log("TCP connection to controller is established");
                            
                            if (!endpoint.Equals(currentEndpoint))
                            {
                                Log("Endpoint has changed, disconnecting");

                                // Endpoint changed, reconnect.
                                tc.Client.Disconnect(true);
                                tc = new TcpClient();
                                continue;
                            }
                            else if (tc.Client.Poll(0, SelectMode.SelectRead))
                            {
                                Log("Checking if we're still live on the TCP connection");

                                byte[] buff = new byte[1];
                                try
                                {
                                    if (tc.Client.Receive(buff, SocketFlags.Peek) == 0)
                                    {
                                        Log("We are not, disconnecting");

                                        // We have actively lost connection to the server, attempt reconnect.
                                        tc.Client.Disconnect(true);
                                        tc = new TcpClient();
                                        continue;
                                    }
                                    else
                                    {
                                        Log("I'm doing science and I'm still alive");
                                    }
                                }
                                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                                {
                                    Log("We are not, disconnecting");
                                    
                                    // We have actively lost connection to the server, attempt reconnect.
                                    tc.Client.Disconnect(true);
                                    tc = new TcpClient();
                                    continue;
                                }
                            }

                            await Task.Delay(500);

                            try
                            {
                                tc.Client.Send(new byte[] { 1 });
                            }
                            catch (Exception ex)
                            {
                                Log("Unable to send data to controller");
                                Log(ex.ToString());
                            }
                        }
                        else if (endpoint.Type != JTokenType.Null)
                        {
                            Log("TCP connection is not established, but there is an endpoint");

                            try
                            {
                                Log("Establishing connection...");
                                IPEndPoint remoteEP = ParseIPEndPoint(endpoint.Value<string>());

                                tc.Dispose();
                                tc = new TcpClient();
                                //Notify($"Connecting to Jamhost controller at {remoteEP}");
                                var result = tc.BeginConnect(remoteEP.Address, remoteEP.Port, null, null);
                                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(5));
                                if (!success)
                                {
                                    Log("Unable to connect to controller within 5 seconds!");

                                    try { tc.EndConnect(result); } catch { }
                                    tc.Dispose();
                                    tc = new TcpClient();
                                    continue;
                                }

                                tc.EndConnect(result);
                                var dirty = WriteProfile(remoteEP);
                                hasWrittenProfile = true;
                                if (dirty)
                                {
                                    LaunchObs(obs, true);
                                }

                                Log("Connection established");
                            }
                            catch (Exception ex)
                            {
                                Log(ex.ToString());

                                // Shrug!
                                await Task.Delay(5000);
                            }
                        }
                        currentEndpoint = endpoint;
                    }
                    catch (Exception ex)
                    {
                        Log(ex.ToString());
                    }
                }
            });
        }

        private bool WriteProfile(IPEndPoint remoteEP)
        {
            Log("Writing OBS profiles for " + remoteEP.ToString());
            var ip = remoteEP.Address.ToString();
            var dirty = false;
            dirty = WriteProfile("Primary", ip, 1234) || dirty;
            dirty = WriteProfile("Secondary", ip, 1235) || dirty;
            dirty = WriteScene("Untitled") || dirty;
            return dirty;
        }

        private static bool WriteProfile(string suffix, string ip, int port)
        {
            var name = $"Jamcast-{suffix}";
            var dname = $"Jamcast{suffix}";
            var profiledir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio", "basic", "profiles");

            Directory.CreateDirectory(Path.Combine(profiledir, dname));
            string[] contents = new string[]
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
                };
            string path = Path.Combine(profiledir, dname, "basic.ini");
            if (!File.Exists(path) || !string.Join("|", File.ReadAllLines(path)).Equals(string.Join("|", contents)))
            {
                File.WriteAllLines(path, contents);
                return true;
            }
            return false;
        }

        private static bool WriteScene(string name)
        {
            var scenedir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "obs-studio", "basic", "scenes");
            Directory.CreateDirectory(Path.Combine(scenedir));
            var sceneJson = Path.Combine(scenedir, name + ".json");
            if (!File.Exists(sceneJson))
            {
                File.WriteAllLines(sceneJson, new[]
                {
                    "{\r\n    \"DesktopAudioDevice1\": {\r\n        \"deinterlace_field_order\": 0,\r\n        \"deinterlace_mode\": 0,\r\n        \"enabled\": true,\r\n        \"flags\": 0,\r\n        \"hotkeys\": {\r\n            \"libobs.mute\": [],\r\n            \"libobs.push-to-mute\": [],\r\n            \"libobs.push-to-talk\": [],\r\n            \"libobs.unmute\": []\r\n        },\r\n        \"id\": \"wasapi_output_capture\",\r\n        \"mixers\": 255,\r\n        \"monitoring_type\": 0,\r\n        \"muted\": false,\r\n        \"name\": \"Desktop Audio\",\r\n        \"private_settings\": {},\r\n        \"push-to-mute\": false,\r\n        \"push-to-mute-delay\": 0,\r\n        \"push-to-talk\": false,\r\n        \"push-to-talk-delay\": 0,\r\n        \"settings\": {\r\n            \"device_id\": \"default\"\r\n        },\r\n        \"sync\": 0,\r\n        \"volume\": 1.0\r\n    },\r\n    \"current_program_scene\": \"Scene\",\r\n    \"current_scene\": \"Scene\",\r\n    \"current_transition\": \"Fade\",\r\n    \"modules\": {\r\n        \"auto-scene-switcher\": {\r\n            \"active\": false,\r\n            \"interval\": 300,\r\n            \"non_matching_scene\": \"\",\r\n            \"switch_if_not_matching\": false,\r\n            \"switches\": []\r\n        },\r\n        \"captions\": {\r\n            \"enabled\": false,\r\n            \"lang_id\": 1033,\r\n            \"provider\": \"mssapi\",\r\n            \"source\": \"\"\r\n        },\r\n        \"output-timer\": {\r\n            \"autoStartRecordTimer\": false,\r\n            \"autoStartStreamTimer\": false,\r\n            \"recordTimerHours\": 0,\r\n            \"recordTimerMinutes\": 0,\r\n            \"recordTimerSeconds\": 30,\r\n            \"streamTimerHours\": 0,\r\n            \"streamTimerMinutes\": 0,\r\n            \"streamTimerSeconds\": 30\r\n        },\r\n        \"scripts-tool\": []\r\n    },\r\n    \"name\": \"Untitled\",\r\n    \"preview_locked\": false,\r\n    \"quick_transitions\": [\r\n        {\r\n            \"duration\": 300,\r\n            \"hotkeys\": [],\r\n            \"id\": 1,\r\n            \"name\": \"Cut\"\r\n        },\r\n        {\r\n            \"duration\": 300,\r\n            \"hotkeys\": [],\r\n            \"id\": 2,\r\n            \"name\": \"Fade\"\r\n        }\r\n    ],\r\n    \"saved_multiview_projectors\": [\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        }\r\n    ],\r\n    \"saved_preview_projectors\": [\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        }\r\n    ],\r\n    \"saved_projectors\": [\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        }\r\n    ],\r\n    \"saved_studio_preview_projectors\": [\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        }\r\n    ],\r\n    \"scaling_enabled\": false,\r\n    \"scaling_level\": 0,\r\n    \"scaling_off_x\": 0.0,\r\n    \"scaling_off_y\": 0.0,\r\n    \"scene_order\": [\r\n        {\r\n            \"name\": \"Scene\"\r\n        }\r\n    ],\r\n    \"sources\": [\r\n        {\r\n            \"deinterlace_field_order\": 0,\r\n            \"deinterlace_mode\": 0,\r\n            \"enabled\": true,\r\n            \"flags\": 0,\r\n            \"hotkeys\": {\r\n                \"OBSBasic.SelectScene\": [],\r\n                \"libobs.hide_scene_item.Display Capture\": [],\r\n                \"libobs.show_scene_item.Display Capture\": []\r\n            },\r\n            \"id\": \"scene\",\r\n            \"mixers\": 0,\r\n            \"monitoring_type\": 0,\r\n            \"muted\": false,\r\n            \"name\": \"Scene\",\r\n            \"private_settings\": {},\r\n            \"push-to-mute\": false,\r\n            \"push-to-mute-delay\": 0,\r\n            \"push-to-talk\": false,\r\n            \"push-to-talk-delay\": 0,\r\n            \"settings\": {\r\n                \"id_counter\": 1,\r\n                \"items\": [\r\n                    {\r\n                        \"align\": 5,\r\n                        \"bounds\": {\r\n                            \"x\": 0.0,\r\n                            \"y\": 0.0\r\n                        },\r\n                        \"bounds_align\": 0,\r\n                        \"bounds_type\": 0,\r\n                        \"crop_bottom\": 0,\r\n                        \"crop_left\": 0,\r\n                        \"crop_right\": 0,\r\n                        \"crop_top\": 0,\r\n                        \"id\": 1,\r\n                        \"locked\": false,\r\n                        \"name\": \"Display Capture\",\r\n                        \"pos\": {\r\n                            \"x\": 0.0,\r\n                            \"y\": 0.0\r\n                        },\r\n                        \"private_settings\": {},\r\n                        \"rot\": 0.0,\r\n                        \"scale\": {\r\n                            \"x\": 1.0,\r\n                            \"y\": 1.0\r\n                        },\r\n                        \"scale_filter\": \"disable\",\r\n                        \"visible\": true\r\n                    }\r\n                ]\r\n            },\r\n            \"sync\": 0,\r\n            \"volume\": 1.0\r\n        },\r\n        {\r\n            \"deinterlace_field_order\": 0,\r\n            \"deinterlace_mode\": 0,\r\n            \"enabled\": true,\r\n            \"flags\": 0,\r\n            \"hotkeys\": {},\r\n            \"id\": \"monitor_capture\",\r\n            \"mixers\": 0,\r\n            \"monitoring_type\": 0,\r\n            \"muted\": false,\r\n            \"name\": \"Display Capture\",\r\n            \"private_settings\": {},\r\n            \"push-to-mute\": false,\r\n            \"push-to-mute-delay\": 0,\r\n            \"push-to-talk\": false,\r\n            \"push-to-talk-delay\": 0,\r\n            \"settings\": {\r\n                \"monitor\": 1\r\n            },\r\n            \"sync\": 0,\r\n            \"volume\": 1.0\r\n        }\r\n    ],\r\n    \"transition_duration\": 300,\r\n    \"transitions\": []\r\n}",
                });
                return true;
            }
            return false;
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
                LaunchObs(obs, true);
            };
            Progress.SetProgress($"Installing obs-websocket plugin", 0);
            wc.DownloadFileAsync(new Uri("https://github.com/Palakis/obs-websocket/releases/download/4.3.1/obs-websocket-4.3.1-Windows-Installer.exe"), "obs-websocket.exe");
        }

        private void LaunchObs(string obs, bool force)
        {
            Task.Run(async () =>
            {
                int attempts = 0;
                while (!hasWrittenProfile)
                {
                    progress.UnsetProgress("Connecting to Controller...");
                    await Task.Delay(1000);
                    if (attempts++ > 300)
                        Application.Restart();
                    continue;
                }

                Log("We have written OBS profiles, now launching OBS");

                if (force)
                {
                    Process[] obses = Process.GetProcessesByName("obs64");
                    foreach (var p in obses)
                    {
                        try
                        {
                            p.Kill();
                        }
                        catch (Exception)
                        {
                            // Shrug~
                        }
                    }
                }

                lock (obs)
                {
                    if (Process.GetProcessesByName("obs64").Length == 0)
                    {
                        //Progress.SetProgress("Launching OBS", 0);
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