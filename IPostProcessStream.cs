using PiKvmLibrary.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace PiKvmLibrary
{
    internal interface IPostProcessStream
    {
        event EventHandler<LogMessage> OnLogEvent;
        event EventHandler<LogMessage> PostProcessLog;
        event EventHandler<string> PostProcessText;
        event EventHandler<byte[]> PostProcessData;
        void Initialize(PostProcessStreamType postProcessStreamConfiguration);

        void ProcessData(WebSocketReceiveResult result, byte[] data, int offset = 0);
    }
}
