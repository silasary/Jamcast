using Newtonsoft.Json.Linq;
using OBSWebsocketDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Controller
{
    public partial class ControllerForm : Form
    {
        private readonly TcpListener _listener;
        private readonly Task _listenerTask;
        private readonly object _periodicRemoteClientConfigurationTask;
        private readonly HashSet<EndPoint> _remoteEndpoints;
        private readonly Dictionary<EndPoint, OBSWebsocket> _websockets;
        private readonly OBSWebsocket _controllerObs;
        private readonly Task _controllerTask;
        private bool _currentInputIsPrimary;
        private bool _needsPrepIntoStandby;
        private EndPoint _standbyInput;
        private EndPoint _currentInput;
        private Random _random;

        public ControllerForm()
        {
            InitializeComponent();

            _random = new Random();

            _remoteEndpoints = new HashSet<EndPoint>();
            _websockets = new Dictionary<EndPoint, OBSWebsocket>();
            _listener = new TcpListener(IPAddress.Any, 8080);
            _listenerTask = Task.Run(RunListener);

            _standbyInput = null;
            _currentInput = null;
            _currentInputIsPrimary = false;
            _needsPrepIntoStandby = true;

            _periodicRemoteClientConfigurationTask = Task.Run(RemoteClientConfiguration);

            _controllerObs = new OBSWebsocket();
            RegisterWebsocketEvents(_controllerObs);
            _controllerObs.Connect("ws://localhost:4444", null);

            _controllerTask = Task.Run(ControllerTask);
        }

        // This task is responsible for controlling the local OBS which has VLC sources
        // and receives inputs, transitioning between different inputs as needed.
        private async Task ControllerTask()
        {
            while (true)
            {
                try
                {
                    _controllerObs.SendRequest("SetSceneItemProperties", JObject.FromObject(new
                    {
                        item = "Primary Input Source",
                        visible = false,
                    }));
                    _controllerObs.SendRequest("SetSceneItemProperties", JObject.FromObject(new
                    {
                        item = "Secondary Input Source",
                        visible = true,
                    }));
                    _controllerObs.TransitionToProgram();

                    var a = _currentInput;
                    _currentInput = _standbyInput;
                    _standbyInput = a;

                    UpdateStatus();

                    _currentInputIsPrimary = false;

                    await Task.Delay(1500);

                    _needsPrepIntoStandby = true;

                    await Task.Delay(28500);

                    _controllerObs.SendRequest("SetSceneItemProperties", JObject.FromObject(new
                    {
                        item = "Primary Input Source",
                        visible = true,
                    }));
                    _controllerObs.SendRequest("SetSceneItemProperties", JObject.FromObject(new
                    {
                        item = "Secondary Input Source",
                        visible = false,
                    }));
                    _controllerObs.TransitionToProgram();

                    a = _currentInput;
                    _currentInput = _standbyInput;
                    _standbyInput = a;

                    UpdateStatus();

                    _currentInputIsPrimary = true;

                    await Task.Delay(1500);

                    _needsPrepIntoStandby = true;

                    await Task.Delay(28500);
                }
                catch (Exception ex)
                {
                    Log(null, ex.ToString());
                }
            }
        }

        // This task goes through the client and configures their scenes, profiles, etc.
        // to be ready for streaming.
        private async Task RemoteClientConfiguration()
        {
            while (true)
            {
                try
                {
                    if (!_needsPrepIntoStandby)
                    {
                        await Task.Delay(500);
                        continue;
                    }

                    var endpoints = _remoteEndpoints.ToList();
                    if (endpoints.Count == 0)
                    {
                        // Can't prep anything - not enough clients.
                        _needsPrepIntoStandby = false;
                        continue;
                    }

                    if (endpoints.Count == 1)
                    {
                        // Tell our only endpoint to stream into the current display. This will look janky
                        // until another client is lined up.
                        _needsPrepIntoStandby = false;
                        continue;
                    }

                    if (_currentInput != null)
                    {
                        endpoints.Remove(_currentInput);
                    }

                    if (_standbyInput != null)
                    {
                        var oldWebsocket = _websockets[_standbyInput];
                        try
                        {
                            oldWebsocket.StopRecording();
                        }
                        catch { }
                    }

                    var nextIdx = _random.Next(0, endpoints.Count - 1);
                    var nextInput = endpoints[nextIdx];

                    // Tell the next endpoint to start streaming into the right slot.
                    var websocket = _websockets[nextInput];
                    if (_currentInputIsPrimary)
                    {
                        websocket.SetCurrentProfile("Jamcast-Secondary");
                    }
                    else
                    {
                        websocket.SetCurrentProfile("Jamcast-Primary");
                    }
                    _standbyInput = nextInput;
                    try
                    {
                        websocket.StartRecording();
                    }
                    catch { }
                    _needsPrepIntoStandby = false;

                    await Task.Delay(1000);
                }
                catch (Exception ex)
                {
                    Log(null, ex.ToString());
                }
            }
        }
        
        private void RegisterWebsocketEvents(OBSWebsocket websocket)
        {
            websocket.Connected += (sender, args) => Log(websocket, "Connected");
            websocket.Disconnected += (sender, args) => Log(websocket, "Disconnected");
            websocket.OBSExit += (sender, args) => Log(websocket, "OBSExit");
            websocket.PreviewSceneChanged += (sender, args) => Log(websocket, "PreviewSceneChanged");
            websocket.ProfileChanged += (sender, args) => Log(websocket, "ProfileChanged");
            websocket.ProfileListChanged += (sender, args) => Log(websocket, "ProfileListChanged");
            websocket.RecordingStateChanged += (sender, args) => Log(websocket, "RecordingStateChanged");
            websocket.ReplayBufferStateChanged += (sender, args) => Log(websocket, "ReplayBufferStateChanged");
            websocket.SceneChanged += (sender, args) => Log(websocket, "SceneChanged");
            websocket.SceneCollectionChanged += (sender, args) => Log(websocket, "SceneCollectionChanged");
            websocket.SceneCollectionListChanged += (sender, args) => Log(websocket, "SceneCollectionListChanged");
            websocket.SceneItemAdded += (sender, args, ev) => Log(websocket, "SceneItemAdded");
            websocket.SceneItemRemoved += (sender, args, ev) => Log(websocket, "SceneItemRemoved");
            websocket.SceneItemVisibilityChanged += (sender, args, ev) => Log(websocket, "SceneItemVisibilityChanged");
            websocket.SceneListChanged += (sender, args) => Log(websocket, "SceneListChanged");
            websocket.SourceOrderChanged += (sender, args) => Log(websocket, "SourceOrderChanged");
            websocket.StreamingStateChanged += (sender, args) => Log(websocket, "StreamingStateChanged");
            websocket.StreamStatus += (sender, args) => Log(websocket, "StreamStatus");
            websocket.StudioModeSwitched += (sender, args) => Log(websocket, "StudioModeSwitched");
            websocket.TransitionBegin += (sender, args) => Log(websocket, "TransitionBegin");
            websocket.TransitionChanged += (sender, args) => Log(websocket, "TransitionChanged");
            websocket.TransitionDurationChanged += (sender, args) => Log(websocket, "TransitionDurationChanged");
            websocket.TransitionListChanged += (sender, args) => Log(websocket, "TransitionListChanged");
        }

        private void Log(OBSWebsocket websocket, string ev)
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() =>
                {
                    Log(websocket, ev);
                }));
                return;
            }

            logMessages.Text += $"{websocket?.WSConnection?.Url?.ToString() ?? "core"}: {ev}" + Environment.NewLine;
            logMessages.SelectionLength = 0;
            logMessages.SelectionStart = logMessages.Text.Length;
            logMessages.ScrollToCaret();
        }

        private void UpdateStatus()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => { UpdateStatus(); }));
                return;
            }

            var status = "";
            foreach (var client in _remoteEndpoints)
            {
                var suffix = "";
                if (client.Equals(_currentInput))
                {
                    suffix += " (Active)";
                }
                if (client.Equals(_standbyInput))
                {
                    suffix += " (Standby)";
                }
                status += client.ToString() + suffix + Environment.NewLine;
            }

            controllerStatus.Text = status;
        }

        private async Task RunListener()
        {
            _listener.Start();
            while (true)
            {
                var client = await _listener.AcceptTcpClientAsync();
                if (!_remoteEndpoints.Contains(client.Client.RemoteEndPoint))
                {
                    _remoteEndpoints.Add(client.Client.RemoteEndPoint);
                    var webSocketEndpoint = new IPEndPoint(((IPEndPoint)client.Client.RemoteEndPoint).Address, 4444);
                    var websocket = new OBSWebsocket();
                    _websockets[client.Client.RemoteEndPoint] = websocket;
                    RegisterWebsocketEvents(websocket);
                    websocket.Connect("ws://" + webSocketEndpoint.ToString(), null);
                    UpdateStatus();
                }
#pragma warning disable CS4014
                Task.Run(async () =>
                {
                    try
                    {
                        while (client.Connected)
                        {
                            if (client.Client.Poll(0, SelectMode.SelectRead))
                            {
                                byte[] buff = new byte[1];
                                if (client.Client.Receive(buff, SocketFlags.Peek) == 0)
                                {
                                    _remoteEndpoints.Remove(client.Client.RemoteEndPoint);
                                    _websockets[client.Client.RemoteEndPoint].Disconnect();
                                    UpdateStatus();
                                    return;
                                }
                            }

                            await Task.Delay(500);
                        }
                    }
                    catch
                    {
                        try
                        {
                            client.Client.Disconnect(true);
                        }
                        catch
                        {
                        }

                        _remoteEndpoints.Remove(client.Client.RemoteEndPoint);
                        _websockets[client.Client.RemoteEndPoint].Disconnect();
                        UpdateStatus();
                    }
                });
#pragma warning restore CS4014
            }
        }
    }
}
