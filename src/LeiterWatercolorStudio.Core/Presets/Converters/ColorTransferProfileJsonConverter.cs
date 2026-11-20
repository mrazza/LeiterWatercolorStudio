using System.Text.Json;
using System.Text.Json.Serialization;
using Leiter.Algorithms.Color;
using Leiter.Pixels.ColorSpaces;

namespace LeiterWatercolorStudio.Core.Presets.Converters;

internal sealed class ColorTransferProfileJsonConverter : JsonConverter<ColorTransferProfile>
{
    public override ColorTransferProfile Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException($"Expected JSON object for {nameof(ColorTransferProfile)}.");
        }

        string? name = null;
        LabColorStatistics? statistics = null;
        Illuminant? illuminant = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (statistics is null)
                {
                    throw new JsonException($"{nameof(ColorTransferProfile)} requires a 'statistics' object.");
                }

                var profile = new ColorTransferProfile(
                    name!,
                    statistics.Value,
                    illuminant ?? Illuminant.E);
                profile.Validate();
                return profile;
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Unexpected token {reader.TokenType} in {nameof(ColorTransferProfile)}.");
            }

            string? propertyName = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException($"Unexpected end of JSON reading {nameof(ColorTransferProfile)}.");
            }

            if (string.Equals(propertyName, nameof(ColorTransferProfile.Name), StringComparison.OrdinalIgnoreCase))
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    name = null;
                }
                else if (reader.TokenType == JsonTokenType.String)
                {
                    name = reader.GetString();
                }
                else
                {
                    throw new JsonException("ColorTransferProfile 'name' must be a string.");
                }
            }
            else if (string.Equals(propertyName, nameof(ColorTransferProfile.Statistics), StringComparison.OrdinalIgnoreCase))
            {
                statistics = JsonSerializer.Deserialize<LabColorStatistics?>(ref reader, options);
            }
            else if (string.Equals(propertyName, nameof(ColorTransferProfile.Illuminant), StringComparison.OrdinalIgnoreCase))
            {
                illuminant = JsonSerializer.Deserialize<Illuminant?>(ref reader, options);
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException($"Unterminated {nameof(ColorTransferProfile)} JSON object.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        ColorTransferProfile value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString(ConvertPropertyName(options, nameof(ColorTransferProfile.Name)), value.Name);
        writer.WritePropertyName(ConvertPropertyName(options, nameof(ColorTransferProfile.Statistics)));
        JsonSerializer.Serialize(writer, value.Statistics, options);
        writer.WritePropertyName(ConvertPropertyName(options, nameof(ColorTransferProfile.Illuminant)));
        JsonSerializer.Serialize(writer, value.Illuminant, options);
        writer.WriteEndObject();
    }

    private static string ConvertPropertyName(JsonSerializerOptions options, string name) =>
        options.PropertyNamingPolicy?.ConvertName(name) ?? name;
}
