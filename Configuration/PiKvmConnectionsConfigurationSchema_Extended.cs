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

namespace PiKvmLibrary.Configuration
{
    public partial class ConnectionType
    {
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
                Dictionary<string, string> parameterValues = new Dictionary<string, string>(); ;

                if (httpRequest.Parameters != null)
                {
                    parameterRequestType = httpRequest.Parameters.ToDictionary(kvp => kvp.Name, kvp => TypeSelector(kvp));


                    // Validation parameters count are the same
                    if (httpRequest.Parameters.Length != parameters.Length)
                        throw new ArgumentException($"Expected {httpRequest.Parameters.Length} parameters, but received {parameters.Length}.");

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
                                if (expectedType == parameter.GetType())
                                    parameter = parameter; // already correct type
                                else if (expectedType == typeof(int) && parameter is string strInt && int.TryParse(strInt, out int intValue))
                                    parameter = intValue;
                                else if (expectedType == typeof(float) && parameter is string strFloat && float.TryParse(strFloat, out float floatValue))
                                    parameter = floatValue;
                                else if (expectedType == typeof(double) && parameter is string strDouble && double.TryParse(strDouble, out double doubleValue))
                                    parameter = doubleValue;
                                else if (expectedType == typeof(bool) && parameter is string strBool && bool.TryParse(strBool, out bool boolValue))
                                    parameter = boolValue;
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
                Dictionary<string, string> sendContents = new Dictionary<string, string>();
                if (httpRequest.HttpHeaders != null)
                {
                    //Process HttpHeaders
                    foreach (var header in httpRequest.HttpHeaders)
                    {
                        sendHeaders.Add(Replaceariables(header.Name, parameterValues), Replaceariables(header.Value, parameterValues));
                    }
                }
                if (httpRequest.Querys != null)
                {
                    //Process Queries
                    foreach (var query in httpRequest.Querys)
                    {
                        sendQuerys.Add(Replaceariables(query.Name, parameterValues), Replaceariables(query.Value, parameterValues));
                    }
                }
                if (httpRequest.Contents != null)
                {
                    //Process Contents
                    foreach (var content in httpRequest.Contents)
                    {
                        sendContents.Add(Replaceariables(content.Name, parameterValues), Replaceariables(content.Value, parameterValues));
                    }
                }

                // Set Headers
                _HttpEndpiontObject.AddDefaultHeaders(sendHeaders);

                // Set Querys
                string queryString = string.Empty;

                foreach (var query in sendQuerys)
                {
                    if (queryString.Contains("?"))
                        queryString += $"&{query.Key}={query.Value}";
                    else
                        queryString += $"?{query.Key}={query.Value}";
                }
                // endpoint string
                string endpoint = httpRequest.Endpoint + queryString;

                // set Contents
                FormUrlEncodedContent contents = null;
                if (sendContents.Count > 0)
                    contents = new FormUrlEncodedContent(sendContents);


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

        private static string Replaceariables(string value, Dictionary<string, string> variables)
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
            else if (parameter.ValueType.ToLower() == "double")
                return typeof(double);
            else
                throw new NotSupportedException($"Unsupported parameter type: {parameter.ValueType}");
        }
    }
}
