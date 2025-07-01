using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PiKvmLibrary.Configuration;
using System.Security.Cryptography;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Data.Common;
using System.Windows.Threading;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Remoting.Contexts;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
using System.Windows.Shapes;
using System.Windows.Documents;

namespace PiKvmLibrary
{
    public class GenericHttpRequest
    {
        private HttpClient _Client;
        private HttpClientHandler _ClientHandler;
        private CookieContainer _CookieContainer;

        private CancellationTokenSource _Cts_ReadingLog;

        private ConnectionType _ConnectionConfiguration;
        private EndpointType _EndPointConfiguration;
        private HttpEndpointType _HttpRequestConfiguration;

        public event EventHandler<LogMessage> OnLogEvent;
        public event EventHandler<string> OnHttpMessageEvent;
        public GenericHttpRequest()
        {
            _Cts_ReadingLog = new CancellationTokenSource();
            _CookieContainer = new CookieContainer();

            _ClientHandler = new HttpClientHandler();
            _ClientHandler.ServerCertificateCustomValidationCallback = ServerCertificateCustomValidationCallback_Handler;
            _ClientHandler.CookieContainer = _CookieContainer; // Use the CookieContainer to manage cookies
            _ClientHandler.UseCookies = true;

            _Client = new HttpClient(_ClientHandler);
        }
        public GenericHttpRequest(ConnectionType connectionConfiguration, EndpointType endpointConfiguration) : this()
        {
            Initialize(connectionConfiguration, endpointConfiguration);
        }
        public void Initialize(ConnectionType connection, EndpointType endpointConfiguration)
        {
            if (connection == null || endpointConfiguration?.Item == null)
            {
                throw new ArgumentNullException("Connection and endpoint configuration cannot be null.");
            }
            if (endpointConfiguration.Item is HttpEndpointType httpEndpointType)
            {
                _EndPointConfiguration = endpointConfiguration;
                _HttpRequestConfiguration = httpEndpointType;

                string baseUri = connection.BaseURI;
                if (!baseUri.ToLower().StartsWith("https://"))
                {
                    if (baseUri.ToLower().StartsWith("wss://"))
                    {
                        baseUri = baseUri.Replace("wss://", "https://");
                    }
                    if (baseUri.ToLower().StartsWith("ws://"))
                    {
                        baseUri = baseUri.Replace("ws://", "https://");
                    }
                    else if (baseUri.ToLower().StartsWith("http://"))
                    {
                        baseUri = baseUri.Replace("http://", "https://");
                    }
                    else
                    {
                        baseUri = "https://" + baseUri;
                    }
                }

                _Client.BaseAddress = new Uri(baseUri);
                _Client.Timeout = TimeSpan.FromSeconds(_EndPointConfiguration.EndpointTimeout_Sec);
            }
            else
            {
                throw new ArgumentException("Invalid endpoint configuration", nameof(endpointConfiguration));
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
                _Client.DefaultRequestHeaders.Add(userPrompt, username);
                _Client.DefaultRequestHeaders.Add(passwordPrompt, password);
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

            bool ignoreCertificateErrors = true;
            if (_EndPointConfiguration != null && _EndPointConfiguration.IgnoreCertificateErrorsSpecified)
                ignoreCertificateErrors = _EndPointConfiguration.IgnoreCertificateErrors;
            else if (_ConnectionConfiguration != null && _ConnectionConfiguration.IgnoreCertificateErrorsSpecified)
                ignoreCertificateErrors = _ConnectionConfiguration.IgnoreCertificateErrors;

            // Log the errors or handle specific issues here
            if (sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateNotAvailable) && ignoreCertificateErrors)
                return true; // Allow connections with no certificate

            return true; // Only allow connections with no errors
        }
        public void AddDefaultHeaders(Dictionary<string, string> headers)
        {
            foreach (var header in headers)
                AddDefaultHeader(header.Key, header.Value);
        }
        public void AddDefaultHeader(string name, string value)
        {
            if (_Client.DefaultRequestHeaders.Contains(name))
            {
                var header = _Client.DefaultRequestHeaders.First(o => o.Key.Equals(name));
                if (header.Value.Any(o => o == value))
                {
                    return; // Header already exists with the same value
                }
                else
                {
                    // Remove existing header with the same name
                    _Client.DefaultRequestHeaders.Remove(name);
                }
            }
            _Client.DefaultRequestHeaders.Add(name, value);
        }
        public async Task GetResponseAsync(string url, Action<string> onSuccess = null, Action<Exception> onError = null)
        {
            try
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Initializing Get request to {url} ", TimeStamp = DateTime.Now });

                Task<HttpResponseMessage> tsk = _Client.GetAsync(url);

                await ReadResult(tsk.Result, onSuccess, onError);
            }
            catch (Exception ex)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Exception = ex, Message = $"Exception during Get request to {url}", TimeStamp = DateTime.Now });
                onError?.Invoke(ex);
            }
        }

        public async Task PostRequestAsync(string url, HttpContent content, Action<string> onSuccess = null, Action<Exception> onError = null)
        {
            try
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Debug, Message = $"Initializing Post request to {url} ", TimeStamp = DateTime.Now });

                Task<HttpResponseMessage> tsk = _Client.PostAsync(url, content);

                await ReadResult(tsk.Result, onSuccess, onError);
            }
            catch (Exception ex)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Exception = ex, Message = $"Exception during Post request to {url}", TimeStamp = DateTime.Now });
                onError?.Invoke(ex);
            }
        }

        private async Task ReadResult(HttpResponseMessage response, Action<string> onSuccess, Action<Exception> onError)
        {
            try
            {
                response.EnsureSuccessStatusCode();
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Information, Message = $"{response.RequestMessage.Method.Method} request to {response.RequestMessage.RequestUri} completed successfully", TimeStamp = DateTime.Now });
                
                using (Stream stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                using (StreamReader reader = new StreamReader(stream))
                {
                    int cnta = 0;
                    string line;
                    char[] buffer = new char[4096];
                    do
                    {
                        cnta = await reader.ReadAsync(buffer, 0, 4096);
                        if (cnta > 0)
                        {
                            line = new string(buffer, 0, cnta);
                            PushHttpMessageEvent(line);
                        }
                    } while (cnta > 0 && response.StatusCode == HttpStatusCode.OK);
                    //string lin = await reader.ReadAsync();
                    //while (lin != null)
                    //{
                    //    PushHttpMessageEvent(lin);
                    //    lin = await reader.ReadLineAsync();
                    //}


                    //while ((line = reader.ReadLine()) != null)
                    //{
                    //    PushHttpMessageEvent(line);
                    //}
                }
            }
            catch (Exception ex)
            {
                PushLogEvent(new LogMessage() { LogLevel = LogLevel.Error, Exception = ex, Message = $"Error reading {response.RequestMessage.Method.Method} response from {response.RequestMessage.RequestUri}.", TimeStamp = DateTime.Now });
                onError?.Invoke(ex);
            }
            onSuccess?.Invoke("");
        }

        private void PushLogEvent(LogMessage logMessage)
        {
            Dispatcher.CurrentDispatcher.InvokeAsync(() => OnLogEvent?.Invoke(this, logMessage));
            OnLogEvent?.BeginInvoke(this, logMessage, null, null);
        }
        private void PushHttpMessageEvent(string Message)
        {
            Dispatcher.CurrentDispatcher.InvokeAsync(() => OnHttpMessageEvent?.Invoke(this, Message));
            OnHttpMessageEvent?.BeginInvoke(this, Message, null, null);
        }
    }
}
