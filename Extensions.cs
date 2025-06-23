using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PiKvmLibrary
{
    public static class AppTypeDetector
    {
        public static bool IsWpfApp() =>
            System.Windows.Application.Current != null;

        public static bool IsWinFormsApp() =>
            System.Windows.Forms.Application.MessageLoop;


        public static async Task<string> ReadStreamWithCancellationAsync(this StreamReader reader, CancellationToken token)
        {
            const int bufferSize = 4096;
            return await reader.ReadStreamWithCancellationAsync(bufferSize, token);
        }
        public static async Task<string> ReadStreamWithCancellationAsync(this StreamReader reader, int bufferSize, CancellationToken token)
        {
            var buffer = new char[bufferSize];
            var result = new StringBuilder();

            while (true)
            {
                // Start the read operation
                var readTask = reader.ReadAsync(buffer, 0, buffer.Length);

                // Wait for it to complete or cancel
                var completedTask = await Task.WhenAny(readTask, Task.Delay(Timeout.Infinite, token));

                if (completedTask == readTask)
                {
                    int charsRead = readTask.Result;
                    if (charsRead == 0)
                        break; // End of stream

                    result.Append(buffer, 0, charsRead);
                }
                else
                {
                    // Task.Delay triggered => cancellation requested
                    throw new OperationCanceledException(token);
                }
            }

            return result.ToString();
        }
    }
}
