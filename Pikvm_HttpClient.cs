using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PiKvmLibrary
{
    public class Pikvm_HttpClient
    {
        private const string USERNAME_PROMPT = "X-KVMD-User";
        private const string PASSWORD_PROMPT = "X-KVMD-Passwd";
        private const string URI_PATH = "https://192.168.1.183/";

        private HttpClient _Client;
        private HttpClientHandler _ClientHandler;
        private CookieContainer _CookieContainer;

        private CancellationTokenSource _Cts_ReadingLog;

        public event EventHandler<LogMessage> OnLogEvent;
        public event EventHandler<LogMessage> OnHttpLogEvent;

        private bool _IgnoreCerticateErrors;
        public bool IgnoreCerticateErrors { get; set; }

        public Pikvm_HttpClient()
        {
            _Cts_ReadingLog = new CancellationTokenSource();
            _CookieContainer = new CookieContainer();

            _ClientHandler = new HttpClientHandler();
            //_ClientHandler.ServerCertificateCustomValidationCallback = ServerCertificateCustomValidationCallback_Handler;
            _ClientHandler.CookieContainer = _CookieContainer; // Use the CookieContainer to manage cookies
            _ClientHandler.UseCookies = true;
            
            _Client = new HttpClient(_ClientHandler);
            _Client.BaseAddress = new Uri(URI_PATH);
        }



        public void SetCredentials(string username, string password)
        {
            _Client.DefaultRequestHeaders.Add(USERNAME_PROMPT, username);
            _Client.DefaultRequestHeaders.Add(PASSWORD_PROMPT, password);
        }

        public void SetCredentials(CookieCollection authCookies)
        {
            SetAuthCookies(authCookies);
        }

        private void SetAuthCookies(CookieCollection cookies)
        {
            _ClientHandler.CookieContainer = new CookieContainer();
            foreach (Cookie cookie in cookies)
                _ClientHandler.CookieContainer.Add(cookie);
        }

        public CookieCollection GetAuthCookies()
        {
            // Return the cookies from the CookieContainer
            var cookies = _CookieContainer.GetCookies(_Client.BaseAddress);
            return cookies;
        }
        private bool ServerCertificateCustomValidationCallback_Handler(HttpRequestMessage sender, X509Certificate2 cert, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            // Check specific certificate errors if necessary
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;

            // Log the errors or handle specific issues here
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable) && _IgnoreCerticateErrors)
                return true; // Allow connections with no certificate

            return true; // Only allow connections with no errors
        }
        public void EnableHttpLogStreamAsync()
        {
            ReadLogStream(_Cts_ReadingLog.Token);
        }
        public string GetResponse(string url)
        {
            try
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Initializing Get request to {url} ", TimeStamp = DateTime.Now });
                HttpResponseMessage response = _Client.GetAsync(url).Result;
                HttpResponseMessage success = response.EnsureSuccessStatusCode();
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Completed Get request to {url}", TimeStamp = DateTime.Now });
                string tt = response.Content.ReadAsStringAsync().Result;
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Response {tt}", TimeStamp = DateTime.Now });
                return response.Content.ReadAsStringAsync().Result;
            }
            catch (HttpRequestException e)
            {
                // Handle exception
                return e.Message;
            }
        }
        public async Task PostLoginCookie(string username, string password)
        {
            try
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Initializing POST request to /api/auth/login", TimeStamp = DateTime.Now });

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("user", username),
                    new KeyValuePair<string, string>("passwd", password)
                });

                Action action = () =>
                {
                    HttpResponseMessage response = _Client.PostAsync("/api/auth/login", content).Result;
                    response.EnsureSuccessStatusCode();
                };

                action.Invoke();
                
                var uri = new Uri("https://pikvm");

                foreach (Cookie cookie in _CookieContainer.GetCookies(_Client.BaseAddress))
                {
                    PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"{cookie.Name} = {cookie.Value}", TimeStamp = DateTime.Now });
                }
                username = null;
                password = null; // Clear sensitive data

                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Completed POST request to /api/auth/login", TimeStamp = DateTime.Now });
            }
            catch (HttpRequestException e)
            {
                // Handle exception
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = $"Error during login: {e.Message}", TimeStamp = DateTime.Now });
            }
        }


        private void UpdateValue(double value, string propertyName)
        {
            //if (AppTypeDetector.IsWpfApp())
            //    Dispatcher.CurrentDispatcher.Invoke(() => FPS = value);
            //else
            //{
            //    Action act = new Action(() => FPS = value);
            //    act.Invoke();
            //}
        }

        public async Task<string> PostRequest(string url, string jsonData)
        {
            try
            {
                if (jsonData == null)
                    jsonData = string.Empty;

                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Initializing POST request to {url} with data: {jsonData}", TimeStamp = DateTime.Now });


                StringContent content = null;
                if (!string.IsNullOrEmpty(jsonData))
                    content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                
                HttpResponseMessage response = _Client.PostAsync(url, content).Result;
                // Check if the response is successful
                response.EnsureSuccessStatusCode();
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Completed POST request to {url} with data: {jsonData}", TimeStamp = DateTime.Now });
                return response.Content.ReadAsStringAsync().Result;
            }
            catch (HttpRequestException e)
            {
                // Handle exception
                return "Invalid command";
            }
        }
        private async Task ReadLogStream(CancellationToken cancellationToken)
        {

            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Starting to read log stream...", TimeStamp = DateTime.Now });
            // Task<Stream> readStream = _Client.GetStreamAsync("/api/log?follow=1");
            Task<Stream> readStream = _Client.GetStreamAsync("/api/log");

            using (Stream stream = await readStream)
            {
                using (StreamReader reader = new StreamReader(stream))
                {
                    try
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            PushHttpLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = line, TimeStamp = DateTime.Now });
                        }
                    }
                    catch (Exception ex)
                    {
                        PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Message = $"Error reading log stream: {ex.Message}", TimeStamp = DateTime.Now });
                    }
                }
            }
            PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = "Finished reading log stream.", TimeStamp = DateTime.Now });
        }

        private void PushLogEvent(LogMessage logMessage)
        {
            if (AppTypeDetector.IsWpfApp())
                Dispatcher.CurrentDispatcher.InvokeAsync(() => OnLogEvent?.Invoke(this, logMessage));
            else
                OnLogEvent?.BeginInvoke(this, logMessage, null, null);
        }
        private void PushHttpLogEvent(LogMessage logMessage)
        {
            if (AppTypeDetector.IsWpfApp())
                Dispatcher.CurrentDispatcher.InvokeAsync(() => OnHttpLogEvent?.Invoke(this, logMessage));
            else
                OnHttpLogEvent?.BeginInvoke(this, logMessage, null, null);
        }
    }
}
