using System.Text.Json;
using System.Text.Json.Serialization;
using Leiter.Algorithms.Stylization.Watercolor;

namespace LeiterWatercolorStudio.Core.Presets.Converters;

// HandTremorOptions has ctor parameter `Rgb8? PaperColor` while its property type is `Rgb8`,
// which prevents System.Text.Json's default parameterized-constructor binder from matching it.
internal sealed class HandTremorOptionsJsonConverter : JsonConverter<HandTremorOptions>
{
    public override HandTremorOptions? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        HandTremorOptionsDto? dto = JsonSerializer.Deserialize<HandTremorOptionsDto>(ref reader, options);
        if (dto is null)
        {
            return null;
        }

        return new HandTremorOptions(
            Amplitude: dto.Amplitude,
            SalientAmplitudeScale: dto.SalientAmplitudeScale,
            SimilarHueThresholdDegrees: dto.SimilarHueThresholdDegrees,
            OverlapDarkening: dto.OverlapDarkening,
            PaperColor: dto.PaperColor,
            PrimaryNoise: dto.PrimaryNoise,
            SecondaryNoise: dto.SecondaryNoise,
            MinContrastingChroma: dto.MinContrastingChroma,
            PaperGapBias: dto.PaperGapBias,
            PaperGapOpacity: dto.PaperGapOpacity);
    }

    public override void Write(
        Utf8JsonWriter writer,
        HandTremorOptions value,
        JsonSerializerOptions options)
    {
        var dto = new HandTremorOptionsDto
        {
            Amplitude = value.Amplitude,
            SalientAmplitudeScale = value.SalientAmplitudeScale,
            SimilarHueThresholdDegrees = value.SimilarHueThresholdDegrees,
            OverlapDarkening = value.OverlapDarkening,
            PaperColor = value.PaperColor,
            PrimaryNoise = value.PrimaryNoise,
            SecondaryNoise = value.SecondaryNoise,
            MinContrastingChroma = value.MinContrastingChroma,
            PaperGapBias = value.PaperGapBias,
            PaperGapOpacity = value.PaperGapOpacity,
        };

        JsonSerializer.Serialize(writer, dto, options);
    }
}
