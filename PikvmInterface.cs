using PiKvmLibrary.Configuration;
using System;
using System.Collections.Generic;
using System.IO.Packaging;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Text;
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
    public class PikvmInterface
    {
        private ConfigurationData<PiKvmLibraryConfigurationType> _Configuration;
        private PiKvmLibraryConfigurationType _AppConfiguration;
        private ConnectionType _Connection;
        private Configuration.json.Streamer.Resolution _Resolution;
        public Func<double, double> Rel_RemapX { get; set; }
        public Func<double, double> Rel_RemapY { get; set; }
        public Func<double, double> Abs_RemapX { get; set; }
        public Func<double, double> Abs_RemapY { get; set; }
        public PikvmInterface()
        {
            _Configuration = new ConfigurationData<PiKvmLibraryConfigurationType>();
            _AppConfiguration = _Configuration.ApplicationConfiguration;
            _Connection = _AppConfiguration.Connections.Connection.First();
            _Resolution = new Configuration.json.Streamer.Resolution
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

            EndpointType login = _Connection.GetEndpoint(StandardEndpointsEnumType.Login_Endpoint);
            login.SendEndpoint(new object[] { username, password }).ConfigureAwait(false).GetAwaiter().GetResult();

            _Connection.SetCredentials(login);
        }
        public void SetResolution(int width, int height)
        {
            _Resolution = new Configuration.json.Streamer.Resolution
            {
                Width = width,
                Height = height
            };
        }
        private async Task<Configuration.json.Streamer.Result> GetStreamerInfo()
        {
            EndpointType streamerInfo_Endpoint = _Connection.GetEndpoint(StandardEndpointsEnumType.StreamerInformation_Endpoint);
            if (streamerInfo_Endpoint.GetEndpointObject() is GenericHttpRequest httpRequest)
            {
                StringBuilder sb = new StringBuilder();
                httpRequest.OnHttpMessageEvent += (sender, e) =>
                {
                    if (!String.IsNullOrEmpty(e))
                    {
                        sb.AppendLine(e);                        
                    }
                };

                await streamerInfo_Endpoint.SendEndpoint(parameters: null, onSuccess: (strg) => { }, onError: (strg) => { } );
                string st = sb.ToString();
                Configuration.json.Streamer.Root streamerInfo = StreamerInformationType.Deserialize(sb.ToString());

                return streamerInfo.Result;
            }
            return null;
        }
        public void SetMouseMode(MouseOutputType outputType)
        {
            EndpointType mouseType = _Connection.GetEndpoint(StandardEndpointsEnumType.MouseOutputType_Endpoint);
            mouseType.SendEndpoint(new object[] { outputType.ToString() }).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        public void MoveMouse(MouseMode mouseMode, int xPos, int yPos)
        {
            MoveMouse(mouseMode, (double)xPos, (double)yPos);
        }
        public void MoveMouse(MouseMode mouseMode, double xPos, double yPos)
        {
            if (mouseMode == MouseMode.Absolute)
            {
                // Convert point to absolute coordinates
                EndpointType mouseMove_abs = _Connection.GetEndpoint(StandardEndpointsEnumType.MouseMoveAbsolute_Endpoint);
                double x = Abs_RemapX(xPos);
                double y = Abs_RemapX(yPos);
                mouseMove_abs.SendEndpoint(new object[] { x, y }).ConfigureAwait(false).GetAwaiter().GetResult();
            }
            else if (mouseMode == MouseMode.Relative)
            {
                EndpointType mouseMove_rel = _Connection.GetEndpoint(StandardEndpointsEnumType.MouseMoveRelative_Endpoint);
                double x = Rel_RemapX(xPos);
                double y = Rel_RemapX(yPos);
                mouseMove_rel.SendEndpoint(new object[] { x, y }).ConfigureAwait(false).GetAwaiter().GetResult();
                // Convert point to relative coordinates
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(mouseMode), mouseMode, null);
            }
            // remap (xvalue-xResMin, 0, xResMax-1,-32768, 32767)
        }
        public void MouseClick(MouseButton mouseButton)
        {
            EndpointType MouseButton = _Connection.GetEndpoint(StandardEndpointsEnumType.MouseButton_Endpoint);
            MouseButton.SendEndpoint(new[] { mouseButton.ToString() }).ConfigureAwait(false).GetAwaiter().GetResult();
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
    }
}
