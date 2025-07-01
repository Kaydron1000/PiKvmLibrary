using PiKvmLibrary.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PiKvmLibrary
{
    public class GenericWebsocket
    {
        private ConnectionType _ConnectionConfiguration;
        private EndpointType _EndPointConfiguration;
        private WebSocketType _WebsocketConfiguration;

        private CancellationTokenSource _Cts_Websocket;
        private CancellationTokenSource _Cts_RecieveMessages;

        private ClientWebSocket _ClientWSS;
        private Uri _WsUri;

        public event EventHandler<LogMessage> OnLogEvent;
        public event EventHandler<string> OnMessageRecieved;
        public event EventHandler<byte[]> OnDataRecieved;

        public GenericWebsocket()
        {
            _Cts_Websocket = new CancellationTokenSource();
            _Cts_RecieveMessages = new CancellationTokenSource();
            System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, errors) => true;
        }
        public GenericWebsocket(ConnectionType connectionConfiguration, EndpointType endPointConfiguration) : this()
        {
            Initialize(connectionConfiguration, endPointConfiguration);
        }
        public void Initialize(ConnectionType connection, EndpointType endpointConfiguration)
        {
            if (connection == null || endpointConfiguration?.Item == null)
            {
                throw new ArgumentNullException("Connection and endpoint configuration cannot be null.");
            }
            if (endpointConfiguration.Item is WebSocketType ws)
            {
                _ConnectionConfiguration = connection;
                _EndPointConfiguration = endpointConfiguration;
                _WebsocketConfiguration = ws;

                string baseUri = _ConnectionConfiguration.BaseURI;
                if (!baseUri.ToLower().StartsWith("wss://"))
                {
                    if (baseUri.ToLower().StartsWith("https://"))
                    {
                        baseUri = baseUri.Replace("https://", "wss://");
                    }
                    else if (baseUri.ToLower().StartsWith("http://"))
                    {
                        baseUri = baseUri.Replace("http://", "wss://");
                    }
                    else if (baseUri.ToLower().StartsWith("ws://"))
                    {
                        baseUri = baseUri.Replace("ws://", "wss://");
                    }
                    else
                    {
                        baseUri = "wss://" + baseUri;
                    }
                }

                _WsUri = new Uri(new Uri(baseUri), _WebsocketConfiguration.Endpoint);
                _ClientWSS = new ClientWebSocket();

            }
        }
        public void SetCredentials(string username, string password)
        {
            string userPrompt = null;
            string passwordPrompt = null;

            if (_EndPointConfiguration?.UserNamePromptHeader != null)
                userPrompt = _EndPointConfiguration?.UserNamePromptHeader;
            else if (_ConnectionConfiguration?.UserNamePromptHeader != null)
                userPrompt = _ConnectionConfiguration?.UserNamePromptHeader;

            if (_EndPointConfiguration?.PasswordPromptHeader != null)
                passwordPrompt = _EndPointConfiguration?.PasswordPromptHeader;
            else if (_ConnectionConfiguration?.PasswordPromptHeader != null)
                userPrompt = _ConnectionConfiguration?.PasswordPromptHeader;

            if (userPrompt != null && passwordPrompt != null)
            {
                _ClientWSS.Options.SetRequestHeader(userPrompt, username);
                _ClientWSS.Options.SetRequestHeader(passwordPrompt, password);
            }
            else
            {
                LogMessage logMessage = new LogMessage()
                {
                    LogLevel = LogLevel.Error,
                    Message = "Username and password prompts are not set in the connection or endpoint configuration.",
                    TimeStamp = DateTime.Now
                };
                PushLogEvent(logMessage);
            }
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
        public async Task<bool> Connect()
        {
            Task<bool> connectWss = ConnectAsync();
            await connectWss;
            return connectWss.Result;
        }
        public bool Disconnect()
        {
            Task<bool> disconnectWss = DisconnectAsync();
            disconnectWss.Wait();
            return disconnectWss.Result;
        }

        public async Task<bool> ConnectAsync()
        {
            int cnt = 1;
            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Attempting connection to {_WsUri.AbsoluteUri}", TimeStamp = DateTime.Now });
            while (_ClientWSS.State != WebSocketState.Open && !_Cts_Websocket.IsCancellationRequested)
            {
                try
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Attempting connection count ({cnt}) to {_WsUri}", TimeStamp = DateTime.Now });
                    await _ClientWSS.ConnectAsync(_WsUri, _Cts_Websocket.Token);
                }
                catch (OperationCanceledException exCanceled)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = exCanceled.Message, TimeStamp = DateTime.Now });
                    return false;
                }
                catch (Exception ex)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = ex.Message, TimeStamp = DateTime.Now });
                }
                if (_ClientWSS.State != WebSocketState.Open)
                    await Task.Delay(1000);
                else
                    RecieveMessages();
                
                if (cnt > 5)
                    break;

                cnt++;
            }

            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Successfully connected to {_WsUri.ToString()}", TimeStamp = DateTime.Now });
            return true;
        }
        public async Task<bool> DisconnectAsync()
        {
            try
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Disconnecting {_WsUri.ToString()}", TimeStamp = DateTime.Now });
                await _ClientWSS.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", _Cts_Websocket.Token);
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Disconnected {_WsUri.ToString()}", TimeStamp = DateTime.Now });
                return true;
            }
            catch (OperationCanceledException exCanceled)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = exCanceled.Message, TimeStamp = DateTime.Now });
                return false;
            }
        }
        public bool SendMessage(string message)
        {
            Task<bool> sendMessageTask = SendMessageAsync(message);
            sendMessageTask.Wait();
            return sendMessageTask.Result;
        }
        public async Task<bool> SendMessageAsync(string message)
        {
            if (_ClientWSS.State != WebSocketState.Open)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = "WebSocket is not connected.", TimeStamp = DateTime.Now });
                return false;
            }

            try
            {
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                await _ClientWSS.SendAsync(new ArraySegment<byte>(messageBytes), WebSocketMessageType.Text, true, _Cts_Websocket.Token);
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Sent message: {message}", TimeStamp = DateTime.Now });
                return true;
            }
            catch (Exception ex)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Exception = ex, Message = "Error sending message.", TimeStamp = DateTime.Now });
                return false;
            }
        }
        private async void RecieveMessages()
        {
            Stopwatch stopwatch = new Stopwatch();
            byte[] buffer = new byte[4096];
            IPostProcessStream postProcessStream = null;

            if (_WebsocketConfiguration?.PostProcessStream?.Item != null)
            {
                if (_WebsocketConfiguration.PostProcessStream.Item is FFmpegConfigurationType config)
                {
                    postProcessStream = new FfmpegProcessor();
                    postProcessStream.OnLogEvent += (sender, logMessage) => PushLogEvent(logMessage);
                    postProcessStream.PostProcessLog += (sender, logMessage) => PushLogEvent(logMessage);
                    postProcessStream.PostProcessText += (sender, message) => PushOnMessage(message);
                    postProcessStream.PostProcessData += (sender, data) => PushOnDataRecieved(data);
                    postProcessStream.Initialize(_WebsocketConfiguration.PostProcessStream);
                }
            }
                

            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Starting to read webSocket stream.", TimeStamp = DateTime.Now });

            while (_ClientWSS.State == WebSocketState.Open && !_Cts_RecieveMessages.IsCancellationRequested)
            {
                try
                {
                    WebSocketReceiveResult result = await _ClientWSS.ReceiveAsync(new ArraySegment<byte>(buffer), _Cts_RecieveMessages.Token);
                    
                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    PushOnMessage(message);
                    if (postProcessStream != null)
                    {
                        postProcessStream.ProcessData(result, buffer, 0);
                    }
                    else
                    {
                        // Check if the result is a close message
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            await _ClientWSS.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, _Cts_RecieveMessages.Token);
                        }
                        // Check if the result is a Text Message
                        else if (result.MessageType == WebSocketMessageType.Text)
                        {
                            byte[] byteMessage = new byte[result.Count];
                            Array.Copy(buffer, byteMessage, result.Count);

                            PushOnMessage(Encoding.UTF8.GetString(byteMessage));
                        }
                        // Check if the result is a Binary Message
                        else if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            byte[] byteMessage = new byte[result.Count];
                            Array.Copy(buffer, byteMessage, result.Count);

                            string hexString = BitConverter.ToString(byteMessage).Replace("-", "");

                            PushOnMessage(hexString);
                        }
                    }
                }
                catch (OperationCanceledException exCanceled)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Exception = exCanceled, Message = "Webstocket stream canceled.", TimeStamp = DateTime.Now });
                    break; // Exit the loop if cancellation is requested
                }
                catch (WebSocketException exWebSocket)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Exception=exWebSocket, Message = $"Error reading stream.", TimeStamp = DateTime.Now });
                    break; // Exit the loop on WebSocket errors
                }
                catch (Exception ex)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Exception = ex, Message = $"Error reading stream.", TimeStamp = DateTime.Now });
                    break; // Exit the loop on other errors
                }
            }
            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = "Stopped reading webSocket stream.", TimeStamp = DateTime.Now });
        }
        private void PushOnDataRecieved(byte[] imageData)
        {
            Dispatcher.CurrentDispatcher?.InvokeAsync(() => OnDataRecieved?.Invoke(this, imageData));
            OnDataRecieved?.BeginInvoke(this, imageData, null, null);
        }
        private void PushOnMessage(string message)
        {
            Dispatcher.CurrentDispatcher?.Invoke(() => OnMessageRecieved?.Invoke(this, message));
            OnMessageRecieved?.Invoke(this, message);
        }
        private void PushLogEvent(LogMessage logMessage)
        {
            Dispatcher.CurrentDispatcher?.InvokeAsync(() => OnLogEvent?.Invoke(this, logMessage));
            OnLogEvent?.BeginInvoke(this, logMessage, null, null);
        }
    }
}
