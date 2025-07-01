using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PiKvmLibrary
{
    public struct LogMessage
    {
        public LogLevel LogLevel { get; set; }
        public Exception Exception { get; set; }
        public string Message { get; set; }
        public DateTime TimeStamp { get; set; }
    }
}
