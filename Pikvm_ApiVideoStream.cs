using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
/// <summary>
/// Connect
/// Disconnect
/// ConnectAsync
/// DisconnectAsync
/// StartVideoStream
/// /// Start WS stream -- ctsWS
/// /// Start Ffmpeg Process -- ctsffmpeg
/// /// Start push bitmapimage -- ctsbitmap
/// StopVideoStream
/// /// Stop WS stream
/// /// Stop Ffmpeg Process
/// /// Stop push bitmapimage
/// 
/// Enable WS log
/// Enable Ffmpeg log
/// Enable Library log
/// 
/// BitRate
/// GopSize
/// UserName_Prompt
/// Password_Prompt
/// URI_Path
/// FFMEPEG_Path
/// FFMPEG_ARGS
/// StartVideoStreamRequest
/// 
/// Width
/// Height
/// FPS
/// Bandwidth
/// </summary>
namespace PiKvmLibrary
{
    public class Pikvm_ApiVideoStream : INotifyPropertyChanged
    {
        private const string USERNAME_PROMPT = "X-KVMD-User";
        private const string PASSWORD_PROMPT = "X-KVMD-Passwd";
        private const string URI_PATH = "wss://192.168.1.183/api/media/ws";
        private const string STARTVIDEOSTREAM = "{\"event_type\": \"start\", \"event\": {\"type\": \"video\", \"format\": \"h264\"}}";
        private const string FFMPEG_PATH = @"C:\ffmpeg\ffmpeg-7.1.1-full_build-shared\ffmpeg-7.1.1-full_build-shared\bin\ffmpeg.exe";
        private const string FFMPEG_ARGS = "-f h264 -i pipe:0 -vf fps=10 -f image2pipe -vcodec mjpeg pipe:1";
        //private const string FFMPEG_ARGS = "-f h264 -i pipe:0 -vf fps=10 -pix_fmt yuvj420p -f image2pipe -vcodec mjpeg -q:v 2 pipe:1";
        //-f h264 -i pipe:0 -vf fps=10 -f image2pipe -vcodec mjpeg pipe:1
        //-hwaccel d3d11va -f h264 -i pipe:0 -vf fps=10 -f image2pipe -vcodec mjpeg pipe:1
        //-init_hw_device vulkan=vulkan0 -filter_hw_device vulkan0 -f h264 -i pipe:0 -vf "hwupload,format=vulkan,fps=10" -f image2pipe -vcodec mjpeg pipe:1

        // Start connections
        // Start Video Stream and Read Stream
        // Start Ffmpeg Process and Read Ffmpeg Errors


        private Task _ProcessFfmpegErrors;
        private Task _ProcessFfmpegOutput;
        private Task _ReadStream;
        private Task<bool> _ConnectWss;

        private CancellationTokenSource _CtsConnect;
        private CancellationTokenSource _CtsFfmpegOutput;
        private CancellationTokenSource _CtsFfmpegErrors;
        private CancellationTokenSource _CtsWsStream;

        private CancellationTokenSource _CtsVideoStream;

        private ClientWebSocket _ClientWSS;
        private Uri _WsUri;
        private Process _FfmpegProcess;
        private StreamWriter _FfmpegInput;
        private StreamReader _FfmpegError;
        private StreamReader _FfmpegOutput;

        public event EventHandler<LogMessage> OnLogEvent;
        public event EventHandler<LogMessage> WebSocketLog;
        public event EventHandler<LogMessage> FfmpegLog;
        public event EventHandler<LogMessage> LibraryLog;
        public event EventHandler<byte[]> OnImageRecieved;

        private int _Width = 0;
        public int Width
        {
            get => _Width;
            set
            {
                if (_Width != value)
                {
                    _Width = value;
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Width set to: {_Width}", TimeStamp = DateTime.Now });
                }
            }
        }
        private int _Height = 0;
        public int Height
        {
            get => _Height;
            set
            {
                if (_Height != value)
                {
                    _Height = value;
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Height set to: {_Height}", TimeStamp = DateTime.Now });
                }
            }
        }
        private int _FrameRate = 10;
        public int FrameRate
        {
            get => _FrameRate;
            set
            {
                if (_FrameRate != value)
                {
                    _FrameRate = value;
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"FrameRate set to: {_FrameRate}", TimeStamp = DateTime.Now });
                }
            }
        }
        private int _BitRate = 1000000; // 1 Mbps
        public int BitRate
        {
            get => _BitRate;
            set
            {
                if (_BitRate != value)
                {
                    _BitRate = value;
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"BitRate set to: {_BitRate}", TimeStamp = DateTime.Now });
                }
            }
        }

        private int _Quality = 75; // 0-100, where 100 is best quality
        public int Quality
        {
            get => _Quality;
            set
            {
                if (_Quality != value)
                {
                    _Quality = value;
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Quality set to: {_Quality}", TimeStamp = DateTime.Now });
                }
            }
        }
        private int _GopSize = 30; // Keyframe interval in seconds
        public int GopSize
        {
            get => _GopSize;
            set
            {
                if (_GopSize != value)
                {
                    _GopSize = value;
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"GopSize set to: {_GopSize}", TimeStamp = DateTime.Now });
                }
            }
        }

        private double _FPS;
        public double FPS
        {
            get => _FPS;
            set => SetField(ref _FPS, value);
        }

        private double _Bandwidth;
        public double Bandwidth
        {
            get => _Bandwidth;
            set => SetField(ref _Bandwidth, value);
        }
        public Pikvm_ApiVideoStream()
        {
            _CtsVideoStream = new CancellationTokenSource();
            _CtsFfmpegErrors = CancellationTokenSource.CreateLinkedTokenSource(_CtsVideoStream.Token);
            _CtsFfmpegOutput = CancellationTokenSource.CreateLinkedTokenSource(_CtsVideoStream.Token);
            _CtsWsStream = CancellationTokenSource.CreateLinkedTokenSource(_CtsVideoStream.Token);
            _CtsConnect = CancellationTokenSource.CreateLinkedTokenSource(_CtsVideoStream.Token);
            
            _ClientWSS = new ClientWebSocket();            
            _WsUri = new Uri(URI_PATH);

            System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, errors) => true;
            // Ignore SSL certificate validation errors
            //_ConnectWss = ConnectAsync();
        }
        public void SetCredentials(string username, string password)
        {
            _ClientWSS.Options.SetRequestHeader(USERNAME_PROMPT, username);
            _ClientWSS.Options.SetRequestHeader(PASSWORD_PROMPT, password);
        }

        public void SetCredentials(CookieCollection authCookies)
        {
            SetAuthCookies(authCookies);
        }

        private void SetAuthCookies(CookieCollection cookies)
        {
            _ClientWSS.Options.Cookies = new CookieContainer();
            foreach (Cookie cookie in cookies)
                _ClientWSS.Options.Cookies.Add(cookie);

            _ClientWSS.Options.SetRequestHeader("Cookie", _ClientWSS.Options.Cookies.GetCookieHeader(_WsUri));
        }
        public bool Connect()
        {
            _ConnectWss = ConnectAsync();
            _ConnectWss.Wait();
            return _ConnectWss.Result;
        }
        public bool Disconnect()
        {
            Task<bool> disconnectWss = DisconnectAsync();
            disconnectWss.Wait();
            return disconnectWss.Result;
        }
        public bool Reconnect()
        {
            Task<bool> reconnectWss = ReconnectAsync();
            reconnectWss.Wait();
            return reconnectWss.Result;
        }
        #region WebSocket Methods
        public async Task<bool> ConnectAsync()
        {
            _ConnectWss = _ConnectAsync(_CtsConnect.Token);
            return await _ConnectWss;
        }
        //private async Task<bool> _ConnectAsync()
        //{

        //    _CtsConnect?.Cancel();
        //    _CtsConnect?.Dispose();
        //    _CtsConnect = CancellationTokenSource.CreateLinkedTokenSource(_CtsVideoStream.Token);
        //    CancellationToken connectCT = _CtsConnect.Token;
        //    int cnt = 1;

        //    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Attempting connection to {_WsUri}", TimeStamp = DateTime.Now });
        //    while (_ClientWSS.State != WebSocketState.Open && !_Cts_ClientComm.IsCancellationRequested)
        //    {
        //        try
        //        {
        //            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Attempting connection count ({cnt}) to {_WsUri}", TimeStamp = DateTime.Now });

        //            await _ClientWSS.ConnectAsync(_WsUri, _Cts_ClientComm.Token);
        //        }
        //        catch (OperationCanceledException exCanceled)
        //        {
        //            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = exCanceled.Message, TimeStamp = DateTime.Now });
        //            return false;
        //        }
        //        catch (Exception ex)
        //        {
        //            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = ex.Message, TimeStamp = DateTime.Now });
        //            return false;
        //        }
        //        if (_ClientWSS.State != WebSocketState.Open)
        //            await Task.Delay(1000);
        //        cnt++;
        //    }

        //    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Successfully connected to {_WsUri}", TimeStamp = DateTime.Now });
        //    return true;
        //}

        public async Task<bool> DisconnectAsync()
        {
            if (_ClientWSS == null || _ClientWSS.State != WebSocketState.Open)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "WebSocket is not connected, nothing to disconnect.", TimeStamp = DateTime.Now });
                return false;
            }
            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Disconnecting WebSocket...", TimeStamp = DateTime.Now });
            await StopVideoStream();
            await _ClientWSS.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect Requested", _CtsVideoStream.Token);
            return true;
        }
        public async Task<bool> ReconnectAsync()
        {
            bool resultDisconnect = await DisconnectAsync();
            _ConnectWss = ConnectAsync();

            return resultDisconnect && await _ConnectWss;
        }
        #endregion

        public async Task StartVideoStream()
        {
            _ConnectWss = _ConnectAsync(_CtsConnect.Token);
            Start_FfmpegProcess();
            await _ConnectWss;
            Start_ReadWebSocketStream();
            Start_FfmpegErrorStream();
            Start_FfmpegOutputStream();
            Start_WebSocketVideoStream();
        }
        public async Task StopVideoStream()
        {
            Stop_ReadWebSocketStream();
            //Stop_WebSocketVideoStream();
            Stop_FfmpegProcess();
            Stop_FfmpegErrorStream();
            Stop_FfmpegOutputStream();
        }

        public void Start_ReadWebSocketStream()
        {
            if (_ReadStream == null || _ReadStream.IsCompleted)
            {
                _ReadStream = ReadWebSocketStream(_ClientWSS, _CtsWsStream.Token);
            }
        }
        public void Start_WebSocketVideoStream()
        {
            InitVideoStream();
        }
        public void Start_FfmpegProcess()
        {
            if (_FfmpegProcess == null || _FfmpegProcess.HasExited)
            {
                Initializeffmpeg(FFMPEG_PATH, FFMPEG_ARGS);
            }
        }
        public void Start_FfmpegErrorStream()
        {
            if (_ProcessFfmpegErrors == null || _ProcessFfmpegErrors.IsCompleted)
            {
                _ProcessFfmpegErrors = ProcessffmpegErrors(_CtsFfmpegErrors.Token);
            }
        }
        public void Start_FfmpegOutputStream()
        {
            if (_ProcessFfmpegOutput == null || _ProcessFfmpegOutput.IsCompleted)
            {
                _ProcessFfmpegOutput = ProcessFfmpegVideoStreamOutput(_FfmpegOutput, _CtsFfmpegOutput.Token);
            }
        }
        public void Stop_ReadWebSocketStream()
        {
            if (_ReadStream != null && !_ReadStream.IsCompleted)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Stopping WebSocket read stream", TimeStamp = DateTime.Now });
                _CtsWsStream?.Cancel();
                _CtsWsStream?.Dispose();
                _CtsWsStream = CancellationTokenSource.CreateLinkedTokenSource(_CtsVideoStream.Token);
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Stopped WebSocket read stream", TimeStamp = DateTime.Now });
            }
        }
        /// <summary>
        /// This needs to send command to stop stream from pikvm
        /// </summary>
        //public void Stop_WebSocketVideoStream()
        //{
        //    if (_ClientWSS != null && _ClientWSS.State == WebSocketState.Open)
        //    {
        //        PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Stopping WebSocket video stream", TimeStamp = DateTime.Now });
        //        _CtsWsStream?.Cancel();
        //        _CtsWsStream?.Dispose();
        //        _CtsWsStream = CancellationTokenSource.CreateLinkedTokenSource(_CtsVideoStream.Token);
        //        PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Stopped WebSocket video stream", TimeStamp = DateTime.Now });
        //    }
        //}
        public void Stop_FfmpegProcess()
        {
            if (_FfmpegProcess != null && !_FfmpegProcess.HasExited)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Stopping ffmpeg process", TimeStamp = DateTime.Now });
                Stop_FfmpegErrorStream();
                Stop_FfmpegOutputStream();
                _FfmpegProcess.Kill();
                _FfmpegProcess.Dispose();
                _FfmpegProcess = null;
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Stopped ffmpeg process", TimeStamp = DateTime.Now });
            }
        }
        public void Stop_FfmpegErrorStream()
        {
            if (_ProcessFfmpegErrors != null && !_ProcessFfmpegErrors.IsCompleted)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Stopping ffmpeg error stream", TimeStamp = DateTime.Now });
                _CtsFfmpegErrors?.Cancel();
                _CtsFfmpegErrors?.Dispose();
                _CtsFfmpegErrors = CancellationTokenSource.CreateLinkedTokenSource(_CtsVideoStream.Token);
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Stopped ffmpeg error stream", TimeStamp = DateTime.Now });
            }
        }
        public void Stop_FfmpegOutputStream()
        {
            if (_ProcessFfmpegOutput != null && !_ProcessFfmpegOutput.IsCompleted)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Stopping ffmpeg output stream", TimeStamp = DateTime.Now });
                _CtsFfmpegOutput?.Cancel();
                _CtsFfmpegOutput?.Dispose();
                _CtsFfmpegOutput = CancellationTokenSource.CreateLinkedTokenSource(_CtsVideoStream.Token);
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Stopped ffmpeg output stream", TimeStamp = DateTime.Now });
            }
        }
        // Start_ReadWebSocketStream
        // Start_WebSocketVideoStream
        // Start_FfmpegProcess
        // Start_FfmpegErrorStream
        // Start_FfmpegOutputStream

        // Stop_ReadWebSocketStream
        // Stop_WebSocketVideoStream
        // Stop_FfmpegProcess
        // Stop_FfmpegErrorStream
        // Stop_FfmpegOutputStream

        #region Private Methods

        private async Task<bool> _ConnectAsync(CancellationToken connectCT)
        {
            int cnt = 1;
            if (_ClientWSS == null || !(_ClientWSS.State == WebSocketState.Connecting || _ClientWSS.State == WebSocketState.Open))
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Attempting connection to {_WsUri}", TimeStamp = DateTime.Now });

                while (!connectCT.IsCancellationRequested)
                {
                    try
                    {
                        PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Attempting connection count ({cnt}) to {_WsUri}", TimeStamp = DateTime.Now });

                        //// Create a new WebSocket each attempt
                        //_ClientWSS?.Dispose();
                        //_ClientWSS = new ClientWebSocket();
                        //_ClientWSS.Options.SetRequestHeader(USERNAME_PROMPT, USERNAME);
                        //_ClientWSS.Options.SetRequestHeader(PASSWORD_PROMPT, PASSWORD);

                        await _ClientWSS.ConnectAsync(_WsUri, connectCT);

                        // Successfully connected
                        if (_ClientWSS.State == WebSocketState.Open)
                        {
                            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Successfully connected to {_WsUri}", TimeStamp = DateTime.Now });
                            return true;
                        }

                        cnt++;
                        await Task.Delay(1000, connectCT); // Delay with respect to cancellation
                    }
                    catch (OperationCanceledException exCanceled)
                    {
                        PushLogEvent(new LogMessage() { LogLevel = LogLevel.Warning, Message = $"Connect canceled: {exCanceled.Message}", TimeStamp = DateTime.Now });
                        return false;
                    }
                    catch (Exception ex)
                    {
                        PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = $"Connect failed: {ex.Message}", TimeStamp = DateTime.Now });
                    }
                }
            }
            else
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Already connecting -or- connected to {_WsUri}", TimeStamp = DateTime.Now });
                return true;
            }
            return false;
        }


        private void InitVideoStream()
        {
            string startString = STARTVIDEOSTREAM;
            if (_ClientWSS.State != WebSocketState.Open)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = "WebSocket is not connected. Cannot start video stream.", TimeStamp = DateTime.Now });
                return;
            }

            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Sending command to websocket to start sending video stream.", TimeStamp = DateTime.Now });
            var startStreamMessage = Encoding.UTF8.GetBytes(startString);
            _ClientWSS.SendAsync(new ArraySegment<byte>(startStreamMessage), WebSocketMessageType.Text, true, CancellationToken.None).Wait();
        }
        #region FFMPEG Methods
        private void Initializeffmpeg(string ffmpegpath, string ffmpegArgs)
        {
            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Initializing ffmpeg from: {ffmpegpath}", TimeStamp = DateTime.Now });
            if (ffmpegpath != null)
            {
                _FfmpegProcess = new Process();
                _FfmpegProcess.StartInfo.FileName = ffmpegpath;
                _FfmpegProcess.StartInfo.Arguments = ffmpegArgs;
                _FfmpegProcess.StartInfo.CreateNoWindow = true;
                _FfmpegProcess.StartInfo.ErrorDialog = false;
                _FfmpegProcess.StartInfo.RedirectStandardInput = true;
                _FfmpegProcess.StartInfo.RedirectStandardOutput = true;
                _FfmpegProcess.StartInfo.RedirectStandardError = true;
                
                _FfmpegProcess.StartInfo.StandardOutputEncoding = Encoding.UTF8;
                _FfmpegProcess.StartInfo.StandardErrorEncoding = Encoding.UTF8;
                _FfmpegProcess.StartInfo.UseShellExecute = false;

                _FfmpegProcess.Start();

                _FfmpegInput = _FfmpegProcess.StandardInput;
                _FfmpegOutput = _FfmpegProcess.StandardOutput;
                _FfmpegError = _FfmpegProcess.StandardError;

                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Initalized ffmpeg from: {ffmpegpath}", TimeStamp = DateTime.Now });
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Initalized ffmpeg parameters: {ffmpegArgs}", TimeStamp = DateTime.Now });
            }
        }
        private async Task ProcessffmpegErrors(CancellationToken ffmpegCT)
        {
            while (_ClientWSS.State == WebSocketState.Open && !ffmpegCT.IsCancellationRequested)
            {
                try
                {
                    string line = await _FfmpegError.ReadStreamWithCancellationAsync(ffmpegCT);
                    PushFfmpegLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = line, TimeStamp = DateTime.Now }); //FFMPEG ERRORS
                }
                catch (OperationCanceledException exCanceled)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = exCanceled.Message, TimeStamp = DateTime.Now });
                    break;
                }
                catch (Exception ex)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = $"Error reading ffmpeg error stream: {ex.Message}", TimeStamp = DateTime.Now });
                    break;
                }
            }
        }
        private async Task ProcessFfmpegVideoStreamOutput(StreamReader ffmpegOutput, CancellationToken fmpegOutputCT)
        {
            Stopwatch stopwatch = new Stopwatch();
            
            byte[] buffer = new byte[4096];
            bool isReading = false;
            MemoryStream ms = new MemoryStream();
            while (_ClientWSS.State == WebSocketState.Open && !fmpegOutputCT.IsCancellationRequested)
            {
                try
                {
                    int bytesRead = await ffmpegOutput.BaseStream.ReadAsync(buffer, 0, buffer.Length, fmpegOutputCT);
                    if (bytesRead == 0) break;

                    for (int i = 0; i < bytesRead; i++)
                    {
                        // Detect JPEG start marker (0xFF 0xD8)
                        if (buffer[i] == 0xFF && i + 1 < bytesRead && buffer[i + 1] == 0xD8)
                        {
                            if (isReading)
                            {
                                long timeLength = stopwatch.ElapsedMilliseconds;
                                stopwatch.Restart();
                                double fps = (1000.0 / timeLength);
                                // Finish the previous frame
                                byte[] frame = ms.ToArray();
                                //_ImageFrameQueue.Enqueue(frame);
                                PushOnImageRecieved(frame);
                                // Frame
                                //UpdateValue(Convert.ToInt32(fps), nameof(this.FPS));
                                ms.SetLength(0);
                            }
                            isReading = true;
                        }

                        if (isReading)
                        {
                            ms.WriteByte(buffer[i]);
                        }

                        // Detect JPEG end marker (0xFF 0xD9)
                        if (buffer[i] == 0xFF && i + 1 < bytesRead && buffer[i + 1] == 0xD9)
                        {
                            long timeLength = stopwatch.ElapsedMilliseconds;
                            stopwatch.Restart();
                            double fps = (1000.0 / timeLength);
                            byte[] frame = ms.ToArray();
                            //_ImageFrameQueue.Enqueue(frame);
                            PushOnImageRecieved(frame);
                            // frame
                            UpdateValue(fps, nameof(this.FPS));

                            ms.SetLength(0);
                            isReading = false;
                        }
                    }
                }
                catch (OperationCanceledException exCanceled)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = exCanceled.Message, TimeStamp = DateTime.Now });
                    break;
                }
                catch (Exception ex)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = $"Error processing video stream: {ex.Message}", TimeStamp = DateTime.Now });
                    break;
                }
            }
        }
        #endregion

        #region WebSocket Methods
        private async Task ReadWebSocketStream(ClientWebSocket clientWS, CancellationToken wsStreamCT)
        {
            Stopwatch stopwatch = new Stopwatch();
            byte[] buffer = new byte[4096];

            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Starting to read webSocket stream.", TimeStamp = DateTime.Now });
            // Read the stream from Websocket while it is open
            while (clientWS.State == WebSocketState.Open && !wsStreamCT.IsCancellationRequested)
            {
                try
                {
                    WebSocketReceiveResult result = await clientWS.ReceiveAsync(new ArraySegment<byte>(buffer), wsStreamCT);
                    // Check if the result is a close message
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await clientWS.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, wsStreamCT);
                    }
                    // Check if the result is a Text Message
                    else if (result.MessageType == WebSocketMessageType.Text)
                    {
                        byte[] message = new byte[result.Count];
                        Array.Copy(buffer, message, result.Count);

                        PushWebSocketLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = Encoding.UTF8.GetString(message), TimeStamp = DateTime.Now }); //WebSocket Log
                    }
                    // Check if the result is a Binary Message
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        _FfmpegInput.BaseStream.WriteAsync(buffer, 0, result.Count);
                        //ReceiveMessages_Binary(_FfmpegInput, buffer, result.Count);
                    }
                    long timeLength = stopwatch.ElapsedMilliseconds;
                    stopwatch.Restart();
                    double bandwidth = ((buffer.Length * 8.0) / timeLength) / 1000; // MBPS per millisecond
                    Bandwidth = bandwidth;
                }
                catch (OperationCanceledException exCanceled)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = exCanceled.Message, TimeStamp = DateTime.Now });
                    break;
                }
                catch (Exception ex)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = $"Error reading stream: {ex.Message}", TimeStamp = DateTime.Now });
                    break;
                }
            }
            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = "Stopped reading webSocket stream.", TimeStamp = DateTime.Now });
        }

        //private async Task ReceiveMessages_Binary(StreamWriter ffmpegInput, byte[] message, int byteCount)
        //{
        //    await ffmpegInput.BaseStream.WriteAsync(message, 0, byteCount);
        //}
        #endregion

        private void PushOnImageRecieved(byte[] imageData)
        {
            if (AppTypeDetector.IsWpfApp())
                Dispatcher.CurrentDispatcher.InvokeAsync(() => OnImageRecieved?.Invoke(this, imageData));
            else
                OnImageRecieved?.BeginInvoke(this, imageData, null, null);
        }
        private void PushLogEvent(LogMessage logMessage)
        {
            if (AppTypeDetector.IsWpfApp())
                Dispatcher.CurrentDispatcher.InvokeAsync(() => OnLogEvent?.Invoke(this, logMessage));
            else
                OnLogEvent?.BeginInvoke(this, logMessage, null, null);
        }
        private void PushWebSocketLogEvent(LogMessage logMessage)
        {
            if (AppTypeDetector.IsWpfApp())
                Dispatcher.CurrentDispatcher.InvokeAsync(() => WebSocketLog?.Invoke(this, logMessage));
            else
                WebSocketLog?.BeginInvoke(this, logMessage, null, null);
        }
        private void PushFfmpegLogEvent(LogMessage logMessage)
        {
            if (AppTypeDetector.IsWpfApp())
                Dispatcher.CurrentDispatcher.InvokeAsync(() => FfmpegLog?.Invoke(this, logMessage));
            else
                FfmpegLog?.BeginInvoke(this, logMessage, null, null);
        }
        private void UpdateValue(double value, string propertyName)
        {
            if (AppTypeDetector.IsWpfApp())
                Dispatcher.CurrentDispatcher.Invoke(() =>  FPS = value);
            else
            {
                Action act = new Action(() => FPS = value);
                act.Invoke();
            }
        }
        #endregion

        #region Protected Methods
        /// <summary>
        /// Method that is used to invoke the PropertyChanged event.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;
        /// <summary>
        /// Method that is used when a property value is changed to incoke the event PropertyChanged.
        /// </summary>
        /// <typeparam name="Ty">Type of the field that is being set.</typeparam>
        /// <param name="field">The field variable being set.</param>
        /// <param name="value">Value to set the field to.</param>
        /// <param name="propertyName">Property name assocaited with the field.</param>
        /// <returns>True, if OnPropertyChanged was called to invoke ProperyChanged event.</returns>
        protected virtual bool SetField<Ty>(ref Ty field, Ty value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<Ty>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        /// <summary>
        /// Method that is used to invoke the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">The property name that was changed.</param>
        public virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion
    }
}
