using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PiKvmLibrary.Configuration
{
    public partial class StreamerInformationType
    {
        public static json.Streamer.Root Deserialize(string json)
        {
            // Implement deserialization logic here
            // This is a placeholder for the actual implementation
            if (string.IsNullOrEmpty(json))
            {
                throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));
            }
            // Use a JSON library like Newtonsoft.Json or System.Text.Json to deserialize the JSON string
            // Example using Newtonsoft.Json:
            // var deserializedObject = JsonConvert.DeserializeObject<HidinformationSchema>(json);
            // For now, we will throw an exception to indicate that this method is not implemented.

            System.Text.Json.JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            // Uncomment the line below when you implement the deserialization logic
            json.Streamer.Root deserializedObject = JsonSerializer.Deserialize<json.Streamer.Root>(json, options);

            return deserializedObject;
        }
    }
}
namespace PiKvmLibrary.Configuration.json.Streamer
{
    public class Root
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public Result Result { get; set; }
    }

    public class Result
    {
        [JsonPropertyName("features")]
        public Features Features { get; set; }

        [JsonPropertyName("limits")]
        public Limits Limits { get; set; }

        [JsonPropertyName("params")]
        public Params Params { get; set; }

        [JsonPropertyName("snapshot")]
        public Snapshot Snapshot { get; set; }

        [JsonPropertyName("streamer")]
        public Streamer Streamer { get; set; }
    }

    public class Features
    {
        [JsonPropertyName("h264")]
        public bool H264 { get; set; }

        [JsonPropertyName("quality")]
        public bool Quality { get; set; }

        [JsonPropertyName("resolution")]
        public bool Resolution { get; set; }
    }

    public class Limits
    {
        [JsonPropertyName("desired_fps")]
        public MinMax DesiredFps { get; set; }

        [JsonPropertyName("h264_bitrate")]
        public MinMax H264Bitrate { get; set; }

        [JsonPropertyName("h264_gop")]
        public MinMax H264Gop { get; set; }
    }

    public class MinMax
    {
        [JsonPropertyName("max")]
        public int Max { get; set; }

        [JsonPropertyName("min")]
        public int Min { get; set; }
    }

    public class Params
    {
        [JsonPropertyName("desired_fps")]
        public int DesiredFps { get; set; }

        [JsonPropertyName("h264_bitrate")]
        public int H264Bitrate { get; set; }

        [JsonPropertyName("h264_gop")]
        public int H264Gop { get; set; }

        [JsonPropertyName("quality")]
        public int Quality { get; set; }
    }

    public class Snapshot
    {
        [JsonPropertyName("saved")]
        public object Saved { get; set; }
    }

    public class Streamer
    {
        [JsonPropertyName("drm")]
        public Drm Drm { get; set; }

        [JsonPropertyName("encoder")]
        public Encoder Encoder { get; set; }

        [JsonPropertyName("h264")]
        public H264 H264 { get; set; }

        [JsonPropertyName("instance_id")]
        public string InstanceId { get; set; }

        [JsonPropertyName("sinks")]
        public Sinks Sinks { get; set; }

        [JsonPropertyName("source")]
        public Source Source { get; set; }

        [JsonPropertyName("stream")]
        public Stream Stream { get; set; }
    }

    public class Drm
    {
        [JsonPropertyName("fps")]
        public int Fps { get; set; }

        [JsonPropertyName("live")]
        public bool Live { get; set; }
    }

    public class Encoder
    {
        [JsonPropertyName("quality")]
        public int Quality { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class H264
    {
        [JsonPropertyName("bitrate")]
        public int Bitrate { get; set; }

        [JsonPropertyName("fps")]
        public int Fps { get; set; }

        [JsonPropertyName("gop")]
        public int Gop { get; set; }

        [JsonPropertyName("online")]
        public bool Online { get; set; }
    }

    public class Sinks
    {
        [JsonPropertyName("h264")]
        public HasClients H264 { get; set; }

        [JsonPropertyName("jpeg")]
        public HasClients Jpeg { get; set; }
    }

    public class HasClients
    {
        [JsonPropertyName("has_clients")]
        public bool HasClientsFlag { get; set; }
    }

    public class Source
    {
        [JsonPropertyName("captured_fps")]
        public int CapturedFps { get; set; }

        [JsonPropertyName("desired_fps")]
        public int DesiredFps { get; set; }

        [JsonPropertyName("online")]
        public bool Online { get; set; }

        [JsonPropertyName("resolution")]
        public Resolution Resolution { get; set; }
    }

    public class Resolution
    {
        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("width")]
        public int Width { get; set; }
    }

    public class Stream
    {
        [JsonPropertyName("clients")]
        public int Clients { get; set; }

        [JsonPropertyName("clients_stat")]
        public Dictionary<string, object> ClientsStat { get; set; }

        [JsonPropertyName("queued_fps")]
        public int QueuedFps { get; set; }
    }
}
