using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmithForge.AlertsEngine.Providers.DonatePay.Models
{
    public class DonatePayMessage
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("date")]
        public long Date { get; set; }

        /// <summary>
        /// Может прийти как объект или как JSON-строка.
        /// Кастомный конвертер обрабатывает оба случая.
        /// </summary>
        [JsonPropertyName("vars")]
        [JsonConverter(typeof(DonatePayVarsConverter))]
        public DonatePayVars? Vars { get; set; }
    }

    public class DonatePayVars
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("sum")]
        public decimal Sum { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("is_commission_covered")]
        public int? IsCommissionCovered { get; set; }
    }

    /// <summary>
    /// Конвертер, который умеет читать vars и как объект, и как строку с JSON.
    /// </summary>
    public class DonatePayVarsConverter : JsonConverter<DonatePayVars>
    {
        public override DonatePayVars? Read(ref Utf8JsonReader reader,
            System.Type typeToConvert, JsonSerializerOptions options)
        {
            // Случай 1: vars пришёл как строка с JSON внутри
            if (reader.TokenType == JsonTokenType.String)
            {
                string? json = reader.GetString();
                if (string.IsNullOrEmpty(json))
                    return null;

                return JsonSerializer.Deserialize<DonatePayVars>(json, options);
            }

            // Случай 2: vars пришёл как объект
            if (reader.TokenType == JsonTokenType.StartObject)
            {
                return JsonSerializer.Deserialize<DonatePayVars>(ref reader, options);
            }

            // Случай 3: null
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            return null;
        }

        public override void Write(Utf8JsonWriter writer,
            DonatePayVars value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value, options);
        }
    }
}