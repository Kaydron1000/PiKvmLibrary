using PiKvmLibrary.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PiKvmLibrary
{
    public class FfmpegProcessor : IPostProcessStream
    {
        private CancellationTokenSource _CtsffmpegProcess;
        private CancellationTokenSource _CtsFfmpegOutput;
        private CancellationTokenSource _CtsFfmpegErrors;

        private FFmpegConfigurationType _FfmpegConfiguration;

        private Task _ProcessFfmpegErrors;
        private Task _ProcessFfmpegOutput;

        private Process _FfmpegProcess;
        private StreamWriter _FfmpegInput;
        private StreamReader _FfmpegOutput;
        private StreamReader _FfmpegError;

        public event EventHandler<LogMessage> OnLogEvent;
        public event EventHandler<LogMessage> PostProcessLog;
        public event EventHandler<string> PostProcessText;
        public event EventHandler<byte[]> PostProcessData;

        public FFmpegConfigurationType FfmpegConfiguration 
        { 
            get => _FfmpegConfiguration;
        }

        public void Initialize(PostProcessStreamType postProcessStreamConfiguration)
        {
            if (postProcessStreamConfiguration.Item is FFmpegConfigurationType configuration)
            {
                _CtsffmpegProcess = new CancellationTokenSource();
                _CtsffmpegProcess.Token.Register(() => Stop_FfmpegProcess());
                _FfmpegConfiguration = configuration;
                StartPostProcessStream();
            }
        }

        public void ProcessData(WebSocketReceiveResult result, byte[] data, int offset = 0)
        {
            if (result.MessageType == WebSocketMessageType.Text)
            {
                string message = Encoding.UTF8.GetString(data, offset, result.Count);
                PushPostProcessTextEvent(message);
            }
            else if (result.MessageType == WebSocketMessageType.Binary)
            {
                // Handle binary data if needed
                _FfmpegInput.BaseStream.WriteAsync(data, offset, result.Count);
            }
        }
        private void Initializeffmpeg(string ffmpegpath, string ffmpegArgs)
        {
            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Initializing ffmpeg from: {ffmpegpath}", TimeStamp = DateTime.Now });
            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Initializing ffmpeg with parameters: {ffmpegArgs}", TimeStamp = DateTime.Now });
            try
            {
                if (ffmpegpath == null || ffmpegArgs == null)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = "ffmpeg path or arguments are null.", TimeStamp = DateTime.Now });
                    return;
                }
                if (!File.Exists(ffmpegpath))
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = $"ffmpeg executable not found at: {ffmpegpath}", TimeStamp = DateTime.Now });
                    return;
                }
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

                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"Initialized ffmpeg from: {ffmpegpath}", TimeStamp = DateTime.Now });
                }
            }
            catch (Exception ex)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Exception = ex, Message = "Error initializing ffmpeg process.", TimeStamp = DateTime.Now });
                return;
            }
        }

        public void StartPostProcessStream()
        {
            Start_FfmpegProcess();
            Start_FfmpegErrorStream();
            Start_FfmpegOutputStream();
        }
        public void Start_FfmpegProcess()
        {
            if (_FfmpegProcess == null || _FfmpegProcess.HasExited)
            {
                Initializeffmpeg(_FfmpegConfiguration.ApplicationPath, _FfmpegConfiguration.ApplicationArguments);
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
                _CtsFfmpegErrors = CancellationTokenSource.CreateLinkedTokenSource(_CtsffmpegProcess.Token);
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
                _CtsFfmpegOutput = CancellationTokenSource.CreateLinkedTokenSource(_CtsffmpegProcess.Token);
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Stopped ffmpeg output stream", TimeStamp = DateTime.Now });
            }
        }

        private async Task ProcessffmpegErrors(CancellationToken ffmpegCT)
        {
            while (!ffmpegCT.IsCancellationRequested)
            {
                try
                {
                    string line = await _FfmpegError.ReadStreamWithCancellationAsync(ffmpegCT);
                    PushFfmpegLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = line, TimeStamp = DateTime.Now }); //FFMPEG ERRORS
                }
                catch (OperationCanceledException exCanceled)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Exception = exCanceled, Message = "ffmpeg error stream reading was canceled.", TimeStamp = DateTime.Now });
                    break;
                }
                catch (Exception ex)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Exception = ex, Message = "Error reading ffmpeg error stream.", TimeStamp = DateTime.Now });
                    break;
                }
            }
        }

        private async Task ProcessFfmpegVideoStreamOutput(StreamReader ffmpegOutput, CancellationToken fmpegOutputCT)
        {
            byte[] buffer;
            if (_FfmpegConfiguration.OutputBufferSize <= 0)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = "Output buffer size is not set or invalid. Default of 4096 will be used.", TimeStamp = DateTime.Now });
                buffer = new byte[4096];
            }
            else
            {
                buffer = new byte[_FfmpegConfiguration.OutputBufferSize];
            }
            bool isReading = false;
            MemoryStream ms = new MemoryStream();
            while (_FfmpegProcess.HasExited && !fmpegOutputCT.IsCancellationRequested)
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
                                // Finish the previous frame
                                byte[] frame = ms.ToArray();
                                //_ImageFrameQueue.Enqueue(frame);
                                PushPostProcessDataEvent(frame);
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
                            byte[] frame = ms.ToArray();
                            //_ImageFrameQueue.Enqueue(frame);
                            PushPostProcessDataEvent(frame);

                            ms.SetLength(0);
                            isReading = false;
                        }
                    }
                }
                catch (OperationCanceledException exCanceled)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Exception = exCanceled, Message = "Canceled processing Ffmpeg output.", TimeStamp = DateTime.Now });
                    break;
                }
                catch (Exception ex)
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Exception = ex, Message = "Error processing Ffmpeg output.", TimeStamp = DateTime.Now });
                    break;
                }
            }
        }

        private void PushPostProcessDataEvent(byte[] data)
        {
            if (AppTypeDetector.IsWpfApp())
                Dispatcher.CurrentDispatcher.InvokeAsync(() => PostProcessData?.Invoke(this, data));
            else
                PostProcessData?.BeginInvoke(this, data, null, null);
        }
        private void PushPostProcessTextEvent(string logMessage)
        {
            if (AppTypeDetector.IsWpfApp())
                Dispatcher.CurrentDispatcher.InvokeAsync(() => PostProcessText?.Invoke(this, logMessage));
            else
                PostProcessText?.BeginInvoke(this, logMessage, null, null);
        }
        private void PushFfmpegLogEvent(LogMessage logMessage)
        {
            if (AppTypeDetector.IsWpfApp())
                Dispatcher.CurrentDispatcher.InvokeAsync(() => PostProcessLog?.Invoke(this, logMessage));
            else
                PostProcessLog?.BeginInvoke(this, logMessage, null, null);
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
