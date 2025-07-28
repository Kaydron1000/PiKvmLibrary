using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Security.Cryptography;
using System.Windows.Media.Animation;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Runtime.Remoting.Contexts;

namespace PiKvmLibrary.Configuration
{
    public partial class ConnectionType
    {
        private Dictionary<string, EndpointType> _Commands;

        private Dictionary<string, EndpointType> Commands
        {
            get
            {
                if (_Commands == null)
                    _Commands = GetAllConnectionCommands();

                return _Commands;
            }
        }
        public void InitializeEndpoints()
        {
            if (Endpoints != null && Endpoints.Length > 0)
            {
                foreach(var endpoint in Endpoints)
                {
                    if (endpoint != null && endpoint.Item != null)
                    {
                        endpoint.InitializeEndpoint(this);
                    }
                }
            }
        }
        public void SetCredentials(string username, string password)
        {
            if (Endpoints != null && Endpoints.Length > 0)
            {
                foreach (var endpoint in Endpoints)
                {
                    var endpointObj = endpoint.GetEndpointObject();
                    if (endpointObj is GenericHttpRequest httpRequest)
                    {
                        httpRequest.SetCredentials(username, password);
                    }
                    else if (endpointObj is GenericWebsocket websocket)
                    {
                        websocket.SetCredentials(username, password);
                    }
                    else
                    {
                        throw new NotSupportedException($"Unsupported endpoint type: {endpoint.Item.GetType().Name}");
                    }
                }
            }
        }
        public void SetCredentials(CookieCollection authCookies)
        {
            if (Endpoints != null && Endpoints.Length > 0)
            {
                foreach (var endpoint in Endpoints)
                {
                    var endpointObj = endpoint.GetEndpointObject();
                    if (endpointObj is GenericHttpRequest httpRequest)
                    {
                        httpRequest.SetCredentials(authCookies);
                    }
                    else if (endpointObj is GenericWebsocket websocket)
                    {
                        websocket.SetCredentials(authCookies);
                    }
                    else
                    {
                        throw new NotSupportedException($"Unsupported endpoint type: {endpoint.Item.GetType().Name}");
                    }
                }
            }
        }
        public void SetCredentials(EndpointType loginEndpoint)
        {
            if (loginEndpoint.GetEndpointObject() is GenericHttpRequest httpRequest)
            {
                SetCredentials(httpRequest.GetAuthCookies());
            }
            else
            {
                throw new NotSupportedException($"Unsupported endpoint type: {loginEndpoint.Item.GetType().Name}");
            }
        }

        public EndpointType GetEndpoint(StandardEndpointsEnumType endpointType)
        {
            if (Endpoints == null || Endpoints.Length == 0)
                throw new NotSupportedException($"No endpoints are defined.");

            return Endpoints.FirstOrDefault(e => e.Name == endpointType.ToString());
        }
        public EndpointType GetCommand(StandardEndpointsEnumType endpointType)
        {
            if (Endpoints == null || Endpoints.Length == 0)
                throw new NotSupportedException($"No endpoints are defined.");

            return Endpoints.FirstOrDefault(e => e.Item == endpointType.ToString());
        }
        public EndpointType GetCommand_HttpType(string endpointCommandName)
        {
            if (Endpoints == null || Endpoints.Length == 0)
                throw new NotSupportedException($"No endpoints are defined.");

            EndpointType endpoint = null;
            if (Commands.ContainsKey(endpointCommandName))
                return endpoint = Commands[endpointCommandName];

            return null;
        }
        private Dictionary<string, EndpointType> GetAllConnectionCommands()
        {
            return Endpoints.Where(o => o.Item is HttpEndpointType).ToDictionary(o => (o.Item as HttpEndpointType).Name, o => o);
        }
    }
    public partial class EndpointType
    {
        private GenericHttpRequest _HttpEndpiontObject;
        private GenericWebsocket _WebsocketEndpiontObject;
        public void InitializeEndpoint(ConnectionType baseConnection)
        {
            if (Item == null)
                throw new NotSupportedException($"No endpoint exists.");

            if (Item is HttpEndpointType)
                _HttpEndpiontObject = new GenericHttpRequest(baseConnection, this);
            else if (Item is WebSocketType)
                _WebsocketEndpiontObject = new GenericWebsocket(baseConnection, this);
            else
                throw new NotSupportedException($"Unsupported endpoint type: {Item.GetType().Name}");
        }
        public object GetEndpointObject()
        {
            if (Item == null)
                return null;
            if (Item is HttpEndpointType)
            {
                if (_HttpEndpiontObject == null)
                    throw new NotSupportedException($"Endpoint is not initialized.");

                return _HttpEndpiontObject;
            }
            else if (Item is WebSocketType)
            {
                if (_WebsocketEndpiontObject == null)
                    throw new NotSupportedException($"Endpoint is not initialized.");
            
                return _WebsocketEndpiontObject;
            }
            else
            {
                throw new NotSupportedException($"Unsupported endpoint type: {Item.GetType().Name}");
            }
        }
        public async Task SendEndpoint(object[] parameters, Action<string> onHttpMessage = null, Action<LogMessage> onLog = null)
        {
            if (Item is HttpEndpointType httpRequest)
            {
                if (_HttpEndpiontObject == null)
                    throw new NotSupportedException($"Endpoint is not initialized.");

                Dictionary<string, Type> parameterRequestType;
                Dictionary<string, string> parameterValues = new Dictionary<string, string>();

                if (httpRequest.Parameters != null)
                {
                    int requiredParameterCnt = httpRequest.Parameters.Count(p => p.Optional == false);
                    int optionalParameterCnt = httpRequest.Parameters.Count(p => p.Optional == true);

                    // Validate parameters count
                    if (parameters.Length < requiredParameterCnt)
                        throw new ArgumentException($"Expected at least {requiredParameterCnt} parameters, but received {parameters.Length}.");
                    if (parameters.Length > requiredParameterCnt + optionalParameterCnt)
                        throw new ArgumentException($"Expected at most {requiredParameterCnt + optionalParameterCnt} parameters, but received {parameters.Length}.");


                    parameterRequestType = httpRequest.Parameters.ToDictionary(kvp => kvp.Name, kvp => TypeSelector(kvp));

                    //// Validation parameters count are the same
                    //if (httpRequest.Parameters.Length != parameters.Length)
                    //    throw new ArgumentException($"Expected {httpRequest.Parameters.Length} parameters, but received {parameters.Length}.");

                    // Validate parameters types
                    for (int indx = 0; indx < parameters.Length; indx++)
                    {
                        object parameter = parameters[indx];
                        ParameterType httpParam = httpRequest.Parameters[indx];

                        if (httpParam == null || parameter == null)
                            continue;

                        if (parameterRequestType.TryGetValue(httpParam.Name, out Type expectedType))
                        {
                            if (expectedType == typeof(string))
                            {
                                // no issues
                            }
                            else
                            {
                                if (expectedType == (parameter.GetType()))
                                    parameter = parameter; // already correct type
                                else if (expectedType == typeof(int) && int.TryParse(parameter.ToString(), out int intValue))
                                    parameter = intValue;
                                else if (expectedType == typeof(float) && float.TryParse(parameter.ToString(), out float floatValue))
                                    parameter = floatValue;
                                else if (expectedType == typeof(double) && double.TryParse(parameter.ToString(), out double doubleValue))
                                    parameter = doubleValue;
                                else if (expectedType == typeof(bool) && bool.TryParse(parameter.ToString(), out bool boolValue))
                                    parameter = boolValue;
                                else if (expectedType == typeof(char) && char.TryParse(parameter.ToString(), out char charValue))
                                    parameter = charValue;
                                else
                                    throw new InvalidCastException($"Parameter '{httpParam.Name}' does not match expected type '{expectedType.Name}' for value '{parameter}'.");
                            }
                        }
                        else
                        {
                            throw new KeyNotFoundException($"Request parameter '{httpParam.Name}' not found in request type.");
                        }
                    }

                    // Create a dictionary for parameter values
                    for (int indx = 0; indx < parameters.Length; indx++)
                    {
                        object parameter = parameters[indx];
                        ParameterType httpParam = httpRequest.Parameters[indx];

                        parameterValues.Add(httpParam.Name, parameter?.ToString() ?? string.Empty);
                    }
                }

                Dictionary<string, string> sendHeaders = new Dictionary<string, string>();
                Dictionary<string, string> sendQuerys = new Dictionary<string, string>();
                HttpContent contents = null;
                if (httpRequest.HttpHeaders != null)
                {
                    //Process HttpHeaders
                    foreach (var header in httpRequest.HttpHeaders)
                    {
                        sendHeaders.Add(ReplaceVariables(header.Name, parameterValues), ReplaceVariables(header.Value, parameterValues));
                    }
                }
                if (httpRequest.Querys != null)
                {
                    //Process Queries
                    foreach (var query in httpRequest.Querys)
                    {
                        sendQuerys.Add(ReplaceVariables(query.Name, parameterValues), ReplaceVariables(query.Value, parameterValues));
                    }
                }
                if (httpRequest.Contents != null)
                {
                    if (httpRequest.Contents.Item is ContentsDictionaryType contentDict)
                    {
                        Dictionary<string, string> sendContents = new Dictionary<string, string>();
                        foreach (var content in contentDict.Content)
                        {
                            sendContents.Add(ReplaceVariables(content.Name, parameterValues), ReplaceVariables(content.Value, parameterValues));
                        }
                        contents = new FormUrlEncodedContent(sendContents);
                    }
                    else if (httpRequest.Contents.Item is ContentsStringType contentStrg)
                    {
                        contents = new StringContent(ReplaceVariables(contentStrg.String, parameterValues), Encoding.UTF8);
                    }
                    else if (httpRequest.Contents.Item is ContentsBinaryType contentBinary)
                    {
                        contents = new ByteArrayContent(contentBinary.BinaryBase64);
                    }
                }

                // Set Headers
                _HttpEndpiontObject.AddDefaultHeaders(sendHeaders);

                // Set Querys
                string queryString = string.Empty;

                foreach (var query in sendQuerys)
                {
                    if (!(query.Value.StartsWith("{") && query.Value.EndsWith("}"))) // Variable not replaced so ignore it.
                    {
                        if (queryString.StartsWith("?"))
                            queryString += $"&{query.Key}={query.Value}";
                        else
                            queryString += $"?{query.Key}={query.Value}";
                    }
                }
                // endpoint string
                string endpoint = httpRequest.Endpoint + queryString;


                if (httpRequest.HttpMethod == HttpRequestEnumType.GET)
                {
                    _HttpEndpiontObject.GetResponseAsync(endpoint, onHttpMessage, onLog).Wait();
                }
                else if (httpRequest.HttpMethod == HttpRequestEnumType.POST)
                {

                    _HttpEndpiontObject.PostRequestAsync(endpoint, contents, onHttpMessage, onLog).Wait();
                }
                else
                {
                    throw new NotSupportedException($"Unsupported HTTP method: {httpRequest.HttpMethod}");
                }
            }
            else if (Item is WebSocketType websocket)
            {
                if (_WebsocketEndpiontObject == null)
                    throw new NotSupportedException($"Endpoint is not initialized.");

                await _WebsocketEndpiontObject.ConnectAsync();   
            }
        }

        private static string ReplaceVariables(string value, Dictionary<string, string> variables)
        {
            if (variables == null || variables.Count == 0)
                return value;

            foreach (var variable in variables)
                value = value.Replace($"{{{variable.Key}}}", variable.Value);

            return value;
        }

        private static Type TypeSelector(ParameterType parameter)
        {
            if (parameter.ValueType.ToLower() == "string")
                return typeof(string);
            else if (parameter.ValueType.ToLower() == "int")
                return typeof(int);
            else if (parameter.ValueType.ToLower() == "integer")
                return typeof(int);
            else if (parameter.ValueType.ToLower() == "single")
                return typeof(float);
            else if (parameter.ValueType.ToLower() == "bool")
                return typeof(bool);
            else if (parameter.ValueType.ToLower() == "boolean")
                return typeof(bool);
            else if (parameter.ValueType.ToLower() == "double")
                return typeof(double);
            else if (parameter.ValueType.ToLower() == "char")
                return typeof(char);
            else
                throw new NotSupportedException($"Unsupported parameter type: {parameter.ValueType}");
        }
    }
}
