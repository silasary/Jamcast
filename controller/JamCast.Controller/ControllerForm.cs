using HeyRed.MarkdownSharp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nowin;
using OBSWebsocketDotNet;
using SlackAPI;
using SlackAPI.WebSocketMessages;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Owin.Builder;
using Owin;
using Grpc.Core;
using Jamcast;

namespace JamCast.Controller
{
    public partial class ControllerForm : Form
    {
        private Grpc.Core.Server _grpcServer;
        private Thread _periodicRemoteClientConfigurationTask;
        private readonly HashSet<EndPoint> _remoteEndpoints;
        private readonly Dictionary<EndPoint, OBSWebsocket> _websockets;
        private readonly OBSWebsocket _controllerObs;
        private readonly HashSet<EndPoint> _falconNineIsInStartup;
        private Thread _controllerTask;
        private bool _currentInputIsPrimary;
        private bool _needsPrepIntoStandby;
        private EndPoint _standbyInput;
        private EndPoint _currentInput;
        private Random _random;
        private readonly UdpForwarder _primaryForwarder;
        private readonly UdpForwarder _secondaryForwarder;
        private Dictionary<string, string> _slackUserCache;
        private SlackSocketClient _slackClient;
        private Dictionary<string, string> _slackEmojis;
        private readonly object _messagesLock = new object();
        private readonly List<string> _excludedIpAddressUserInput = new List<string>();

        private readonly Dictionary<EndPoint, DateTime> LastBroadcastTime = new Dictionary<EndPoint, DateTime>();

        private class GrpcServer : Jamcast.Controller.ControllerBase
        {
            private readonly ControllerForm _form;

            public GrpcServer(ControllerForm form)
            {
                _form = form;
            }

            public override async Task Connect(ClientRequest request, IServerStreamWriter<ClientResponse> responseStream, ServerCallContext context)
            {
                try
                {
                    var remoteEndPoint = IPEndPoint.Parse(context.Peer.Substring("ipv4:".Length));

                    if (!_form._remoteEndpoints.Contains(remoteEndPoint))
                    {
                        _form.Log(null, "Client not already seen, adding to remote endpoints and adding to WS list");
                        _form._remoteEndpoints.Add(remoteEndPoint);

                        var webSocketEndpoint = new IPEndPoint(((IPEndPoint)remoteEndPoint).Address, 4444);
                        var websocket = new OBSWebsocket
                        {
                            WSTimeout = TimeSpan.FromSeconds(2)
                        };
                        _form._websockets[remoteEndPoint] = websocket;
                        _form.RegisterWebsocketEvents(websocket);

                        _form._falconNineIsInStartup.Add(remoteEndPoint);

                        _form.Log(null, "Sleeping for 2 seconds while we wait for OBS to start up on the remote client");
                        await Task.Delay(2000);
                        while (true)
                        {
                            if (!_form._remoteEndpoints.Contains(remoteEndPoint))
                            {
                                _form.Log(null, "Remote endpoint has been disconnected (error: this should never happen)");
                                return;
                            }

                            _form.Log(null, "Connecting to OBS websocket at " + webSocketEndpoint.ToString());
                            try
                            {
                                websocket.Connect("ws://" + webSocketEndpoint.ToString(), null);
                                if (!websocket.IsConnected)
                                {
                                    _form.Log(null, "Unable to connect to WS... sleeping 2 seconds and trying again");
                                    await Task.Delay(2000);
                                    continue;
                                }

                                _form.Log(null, "Connected to OBS websocket at " + webSocketEndpoint.ToString());
                                break;
                            }
                            catch (Exception ex)
                            {
                                _form.Log(null, "Unable to connect to WS... sleeping 2 seconds and trying again");
                                await Task.Delay(2000);

                                if (context.CancellationToken.IsCancellationRequested)
                                {
                                    _form.Log(null, "Unable to talk to " + remoteEndPoint + " during OBS connect, disconnecting");

                                    _form._remoteEndpoints.Remove(remoteEndPoint);
                                    _form._websockets[remoteEndPoint].Disconnect();
                                    _form.UpdateStatus();
                                    return;
                                }

                                await Task.Delay(500);
                            }
                        }

                        _form._falconNineIsInStartup.Remove(remoteEndPoint);

                        _form.Log(null, "Updating client list");
                        _form.UpdateStatus();
                    }

                    while (!context.CancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(500);
                    }

                    _form.Log(null, "Client " + remoteEndPoint + " disconnected");

                    _form._remoteEndpoints.Remove(remoteEndPoint);
                    _form._websockets[remoteEndPoint].Disconnect();
                    _form.UpdateStatus();
                    return;
                }
                catch (Exception ex)
                {
                    _form.Log(null, "error while handling gRPC: " + ex.Message);
                }
            }
        }


        public ControllerForm()
        {
            InitializeComponent();

            _primaryForwarder = new UdpForwarder(1234, 1237);
            _secondaryForwarder = new UdpForwarder(1235, 1238);

            _primaryForwarder.OnError = (ex) =>
            {
                Log(null, "Primary forwarder: " + ex);
            };
            _secondaryForwarder.OnError = (ex) =>
            {
                Log(null, "Secondary forwarder: " + ex);
            };

            if (System.IO.File.Exists("ExcludeList.txt"))
            {
                excludeBox.Text = System.IO.File.ReadAllText("ExcludeList.txt");
                excludeBox_TextChanged(this, EventArgs.Empty);
            }

            _random = new Random();

            _grpcServer = new Grpc.Core.Server
            {
                Services = { Jamcast.Controller.BindService(new GrpcServer(this)) },
                Ports = { new ServerPort("", 8080, ServerCredentials.Insecure) },
            };
            _grpcServer.Start();

            _remoteEndpoints = new HashSet<EndPoint>();
            _websockets = new Dictionary<EndPoint, OBSWebsocket>();
            _falconNineIsInStartup = new HashSet<EndPoint>();

            _standbyInput = null;
            _currentInput = null;
            _currentInputIsPrimary = false;
            _needsPrepIntoStandby = true;

            _periodicRemoteClientConfigurationTask = StartThread("Remote Client Config", RemoteClientConfiguration);

            _controllerObs = new OBSWebsocket();
            RegisterWebsocketEvents(_controllerObs);

            Task.Run(Startup);
        }

        private void Startup()
        {
            Log(null, "Connecting to OBS on ws://localhost:4444 ...");
            _controllerObs.Connect("ws://localhost:4444", null);

            Log(null, "Starting controller thread...");
            _controllerTask = StartThread("OBS Controller", ControllerTask);

            Log(null, "Starting HTTP server for chat...");
            StartThread("HTTP Host", HostHttpServerForChat);

            Log(null, "Starting Slack thread...");
            StartThread("Slack Thread", PullSlackMessagesAndOutputToFile);
        }

        private void HostHttpServerForChat()
        {
            var app = new AppBuilder();
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.Value == "/")
                {
                    await context.Response.WriteAsync(System.IO.File.ReadAllText("SlackHost.htm"));
                }
                else if (System.IO.File.Exists(context.Request.Path.Value.TrimStart('/')))
                {
                    var buffer = System.IO.File.ReadAllBytes(context.Request.Path.Value.TrimStart('/'));
                    await context.Response.WriteAsync(buffer);
                }
                else
                    await next();
            });
            var listener = ServerBuilder.New().SetPort(9091).SetOwinApp(app.Build());
            listener.Start();
        }

        private Thread StartThread(string name, Action action)
        {
            var t = new Thread(new ThreadStart(() => { action(); }));
            t.Name = name;
            t.IsBackground = true;
            t.Start();
            return t;
        }

        // This task is responsible for controlling the local OBS which has VLC sources
        // and receives inputs, transitioning between different inputs as needed.
        private void ControllerTask()
        {
            while (true)
            {
                try
                {
                    Log(null, "Switching to Secondary source...");

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

                    System.IO.File.WriteAllLines("CurrentEndpoint.txt", new[] { _currentInput?.ToString() ?? "No-one!" });

                    UpdateStatus();

                    _currentInputIsPrimary = false;

                    Thread.Sleep(1500);

                    _needsPrepIntoStandby = true;

                    Thread.Sleep(28500);

                    Log(null, "Switching to Primary source...");

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

                    System.IO.File.WriteAllLines("CurrentEndpoint.txt", new[] { _currentInput?.ToString() ?? "No-one!" });

                    UpdateStatus();

                    _currentInputIsPrimary = true;

                    Thread.Sleep(1500);

                    _needsPrepIntoStandby = true;

                    Thread.Sleep(28500);
                }
                catch (Exception ex)
                {
                    Log(null, ex.ToString());
                }
            }
        }

        [RequestPath("emoji.list")]
        public class EmojiListResponse : Response
        {
            public Dictionary<string, string> emoji;
        }

        [RequestPath("files.sharedPublicURL")]
        public class FilesSharedPublicURLResponse : Response
        {
            public SlackAPI.File file;
        }

        private void PullSlackMessagesAndOutputToFile()
        {
            var messages = new List<JObject>();
            if (System.IO.File.Exists("SlackChat.json"))
            {
                messages = JsonConvert.DeserializeObject<List<JObject>>(System.IO.File.ReadAllText("SlackChat.json"));
                if (messages == null)
                {
                    messages = new List<JObject>();
                }
            }

            // _slackClient = new SlackSocketClient("TODO");
            _slackClient.Connect((connected) => {
                // This is called once the client has emitted the RTM start command
                _slackClient.APIRequestWithToken<EmojiListResponse>((resp) =>
                {
                    _slackEmojis = resp.emoji;

                    lock (_messagesLock)
                    {
                        System.IO.File.WriteAllText("SlackChat.htm", GenerateHtmlMessages(messages));
                    }
                });
            }, () => {
                // This is called once the RTM client has connected to the end point
            });
            _slackClient.OnMessageReceived += (messageObj) =>
            {
                var messageRaw = JsonConvert.DeserializeObject<JObject>(JsonConvert.SerializeObject(messageObj));

                var message = JsonConvert.DeserializeObject<NewMessage>(JsonConvert.SerializeObject(messageRaw));
                var fileShareMessage = JsonConvert.DeserializeObject<FileShareMessage>(JsonConvert.SerializeObject(messageRaw));

                if (message.channel == "CFQ0AF6CX")
                {
                    Action complete = () =>
                    {
                        lock (_messagesLock)
                        {
                            messages.Add(messageRaw);

                            System.IO.File.WriteAllText("SlackChat.json", JsonConvert.SerializeObject(messages));
                            System.IO.File.WriteAllText("SlackChat.htm", GenerateHtmlMessages(messages));
                        }
                    };

                    if (message.subtype == "file_share")
                    {
                        if (fileShareMessage.file.mimetype.Contains("image"))
                        {
                            _slackClient.APIRequestWithToken<FilesSharedPublicURLResponse>((resp) =>
                            {
                                Task.Run(async () =>
                                {
                                    // Wait for Slack to make the file public.
                                    await Task.Delay(1500);
                                    fileShareMessage.file = resp.file;
                                    complete();
                                });
                            }, new Tuple<string, string>("file", fileShareMessage.file.id));
                        }
                        else
                        {
                            complete();
                        }
                    }
                    else
                    {
                        complete();
                    }

                }
            };
        }

        private string GenerateHtmlMessages(List<JObject> messages)
        {
            if (!_slackClient.IsReady)
                return "... Loading Slack ...";
            messages = ((IEnumerable<JObject>)messages).Reverse().ToList();

            var channelRegex = new Regex("\\<\\#.+?\\|(.+?)\\>");
            var userRegex = new Regex("\\<\\@(.+?)\\>");
            var emojiRegex = new Regex("\\:([a-z_0-9-]+?)\\:");

            var markdown = new Markdown();

            var chat = "";
            foreach (var messageRaw in messages.Take(40))
            {
                var message = JsonConvert.DeserializeObject<NewMessage>(JsonConvert.SerializeObject(messageRaw));
                var fileShareMessage = JsonConvert.DeserializeObject<FileShareMessage>(JsonConvert.SerializeObject(messageRaw));
                if (message.subtype == "file_share" &&
                    fileShareMessage != null &&
                    fileShareMessage.file.mimetype.Contains("image"))
                {
                    var publicUri = new Uri(fileShareMessage.file.permalink_public);
                    var publicUriPath = publicUri.LocalPath.TrimStart('/').Split('-');
                    var publicSecret = publicUriPath.Last();
                    var privateComponents = publicUriPath.Take(publicUriPath.Length - 1);
                    var realPublicUri = $"https://files.slack.com/files-pri/{string.Join("-", privateComponents)}/{fileShareMessage.file.name}?pub_secret={publicSecret}";

                    chat += "<span class=\"author\">@" + _slackClient.UserLookup[message.user].name + ":</span> <img class=\"img\" src=\"" + realPublicUri + "\" />";
                }
                else
                {
                    var newMessage = message.text;
                    newMessage = channelRegex.Replace(newMessage, (match) => "<span class=\"channel\">#" + match.Groups[1].Value + "</span>");
                    newMessage = userRegex.Replace(newMessage, (match) => "<span class=\"user\">@" + _slackClient.UserLookup[match.Groups[1].Value].name + "</span>");
                    newMessage = emojiRegex.Replace(newMessage, (match) =>
                    {
                        var emoji_name = match.Groups[1].Value;

                        if (_slackEmojis != null)
                        {
                            if (_slackEmojis.ContainsKey(emoji_name))
                            {
                                var customEmoji = _slackEmojis[emoji_name];
                                if (customEmoji.StartsWith("alias:"))
                                {
                                    emoji_name = customEmoji.Substring("alias:".Length);
                                }
                                else
                                {
                                    return $"<span class=\"emoji-outer emoji-sizer\"><span class=\"emoji-inner\" style=\"background: url(" + customEmoji + "); background-size: contain;\"></span></span>";
                                }
                            }
                        }

                        if (EmojiSharp.Emoji.All.ContainsKey(emoji_name))
                        {
                            var unicode = EmojiSharp.Emoji.All[emoji_name].Unified.Replace("-", "").ToLowerInvariant();
                            return $"<span class=\"emoji-outer emoji-sizer\"><span class=\"emoji-inner emoji{unicode}\"></span></span>";
                        }
                        else
                        {
                            return ":" + emoji_name + ":";
                        }
                    });

                    var formattedMessage = markdown.Transform(newMessage);

                    chat += "<span class=\"author\">@" + _slackClient.UserLookup[message.user].name + ":</span> " + formattedMessage + "<br />";
                }
            }
            return chat;
        }

        // This task goes through the client and configures their scenes, profiles, etc.
        // to be ready for streaming.
        private void RemoteClientConfiguration()
        {
            while (true)
            {
                try
                {
                    if (!_needsPrepIntoStandby && !(_remoteEndpoints.Count > 2 && _currentInput == null && _standbyInput == null))
                    {
                        Thread.Sleep(500);
                        continue;
                    }

                    Log(null, "Prepping standby screen");

                    Log(null, "Current standby is: " + (_standbyInput == null ? "<none>" : _standbyInput.ToString()));
                    Log(null, "Current active is: " + (_currentInput == null ? "<none>" : _currentInput.ToString()));

                    var endpoints = _remoteEndpoints.Where(x => !_falconNineIsInStartup.Contains(x) && _websockets.ContainsKey(x)).ToList();
                    if (endpoints.Count == 0)
                    {
                        // Can't prep anything - not enough clients.
                        Log(null, "No clients, nothing to prep into standby slot");
                        _needsPrepIntoStandby = false;
                        continue;
                    }

                    if (endpoints.Count == 1)
                    {
                        // Tell our only endpoint to stream into the current display. This will look janky
                        // until another client is lined up.
                        Log(null, "Only one client, prepping into standby only if not active");
                        if (!endpoints[0].Equals(_standbyInput) && !endpoints[0].Equals(_currentInput))
                        {
                            Log(null, "Not in standby or current");

                            var websocketSingle = _websockets[endpoints[0]];
                            if (websocketSingle == null)
                            {
                                Log(null, "WebSocket was null! WHAT?");
                                _needsPrepIntoStandby = false;
                                continue;
                            }
                            if (_currentInputIsPrimary)
                            {
                                Log(null, "Setting profile to Secondary");
                                websocketSingle.SetCurrentProfile("Jamcast-Secondary");
                            }
                            else
                            {
                                Log(null, "Setting profile to Primary");
                                websocketSingle.SetCurrentProfile("Jamcast-Primary");
                            }
                            _standbyInput = endpoints[0];
                            try
                            {
                                Log(null, "Telling client at " + _standbyInput + " start streaming");
                                websocketSingle.StartRecording();
                            }
                            catch (Exception ex)
                            {
                                Log(null, _standbyInput + " during recording start: " + ex.ToString());
                            }
                            _needsPrepIntoStandby = false;

                            if (_currentInputIsPrimary)
                            {
                                _secondaryForwarder.ExpectedIpAddress = ((IPEndPoint)_standbyInput).Address;
                            }
                            else
                            {
                                _primaryForwarder.ExpectedIpAddress = ((IPEndPoint)_standbyInput).Address;
                            }
                        }
                        _needsPrepIntoStandby = false;
                        continue;
                    }

                    if (_currentInput != null)
                    {
                        endpoints.Remove(_currentInput);
                    }

                    if (_standbyInput != null)
                    {
                        if (endpoints.Count > 2)
                        {
                            // Enough endpoints that we can rotate away from this one.
                            endpoints.Remove(_standbyInput);
                        }

                        Log(null, "Requesting client at " + _standbyInput + " stop streaming");
                        if (_websockets.ContainsKey(_standbyInput))
                        {
                            var oldWebsocket = _websockets[_standbyInput];
                            try
                            {
                                oldWebsocket.StopRecording();
                            }
                            catch (Exception ex)
                            {
                                Log(null, _standbyInput + " during recording stop: " + ex.ToString());
                            }
                        }
                        else
                        {
                            Log(null, "(Websocket for " + _standbyInput + " isn't around any more, nothing to do)");
                        }

                        _standbyInput = null;
                    }
                    EndPoint nextInput = ChooseNextEndpoint(endpoints);

                    Log(null, "Selected " + nextInput + " as next standby");

                    var ipClient = nextInput as IPEndPoint;
                    if (ipClient != null && _excludedIpAddressUserInput.Contains(ipClient.Address.ToString()))
                    {
                        Log(null, "Need to pick another websocket (this one is excluded)...");
                        Thread.Sleep(1000);
                        continue;
                    }

                    // Tell the next endpoint to start streaming into the right slot.
                    var websocket = _websockets[nextInput];
                    if (!websocket.IsConnected)
                    {
                        Log(null, "Need to pick another websocket (this one isn't ready)...");
                        Thread.Sleep(1000);
                        continue;
                    }
                    if (_currentInputIsPrimary)
                    {
                        Log(null, "Setting profile to Secondary");
                        websocket.SetCurrentProfile("Jamcast-Secondary");
                        if (websocket.GetCurrentProfile() != "Jamcast-Secondary")
                        {
                            // OBS in a state that we can't switch profiles don't use.
                            Log(null, "Need to pick another websocket (this one isn't ready)...");
                            Thread.Sleep(1000);
                            continue;
                        }
                    }
                    else
                    {
                        Log(null, "Setting profile to Primary");
                        websocket.SetCurrentProfile("Jamcast-Primary");
                        if (websocket.GetCurrentProfile() != "Jamcast-Primary")
                        {
                            // OBS in a state that we can't switch profiles don't use.
                            Log(null, "Need to pick another websocket (this one isn't ready)...");
                            Thread.Sleep(1000);
                            continue;
                        }
                    }
                    try
                    {
                        Log(null, "Telling client at " + nextInput + " start streaming");
                        websocket.StartRecording();
                    }
                    catch (Exception ex)
                    {
                        if (ex.Message.StartsWith("recording already active"))
                        {
                            websocket.StopRecording();
                        }
                        Log(null, nextInput + " during recording start: " + ex.ToString());
                        Log(null, "Need to pick another websocket (this one isn't ready)...");
                        Thread.Sleep(1000);
                        continue;
                    }

                    _standbyInput = nextInput;
                    _needsPrepIntoStandby = false;

                    if (_currentInputIsPrimary)
                    {
                        _secondaryForwarder.ExpectedIpAddress = ((IPEndPoint)_standbyInput).Address;
                    }
                    else
                    {
                        _primaryForwarder.ExpectedIpAddress = ((IPEndPoint)_standbyInput).Address;
                    }

                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    try
                    {
                        Log(null, ex.ToString());
                    }
                    catch { }
                }
                finally
                {
                    try
                    {
                        UpdateStatus();
                    }
                    catch { }
                }
            }
        }

        private EndPoint ChooseNextEndpoint(List<EndPoint> endpoints)
        {
            var clone = endpoints.ToList();

            int nextIdx = 0;
            //return _random.Next(0, endpoints.Count - 1);
            var never = clone.Where(k => !LastBroadcastTime.ContainsKey(k)).ToList();
            if (never.Any())
            {
                nextIdx = _random.Next(0, never.Count - 1);
            }
            else
            {
                clone = clone.OrderBy(k => LastBroadcastTime[k]).ToList();
                if (clone.Count > 1)
                    nextIdx = _random.Next(0, 1);
                else
                    nextIdx = 0;
            }

            var nextInput = clone[nextIdx];
            LastBroadcastTime[nextInput] = DateTime.Now;
            return nextInput;
        }

        private void RegisterWebsocketEvents(OBSWebsocket websocket)
        {
            websocket.Connected += (sender, args) => Log(websocket, "Connected");
            websocket.Disconnected += (sender, args) =>
            {
                Log(websocket, "Disconnected");

                Log(websocket, "Terminating associated TCP connection to force client to reconnect / reinit");
                var associatedEndpoint = _websockets.Keys.FirstOrDefault(x => _websockets[x].Equals(websocket));
                if (associatedEndpoint == null)
                {
                    Log(websocket, "Unable to find associated TCP endpoint");
                }
                else if (_falconNineIsInStartup.Contains(associatedEndpoint))
                {
                    Log(websocket, "Falcon nine is in startup, not disconnecting");
                }
                else
                { 
                    _remoteEndpoints.Remove(associatedEndpoint);
                    _websockets.Remove(associatedEndpoint);

                    UpdateStatus();
                }
            };
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

            if (websocket != null)
            {
                wsLogs.Text += $"{websocket?.WSConnection?.Url?.ToString() ?? "??"}: {ev}" + Environment.NewLine;
                wsLogs.SelectionLength = 0;
                wsLogs.SelectionStart = wsLogs.Text.Length;
                wsLogs.ScrollToCaret();
            }
            else
            {
                logMessages.Text += $"core: {ev}" + Environment.NewLine;
                logMessages.SelectionLength = 0;
                logMessages.SelectionStart = logMessages.Text.Length;
                logMessages.ScrollToCaret();
            }
        }

        private void UpdateStatus()
        {
            if (InvokeRequired)
            {
                Invoke(new Action(() => { UpdateStatus(); }));
                return;
            }

            try
            {
                System.IO.File.WriteAllText("Connections.txt", _remoteEndpoints.Count + " people connected");
            }
            catch
            {

            }

            var status = "";
            foreach (var client in _remoteEndpoints)
            {
                var suffix = "";
                var ipClient = client as IPEndPoint;
                if (ipClient != null && _excludedIpAddressUserInput.Contains(ipClient.Address.ToString()))
                {
                    suffix += " (Excluded)";
                }
                if (client.Equals(_currentInput))
                {
                    suffix += " (Active)";
                }
                if (client.Equals(_standbyInput))
                {
                    suffix += " (Standby)";
                }
                if (_falconNineIsInStartup.Contains(client))
                {
                    suffix += " (Startup)";
                }
                status += client.ToString() + suffix + Environment.NewLine;
            }

            controllerStatus.Text = status;
        }

        private void excludeBox_TextChanged(object sender, EventArgs e)
        {
            _excludedIpAddressUserInput.Clear();
            _excludedIpAddressUserInput.AddRange(excludeBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries));

            System.IO.File.WriteAllText("ExcludeList.txt", excludeBox.Text);
        }
    }
}
