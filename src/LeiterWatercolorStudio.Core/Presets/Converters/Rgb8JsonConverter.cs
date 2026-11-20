using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Leiter.Pixels;

namespace LeiterWatercolorStudio.Core.Presets.Converters;

internal sealed class Rgb8JsonConverter : JsonConverter<Rgb8>
{
    public override Rgb8 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return ParseHex(reader.GetString());
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            return ReadRgbObject(ref reader);
        }

        throw new JsonException(
            $"Expected '#RRGGBB' string or {{r, g, b}} object for {nameof(Rgb8)}, got {reader.TokenType}.");
    }

    public override void Write(Utf8JsonWriter writer, Rgb8 value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"#{value.R:X2}{value.G:X2}{value.B:X2}");
    }

    private static Rgb8 ParseHex(string? hex)
    {
        if (hex is null || hex.Length != 7 || hex[0] != '#')
        {
            throw new JsonException($"Invalid hex color '{hex}'. Expected '#RRGGBB'.");
        }

        ReadOnlySpan<char> span = hex.AsSpan();
        if (!byte.TryParse(span.Slice(1, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte r) ||
            !byte.TryParse(span.Slice(3, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte g) ||
            !byte.TryParse(span.Slice(5, 2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out byte b))
        {
            throw new JsonException($"Invalid hex color '{hex}'. Expected '#RRGGBB'.");
        }

        return new Rgb8(r, g, b);
    }

    private static Rgb8 ReadRgbObject(ref Utf8JsonReader reader)
    {
        byte r = 0;
        byte g = 0;
        byte b = 0;
        bool hasR = false;
        bool hasG = false;
        bool hasB = false;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (!hasR || !hasG || !hasB)
                {
                    throw new JsonException("RGB object must specify 'r', 'g', and 'b' byte channels.");
                }

                return new Rgb8(r, g, b);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException($"Unexpected token {reader.TokenType} inside {nameof(Rgb8)} object.");
            }

            string? propertyName = reader.GetString();
            if (!reader.Read())
            {
                throw new JsonException($"Unexpected end of JSON reading {nameof(Rgb8)} property '{propertyName}'.");
            }

            if (string.Equals(propertyName, "r", StringComparison.OrdinalIgnoreCase))
            {
                r = ReadByteChannel(ref reader, "r");
                hasR = true;
            }
            else if (string.Equals(propertyName, "g", StringComparison.OrdinalIgnoreCase))
            {
                g = ReadByteChannel(ref reader, "g");
                hasG = true;
            }
            else if (string.Equals(propertyName, "b", StringComparison.OrdinalIgnoreCase))
            {
                b = ReadByteChannel(ref reader, "b");
                hasB = true;
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException($"Unterminated {nameof(Rgb8)} JSON object.");
    }

    private static byte ReadByteChannel(ref Utf8JsonReader reader, string channelName)
    {
        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetByte(out byte value))
        {
            throw new JsonException($"RGB channel '{channelName}' must be an integer byte in [0, 255].");
        }

        return value;
    }
}
