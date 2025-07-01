using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;

namespace PiKvmLibrary
{
    public class Pikvm_Apiws
    {
        private static Dictionary<Key, string> keyValuePairs;
        private const string USERNAME_PROMPT = "X-KVMD-User";
        private const string PASSWORD_PROMPT = "X-KVMD-Passwd";
        private const string URI_PATH = "wss://192.168.1.183/api/ws?stream=0";

        private CancellationTokenSource _Cts_ClientComm;
        private const string SENDMOUSEMOVE = "{\"event_type\": \"mouse_move\", \"event\": {\"to\": {\"x\": \\{TOX\\}, \"y\": \\{TOY\\}}}}";
        //private const string SENDMOUSERELATIVE = "{\"event_type\": \"mouse_relative\", \"event\": {\"delta_x\": \\{TOX\\}, \"delta_y\": \\{TOY\\}}}";
        private const string SENDMOUSERELATIVE = "{\"event_type\": \"mouse_relative\", \"event\": {\"delta\": {\"x\": \\{TOX\\}, \"y\": \\{TOY\\}}, \"squash\": false}}";
        private const string SENDMOUSEBUTTON = "{\"event_type\": \"mouse_button\", \"event\": {\"button\": \"\\{BUTTON\\}\"}}";
        private const string SENDKEYPRESSSINGLE = "{\"event_type\": \"key\", \"event\": {\"key\": \"\\{KEY\\}\", \"state\": true, \"finish\": true }}";
        private const string SENDKEYPRESS = "{\"event_type\": \"key\", \"event\": {\"key\": \"\\{KEY\\}\", \"state\": \"\\{STATE\\}\" }}";

        private ClientWebSocket _ClientWSS;
        private Uri _WsUri;

        public event EventHandler<LogMessage> OnLogEvent;
        public event EventHandler<string> OnMessageRecieved;

        public Pikvm_Apiws()
        {
            _Cts_ClientComm = new CancellationTokenSource();
            _ClientWSS = new ClientWebSocket();

            _WsUri = new Uri(URI_PATH);

            keyValuePairs = new Dictionary<Key, string>();
            
            keyValuePairs.Add(Key.A,                "KeyA");
            keyValuePairs.Add(Key.B,                "KeyB");
            keyValuePairs.Add(Key.C,                "KeyC");
            keyValuePairs.Add(Key.D,                "KeyD");
            keyValuePairs.Add(Key.E,                "KeyE");
            keyValuePairs.Add(Key.F,                "KeyF");
            keyValuePairs.Add(Key.G,                "KeyG");
            keyValuePairs.Add(Key.H,                "KeyH");
            keyValuePairs.Add(Key.I,                "KeyI");
            keyValuePairs.Add(Key.J,                "KeyJ");
            keyValuePairs.Add(Key.K,                "KeyK");
            keyValuePairs.Add(Key.L,                "KeyL");
            keyValuePairs.Add(Key.M,                "KeyM");
            keyValuePairs.Add(Key.N,                "KeyN");
            keyValuePairs.Add(Key.O,                "KeyO");
            keyValuePairs.Add(Key.P,                "KeyP");
            keyValuePairs.Add(Key.Q,                "KeyQ");
            keyValuePairs.Add(Key.R,                "KeyR");
            keyValuePairs.Add(Key.S,                "KeyS");
            keyValuePairs.Add(Key.T,                "KeyT");
            keyValuePairs.Add(Key.U,                "KeyU");
            keyValuePairs.Add(Key.V,                "KeyV");
            keyValuePairs.Add(Key.W,                "KeyW");
            keyValuePairs.Add(Key.X,                "KeyX");
            keyValuePairs.Add(Key.Y,                "KeyY");
            keyValuePairs.Add(Key.Z,                "KeyZ");
            keyValuePairs.Add(Key.D0,               "Digit0");
            keyValuePairs.Add(Key.D1,               "Digit1");
            keyValuePairs.Add(Key.D2,               "Digit2");
            keyValuePairs.Add(Key.D3,               "Digit3");
            keyValuePairs.Add(Key.D4,               "Digit4");
            keyValuePairs.Add(Key.D5,               "Digit5");
            keyValuePairs.Add(Key.D6,               "Digit6");
            keyValuePairs.Add(Key.D7,               "Digit7");
            keyValuePairs.Add(Key.D8,               "Digit8");
            keyValuePairs.Add(Key.D9,               "Digit9");
            keyValuePairs.Add(Key.Return,           "Enter");
            keyValuePairs.Add(Key.Escape,           "Escape");
            keyValuePairs.Add(Key.Back,             "Backspace");
            keyValuePairs.Add(Key.Tab,              "Tab");
            keyValuePairs.Add(Key.Space,            "Space");
            keyValuePairs.Add(Key.OemMinus,         "Minus");
            keyValuePairs.Add(Key.OemPlus,          "Equal");
            keyValuePairs.Add(Key.OemOpenBrackets,  "BracketLeft"); // OemOpenBrackets is often used for the '[' key
            keyValuePairs.Add(Key.OemCloseBrackets, "BracketRight"); // OemCloseBrackets is often used for the ']' key
            keyValuePairs.Add(Key.OemBackslash,     "Backslash"); // OemBackslash is often used for the '\' key
            keyValuePairs.Add(Key.OemSemicolon,     "Semicolon"); // OemSemicolon is often used for the ';' key
            keyValuePairs.Add(Key.OemQuotes,        "Quote"); // OemQuotes is often used for the '\'' key
            keyValuePairs.Add(Key.Oem3,             "Backquote"); // Oem3 is often used for the '`' key (backtick or grave accent)
            keyValuePairs.Add(Key.OemComma,         "Comma"); // OemComma is often used for the ',' key
            keyValuePairs.Add(Key.OemPeriod,        "Period"); // OemPeriod is often used for the '.' key
            keyValuePairs.Add(Key.OemQuestion,      "Slash"); // OemQuestion is often used for the '/' key
            keyValuePairs.Add(Key.CapsLock,         "CapsLock");
            keyValuePairs.Add(Key.F1,               "F1");
            keyValuePairs.Add(Key.F2,               "F2");
            keyValuePairs.Add(Key.F3,               "F3");
            keyValuePairs.Add(Key.F4,               "F4");
            keyValuePairs.Add(Key.F5,               "F5");
            keyValuePairs.Add(Key.F6,               "F6");
            keyValuePairs.Add(Key.F7,               "F7");
            keyValuePairs.Add(Key.F8,               "F8");
            keyValuePairs.Add(Key.F9,               "F9");
            keyValuePairs.Add(Key.F10,              "F10");
            keyValuePairs.Add(Key.F11,              "F11");
            keyValuePairs.Add(Key.F12,              "F12");
            keyValuePairs.Add(Key.PrintScreen,      "PrintScreen");
            keyValuePairs.Add(Key.Insert,           "Insert");
            keyValuePairs.Add(Key.Home,             "Home");
            keyValuePairs.Add(Key.PageUp,           "PageUp");
            keyValuePairs.Add(Key.Delete,           "Delete");
            keyValuePairs.Add(Key.End,              "End");
            keyValuePairs.Add(Key.PageDown,         "PageDown");
            keyValuePairs.Add(Key.Right,            "ArrowRight");
            keyValuePairs.Add(Key.Left,             "ArrowLeft");
            keyValuePairs.Add(Key.Down,             "ArrowDown");
            keyValuePairs.Add(Key.Up,               "ArrowUp");
            keyValuePairs.Add(Key.LeftCtrl,         "ControlLeft");
            keyValuePairs.Add(Key.LeftShift,        "ShiftLeft");
            keyValuePairs.Add(Key.LeftAlt,          "AltLeft");
            keyValuePairs.Add(Key.LWin,             "MetaLeft"); // Left Windows key
            keyValuePairs.Add(Key.RightCtrl,        "ControlRight");
            keyValuePairs.Add(Key.RightShift,       "ShiftRight");
            keyValuePairs.Add(Key.RightAlt,         "AltRight");
            keyValuePairs.Add(Key.RWin,             "MetaRight"); // Right Windows key
            keyValuePairs.Add(Key.Pause,            "Pause");
            keyValuePairs.Add(Key.Scroll,           "ScrollLock");
            keyValuePairs.Add(Key.NumLock,          "NumLock");
            keyValuePairs.Add(Key.Apps,             "ContextMenu"); // Context menu key (often found on keyboards)
            keyValuePairs.Add(Key.Divide,           "NumpadDivide");
            keyValuePairs.Add(Key.Multiply,         "NumpadMultiply");
            keyValuePairs.Add(Key.Subtract,         "NumpadSubtract");
            keyValuePairs.Add(Key.Add,              "NumpadAdd");
            //keyValuePairs.Add(Key.Enter,            "NumpadEnter"); COPY OF ENTER
            keyValuePairs.Add(Key.NumPad1,          "Numpad1");
            keyValuePairs.Add(Key.NumPad2,          "Numpad2");
            keyValuePairs.Add(Key.NumPad3,          "Numpad3");
            keyValuePairs.Add(Key.NumPad4,          "Numpad4");
            keyValuePairs.Add(Key.NumPad5,          "Numpad5");
            keyValuePairs.Add(Key.NumPad6,          "Numpad6");
            keyValuePairs.Add(Key.NumPad7,          "Numpad7");
            keyValuePairs.Add(Key.NumPad8,          "Numpad8");
            keyValuePairs.Add(Key.NumPad9,          "Numpad9");
            keyValuePairs.Add(Key.NumPad0,          "Numpad0");
            keyValuePairs.Add(Key.Decimal,          "NumpadDecimal");
            keyValuePairs.Add(Key.Sleep,            "Power");
            //keyValuePairs.Add(Key.Oem102,           "IntlBackslash"); // Oem102 is often used for the '|' key (pipe) on some keyboards COPY OF BACKSLASH
                                                                            // "IntlYen": ecodes.KEY_YEN,
                                                                            // "IntlRo": ecodes.KEY_RO,
                                                                            // "KanaMode": ecodes.KEY_KATAKANA,
            keyValuePairs.Add(Key.ImeConvert,       "Convert"); // IME Convert key
            keyValuePairs.Add(Key.ImeNonConvert,    "NonConvert"); // IME Non-Convert key
            keyValuePairs.Add(Key.VolumeMute,       "VolumeMute");
            keyValuePairs.Add(Key.VolumeUp,         "VolumeUp");
            keyValuePairs.Add(Key.VolumeDown,       "VolumeDown");
            keyValuePairs.Add(Key.F20,              "F20"); // F20 key, if available on the keyboard

            
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
            Task<bool> connectWss = ConnectAsync();
            connectWss.Wait();
            return connectWss.Result;
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
        public async Task<bool> ConnectAsync()
        {
            int cnt = 1;
            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Attempting connection to {_WsUri.AbsoluteUri}", TimeStamp = DateTime.Now });
            while (_ClientWSS.State != WebSocketState.Open && !_Cts_ClientComm.IsCancellationRequested)
            {
                try
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Attempting connection count ({cnt}) to {_WsUri}", TimeStamp = DateTime.Now });
                    await _ClientWSS.ConnectAsync(_WsUri, _Cts_ClientComm.Token);
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
                await _ClientWSS.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnecting", _Cts_ClientComm.Token);
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Disconnected {_WsUri.ToString()}", TimeStamp = DateTime.Now });
                return true;
            }
            catch (OperationCanceledException exCanceled)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = exCanceled.Message, TimeStamp = DateTime.Now });
                return false;
            }
        }
        public async Task<bool> ReconnectAsync()
        {
            bool resultDisconnect = await DisconnectAsync();
            bool resultConnect = await ConnectAsync();

            return resultDisconnect && resultConnect;
        }

        private async void RecieveMessages()
        {
            while (_ClientWSS.State == WebSocketState.Open && !_Cts_ClientComm.IsCancellationRequested)
            {
                try
                {
                    byte[] buffer = new byte[2048];
                    WebSocketReceiveResult result = await _ClientWSS.ReceiveAsync(new ArraySegment<byte>(buffer), _Cts_ClientComm.Token);

                    string message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    PushOnMessage(message);
                }
                catch (OperationCanceledException exCanceled)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = exCanceled.Message, TimeStamp = DateTime.Now });
                    break; // Exit the loop if cancellation is requested
                }
                catch (WebSocketException exWebSocket)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = exWebSocket.Message, TimeStamp = DateTime.Now });
                    break; // Exit the loop on WebSocket errors
                }
                catch (Exception ex)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = ex.Message, TimeStamp = DateTime.Now });
                    break; // Exit the loop on other errors
                }
            }
        }

        public async Task SendMouseMove(int toX, int toY)
        {
            await SendMouseMoveAsync(toX, toY);
        }
        public async Task SendMouseRelative(int toX, int toY)
        {
            await SendMouseRelativeAsync(toX, toY);
        }
        public async Task SendMouseButton(string button)
        {
            await SendMouseButtonAsync(button);
        }
        public async Task GenericSendMessage(string message)
        {
            await _ClientWSS.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)), WebSocketMessageType.Text, true, _Cts_ClientComm.Token);
        }
        public async Task SendMessage(string eventType, string eventData)
        {
            string outMsg = "{\"event_type\": \"" + eventType + "\", \"event\": " + eventData + "}";
            await _ClientWSS.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(outMsg)), WebSocketMessageType.Text, true, _Cts_ClientComm.Token);
        }
        public async Task SendMouseMoveAsync(int toX, int toY)
        {
            string outMsg = SENDMOUSEMOVE;
            outMsg = outMsg.Replace("\\{TOX\\}", toX.ToString());
            outMsg = outMsg.Replace("\\{TOY\\}", toY.ToString());
            await _ClientWSS.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(outMsg)), WebSocketMessageType.Text, true, _Cts_ClientComm.Token);
        }
        public async Task SendMouseRelativeAsync(int toX, int toY)
        {
            string outMsg = SENDMOUSERELATIVE;
            outMsg = outMsg.Replace("\\{TOX\\}", toX.ToString());
            outMsg = outMsg.Replace("\\{TOY\\}", toY.ToString());
            await _ClientWSS.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(outMsg)), WebSocketMessageType.Text, true, _Cts_ClientComm.Token);
        }
        public async Task SendMouseButtonAsync(string button)
        {
            string outMsg = SENDMOUSEBUTTON;
            outMsg = outMsg.Replace("\\{BUTTON\\}", button.ToString());
            await _ClientWSS.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(outMsg)), WebSocketMessageType.Text, true, _Cts_ClientComm.Token);
        }

        //
        /// Keyboard needs the following
        /// --  sends keys stright from computer so keyEventArgs
        /// -- sends keys as characters
        /// -- sends keys as string
        /// -- sends keys as Key enum
        /// 
        public async Task SendKeyboardKeyAsync(KeyEventArgs e)
        {
            if (keyValuePairs.TryGetValue(e.Key, out string keyStrg) == false)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = $"Invalid key '{e.Key}' in key sequence.", TimeStamp = DateTime.Now });
                return;
            }
            if (e.IsDown)
                SendKeyboardKeyAsync(keyStrg, true);
            else if (e.IsUp)
                SendKeyboardKeyAsync(keyStrg, false);
        }
        public async Task SendKeyboardKeyAsync(char key)
        {
            if (key < 32 || key > 126)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = $"Invalid character '{key}' in key sequence.", TimeStamp = DateTime.Now });
                return;
            }
            if (key >= 65 && key <= 90) // Uppercase letters
                SendKeyboardUppercase("Key" + key);
            else if (key >= 97 && key <= 122) // Lowercase letters
                SendKeyboardLowercase("Key" + ((char)(key - 32)));
            else if (key >= 48 && key <= 57) // Digits
                SendKeyboardLowercase("Digit" + key);
            else if (key == 32)
            {
                if (keyValuePairs.TryGetValue(Key.Space, out string keyStrg))
                    SendKeyboardSingleKeyAsync(keyStrg);
            }
        }
        private void SendKeyboardLowercase(string key)
        {
            SendKeyboardSingleKeyAsync(key);
        }
        private void SendKeyboardUppercase(string key)
        {
            string outMsgShiftStart = SENDKEYPRESS;
            string outMsgKey = SENDKEYPRESSSINGLE;
            string outMsgShiftEnd = SENDKEYPRESS;
            SendKeyboardKeyAsync("ShiftLeft", true);
            SendKeyboardSingleKeyAsync(key);
            SendKeyboardKeyAsync("ShiftLeft", false);
        }
        private void SendKeyboardKeyAsync(string key, bool isPressed)
        {
            string outMsg = SENDKEYPRESS;
            outMsg = outMsg.Replace("\\{KEY\\}", key);
            outMsg = outMsg.Replace("\\{STATE\\}", isPressed ? "true" : "false");
            _ClientWSS.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(outMsg)), WebSocketMessageType.Text, true, _Cts_ClientComm.Token);
        }
        private void SendKeyboardSingleKeyAsync(string key)
        {
            string outMsg = SENDKEYPRESSSINGLE;
            outMsg = outMsg.Replace("\\{KEY\\}", key);
            _ClientWSS.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(outMsg)), WebSocketMessageType.Text, true, _Cts_ClientComm.Token);
        }
        private void PushOnMessage(string message)
        {
            if (AppTypeDetector.IsWpfApp())
                Dispatcher.CurrentDispatcher.Invoke(() => OnMessageRecieved?.Invoke(this, message));
            else
                OnMessageRecieved?.Invoke(this, message);
        }
        private void PushLogEvent(LogMessage logMessage)
        {
            if (AppTypeDetector.IsWpfApp())
                Dispatcher.CurrentDispatcher.InvokeAsync(() => OnLogEvent?.Invoke(this, logMessage));
            else
                OnLogEvent?.BeginInvoke(this, logMessage, null, null);
        }
    }
}
