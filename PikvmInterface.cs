using PiKvmLibrary.Configuration;
using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PiKvmLibrary
{
    public enum MouseMode
    {
        Absolute,
        Relative
    }
    public enum MouseButton
    {
        Left,
        Right,
        Middle
    }
    public enum MouseOutputType
    {
        usb,
        usb_rel
    }
    public class Resolution
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }
    public class PikvmInterface
    {
        private ConfigurationData<PiKvmLibraryConfigurationType> _Configuration;
        private PiKvmLibraryConfigurationType _AppConfiguration;
        private ConnectionType _Connection;
        private Resolution _Resolution;
        public EventHandler<string> OnHttpMessageEvent;
        public EventHandler<LogMessage> OnLogEvent;
        public Func<double, double> Rel_RemapX { get; set; }
        public Func<double, double> Rel_RemapY { get; set; }
        public Func<double, double> Abs_RemapX { get; set; }
        public Func<double, double> Abs_RemapY { get; set; }
        public PikvmInterface()
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, cert, chain, errors) => true;
            _Configuration = new ConfigurationData<PiKvmLibraryConfigurationType>();
            _AppConfiguration = _Configuration.ApplicationConfiguration;
            _Connection = _AppConfiguration.Connections.Connection.First();
            _Resolution = new Resolution
            {
                Width = 1920,
                Height = 1080
            };
            Rel_RemapX = (x) => RemapRelative(x, 0, _Resolution.Width, -32768, 32767);
            Rel_RemapY = (y) => RemapRelative(y, 0, _Resolution.Height, -32768, 32767);
            Abs_RemapX = (x) => Remap(x, 0, _Resolution.Width, -32768, 32767);
            Abs_RemapY = (y) => Remap(y, 0, _Resolution.Height, -32768, 32767);
        }
        public void InitializeCommunication(string uri, string username, string password)
        {
            _Connection.BaseURI = uri;
            _Connection.InitializeEndpoints();
            var endPoints = _Connection.Endpoints.Where(o => o.GetEndpointObject() is GenericHttpRequest).Select(o => o.GetEndpointObject() as GenericHttpRequest);
            endPoints.ToList().ForEach(o =>
            {
                o.OnHttpMessageEvent += (sender, e) =>
                {
                    if (!String.IsNullOrEmpty(e))
                    {
                        OnHttpMessageEvent?.Invoke(sender, e);
                    }
                };
                o.OnLogEvent += (sender, e) =>
                {
                    OnLogEvent?.Invoke(sender, e);
                };
            });

            EndpointType login = _Connection.GetEndpoint(StandardEndpointsEnumType.Login_Endpoint);
            login.SendEndpoint(new object[] { username, password }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();

            _Connection.SetCredentials(login);
        }
        public void SetResolution(int width, int height)
        {
            _Resolution = new Resolution
            {
                Width = width,
                Height = height
            };
        }
        public void GenericRequest(string requestName, object[] parameters = null)
        {
            EndpointType endpoint = _Connection.GetCommand_HttpType(requestName);
            if (endpoint != null)
            {
                endpoint.SendEndpoint(parameters, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else
            {
                throw new ArgumentException($"Endpoint '{requestName}' not found.");
            }
        }
        //private async Task<Configuration.json.Streamer.Result> GetStreamerInfo()
        //{
        //    EndpointType streamerInfo_Endpoint = _Connection.GetEndpoint(StandardEndpointsEnumType.StreamerInformation_Endpoint);
        //    if (streamerInfo_Endpoint.GetEndpointObject() is GenericHttpRequest httpRequest)
        //    {
        //        StringBuilder sb = new StringBuilder();
        //        httpRequest.OnHttpMessageEvent += (sender, e) =>
        //        {
        //            if (!String.IsNullOrEmpty(e))
        //            {
        //                sb.AppendLine(e);                        
        //            }
        //        };

        //        await streamerInfo_Endpoint.SendEndpoint(parameters: null, onSuccess: (strg) => { }, onError: (strg) => { } );
        //        string st = sb.ToString();
        //        Configuration.json.Streamer.Root streamerInfo = StreamerInformationType.Deserialize(sb.ToString());

        //        return streamerInfo.Result;
        //    }
        //    return null;
        //}

        /// <summary>
        /// Send a full string of text to PiKVM to be printed as if it was typed on the keyboard.
        /// </summary>
        /// <param name="inputText">String to send to PiKVM.</param>
        /// <param name="slow"><c>true</c> to send each character with longer pause between each keystroke, <c>false</c> by default.</param>
        /// <param name="limit">Limits number of characters being sent by call. 0 to have no limit, default limit is 1024.</param>
        /// <param name="keymap">Keymap specifies keyboard layout/language for Keys, defaults to system default.</param>
        /// <exception cref="ArgumentException"></exception>
        public void SendPrintText(string inputText, bool? slow = null, int? limit = null, string keymap = null)
        {
            EndpointType keyboardInput = _Connection.GetEndpoint(StandardEndpointsEnumType.PrintText_Endpoint);
            if (keyboardInput != null)
            {
                if      (slow == null && limit == null && keymap == null)
                    keyboardInput.SendEndpoint(new object[] { inputText }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                else if (limit == null && keymap == null)
                    keyboardInput.SendEndpoint(new object[] { inputText, slow }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                else if (keymap == null)
                    keyboardInput.SendEndpoint(new object[] { inputText, slow, limit }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                else
                    keyboardInput.SendEndpoint(new object[] { inputText, slow, limit, keymap }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else
            {
                throw new ArgumentException("Keyboard input endpoint not found.");
            }
        }
        /// <summary>
        /// Sends a single character to PiKVM as if it was typed on the keyboard.
        /// </summary>
        /// <param name="inputCharacter">Single character to send to PiKVM.</param>
        /// <param name="state"><c>true</c>, to hold key in the down position. <c>false</c>, to release key to the up position.</param>
        /// <param name="finish">Releases non-modifier keys right after pressing them so that they don't get stuck when the connection is not stable. Defaults to false.</param>
        /// <exception cref="ArgumentException"></exception>
        public void SendKeyboardKey(char inputCharacter, bool? state = null, bool? finish = null)
        {
            EndpointType keyboardInput = _Connection.GetEndpoint(StandardEndpointsEnumType.SendKeyboardKey_Endpoint);
            string specialCharArray = "!@#$%^&*()_+{}|:\"<>?~";
            if (keyboardInput != null)
            {
                if (char.IsLower(inputCharacter) || char.IsDigit(inputCharacter))
                {
                    string inputText = inputCharacter.CharToWebNameKey();
                    if (state == null && finish == null)
                        keyboardInput.SendEndpoint(new object[] { inputText }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                    if (finish == null)
                        keyboardInput.SendEndpoint(new object[] { inputText, state }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                    else
                        keyboardInput.SendEndpoint(new object[] { inputText, state, finish }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                }
                else if (char.IsUpper(inputCharacter) || specialCharArray.Contains(inputCharacter))
                { 
                    string inputText = inputCharacter.CharToWebNameKey();
                    if (state == null && finish == null)
                    {
                        keyboardInput.SendEndpoint(new object[] { "ShiftLeft", true, false }).ConfigureAwait(false).GetAwaiter().GetResult();
                        keyboardInput.SendEndpoint(new object[] { inputText }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                        keyboardInput.SendEndpoint(new object[] { "ShiftLeft", false, true }).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    if (finish == null)
                    {
                        keyboardInput.SendEndpoint(new object[] { "ShiftLeft", true, false }).ConfigureAwait(false).GetAwaiter().GetResult();
                        keyboardInput.SendEndpoint(new object[] { inputText, state }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                        keyboardInput.SendEndpoint(new object[] { "ShiftLeft", false, true }).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                    else
                    {
                        keyboardInput.SendEndpoint(new object[] { "ShiftLeft", true, false }).ConfigureAwait(false).GetAwaiter().GetResult();
                        keyboardInput.SendEndpoint(new object[] { inputText, state, finish }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                        keyboardInput.SendEndpoint(new object[] { "ShiftLeft", false, true }).ConfigureAwait(false).GetAwaiter().GetResult();
                    }
                }
                else if (Regex.Unescape("\\n") == inputCharacter.ToString() || Regex.Unescape("\\r") == inputCharacter.ToString() || inputCharacter == '\n' || inputCharacter == '\r')
                {
                    keyboardInput.SendEndpoint(new object[] { "Enter" }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                }
                else if (inputCharacter == ' ')
                {
                    keyboardInput.SendEndpoint(new object[] { "Space" }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                }
                else if (inputCharacter == '\t')
                {
                    keyboardInput.SendEndpoint(new object[] { "Tab" }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                }
                else if (inputCharacter == '\b' || inputCharacter == '\x7f' || inputCharacter == '\x08')
                {
                    keyboardInput.SendEndpoint(new object[] { "Backspace" }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                }
                else
                {
                    throw new ArgumentException($"Invalid character specified cannot process character '{inputCharacter}'.");
                }
            }
            else
            {
                throw new ArgumentException("Keyboard input endpoint not found.");
            }
        }
        /// <summary>
        /// Sends a keyboard shortcut, or key combination, to be typed on the PiKVM. Expected Value Key{LETTER} or Digit{NUMBER}. Other valid identifiers, 'Enter', 'Escape', 'Backspace', 'Tab', 'ControlLeft' 'AltLeft' 'Delete'
        /// </summary>
        /// <param name="keyNames">Each key name for each parameter.</param>
        /// <exception cref="ArgumentException"></exception>
        public void SendKeyboardMultiKey(string[] keyNames)
        {
            EndpointType keyboardInput = _Connection.GetEndpoint(StandardEndpointsEnumType.SendKeyboardMultiKeyPress_Endpoint);
            if (keyboardInput != null)
            {
                object[] objects = new object[] { String.Join(",", keyNames) };
                keyboardInput.SendEndpoint(objects, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else
            {
                throw new ArgumentException("Keyboard input endpoint not found.");
            }
        }
        public void SetMouseMode(MouseOutputType outputType)
        {
            EndpointType mouseType = _Connection.GetEndpoint(StandardEndpointsEnumType.MouseOutputType_Endpoint);
            mouseType.SendEndpoint(new object[] { outputType.ToString() }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        /// <summary>
        /// Moves mouse based on the specified mouse mode and position.
        /// </summary>
        /// <param name="mouseMode">Relative movement or absolute movement</param>
        /// <param name="xPos">X position to move mouse.</param>
        /// <param name="yPos">Y position to move mouse.</param>
        public void MoveMouse(MouseMode mouseMode, int xPos, int yPos)
        {
            MoveMouse(mouseMode, (double)xPos, (double)yPos);
        }
        /// <summary>
        /// Moves mouse based on the specified mouse mode and position.
        /// </summary>
        /// <param name="mouseMode">Relative movement or absolute movement</param>
        /// <param name="xPos">X position to move mouse.</param>
        /// <param name="yPos">Y position to move mouse.</param>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        public void MoveMouse(MouseMode mouseMode, double xPos, double yPos)
        {
            if (mouseMode == MouseMode.Absolute)
            {
                // Convert point to absolute coordinates
                EndpointType mouseMove_abs = _Connection.GetEndpoint(StandardEndpointsEnumType.MouseMoveAbsolute_Endpoint);
                double x = Abs_RemapX(xPos);
                double y = Abs_RemapX(yPos);
                mouseMove_abs.SendEndpoint(new object[] { x, y }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else if (mouseMode == MouseMode.Relative)
            {
                EndpointType mouseMove_rel = _Connection.GetEndpoint(StandardEndpointsEnumType.MouseMoveRelative_Endpoint);
                double x = Rel_RemapX(xPos);
                double y = Rel_RemapX(yPos);
                mouseMove_rel.SendEndpoint(new object[] { x, y }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
                // Convert point to relative coordinates
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(mouseMode), mouseMode, null);
            }
            // remap (xvalue-xResMin, 0, xResMax-1,-32768, 32767)
        }
        /// <summary>
        /// Click mouse button using PiKVM. This is a down and up action.
        /// </summary>
        /// <param name="mouseButton">Left, Right, or Middle click.</param>
        public void MouseClick(MouseButton mouseButton)
        {
            EndpointType MouseButton;
            MouseButton = _Connection.GetEndpoint(StandardEndpointsEnumType.MouseButton_Endpoint);
            MouseButton.SendEndpoint(new[] { mouseButton.ToString() }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        /// <summary>
        /// Sets the state of a mouse button, either pressed or released.
        /// </summary>
        /// <param name="mouseButton">Left, Right, or Middle button.</param>
        /// <param name="state"><c>true</c> for pressed and <c>false</c> for released.</param>
        public void MouseClickState(MouseButton mouseButton, bool state)
        {
            EndpointType MouseButton;
            MouseButton = _Connection.GetEndpoint(StandardEndpointsEnumType.MouseButtonState_Endpoint);
            MouseButton.SendEndpoint(new object[] { mouseButton.ToString(), state }, OnHttpMessage, OnLogMessage).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        private double Remap(double value, double in_min, double in_max, double out_min, double out_max)
        {
            double range = (in_max - in_min) > 0 ? (in_max - in_min) : 1; // Avoid division by zero
            double rslt = Math.Round((value - in_min) * (out_max - out_min) / (range) + out_min);
            double rsltClamp = Math.Min(Math.Max(rslt, out_min), out_max); // Clamp to output range
            return (int)rsltClamp;

        }
        private double RemapRelative(double value, double in_min, double in_max, double out_min, double out_max)
        {
            //in min and max is resolution
            double inRange = (in_max - in_min) > 0 ? (in_max - in_min) : 1; // Calculate the input range ... Avoid division by zero
            double outRange = (out_max - out_min) > 0 ? (out_max - out_min) : 1; // Calculate the output range ... Avoid division by zero
            double screenPrecentMove = value / inRange;
            double outMove = (screenPrecentMove * outRange) + out_min; // Calculate the output move based on the input move percentage
            double rslt = Math.Round(outMove);
            return (int)rslt;
        }
        private void OnHttpMessage(string message)
        {
            OnHttpMessageEvent?.Invoke(this, message);
        }
        private void OnLogMessage(LogMessage logMessage)
        {
            OnLogEvent?.Invoke(this, logMessage);
        }
    }
}
