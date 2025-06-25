using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PiKvmLibrary.Configuration
{
    public partial class HidInformationType
    {
        public static json.Result Deserialize (string json)
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
            json.Result deserializedObject = JsonSerializer.Deserialize<json.Result>(json, options);

            return deserializedObject;
        }
    }
}
namespace PiKvmLibrary.Configuration.json
{
    public class Result
    {

        [JsonPropertyName("ok")]
        public bool ok { get; set; }
        [JsonPropertyName("result")]
        public HidInformation result { get; set; }
    }
    public class HidInformation
    {
        [JsonPropertyName("busy")]
        public bool Busy { get; set; }

        [JsonPropertyName("connected")]
        public bool? Connected { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("online")]
        public bool Online { get; set; }

        [JsonPropertyName("jiggler")]
        public Jiggler Jiggler { get; set; }

        [JsonPropertyName("keyboard")]
        public Keyboard Keyboard { get; set; }

        [JsonPropertyName("mouse")]
        public Mouse Mouse { get; set; }
    }

    public class Jiggler
    {
        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        [JsonPropertyName("interval")]
        public int Interval { get; set; }
    }

    public class Keyboard
    {
        [JsonPropertyName("online")]
        public bool Online { get; set; }

        [JsonPropertyName("leds")]
        public Leds Leds { get; set; }

        [JsonPropertyName("outputs")]
        public Outputs Outputs { get; set; }
    }

    public class Mouse
    {
        [JsonPropertyName("online")]
        public bool Online { get; set; }

        [JsonPropertyName("absolute")]
        public bool Absolute { get; set; }

        [JsonPropertyName("outputs")]
        public Outputs Outputs { get; set; }
    }

    public class Leds
    {
        [JsonPropertyName("caps")]
        public bool Caps { get; set; }

        [JsonPropertyName("num")]
        public bool Num { get; set; }

        [JsonPropertyName("scroll")]
        public bool Scroll { get; set; }
    }

    public class Outputs
    {
        [JsonPropertyName("active")]
        public string Active { get; set; }

        [JsonPropertyName("available")]
        public List<string> Available { get; set; }
    }
}