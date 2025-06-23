using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PiKvmLibrary
{
    public enum MouseMode
    {
        Absolute,
        Relative
    }
    public class PikvmInterface
    {
        public Pikvm_Apiws PikvmApiws { get; set; }
        public Pikvm_ApiVideoStream PikvmApiVideoStream { get; set; }
        public Pikvm_HttpClient PikvmHttpRequest { get; set; }

        public bool ffmpegLogEnabled { get; set; }

        // Keyboard
        //// Enabled
        //// Logging

        // Mouse
        //// Enabled
        //// Logging

        // Video
        //// Enabled
        //// Logging

        // ATX
        //// Enabled
        //// Logging
        public void MoveMouse(MouseMode mouseMode, Point point)
        {
            if (mouseMode == MouseMode.Absolute)
            {
                // Convert point to absolute coordinates
                PikvmApiws.SendMouseMove(Convert.ToInt32(point.X), Convert.ToInt32(point.Y)).Wait();
            }
            else if (mouseMode == MouseMode.Relative)
            {
                // Convert point to relative coordinates
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(mouseMode), mouseMode, null);
            }
        }
        public void SetMouseMode(MouseMode mouseMode)
        {
            string response = null;
            switch (mouseMode)
            {
                case MouseMode.Absolute:
                    response = PikvmHttpRequest.PostRequest("api/hid/set_params?mouse_output=usb_rel", null).Result;
                    // Set mouse mode to absolute
                    break;
                case MouseMode.Relative:
                    response = PikvmHttpRequest.PostRequest("api/hid/set_params?mouse_output=usb", null).Result;
                    // Set mouse mode to relative
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mouseMode), mouseMode, null);
            }
        }
    }
}
