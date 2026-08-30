using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using PlexCleaner;
using Xunit.Sdk;

namespace PlexCleanerTests;

public class FfMpegIdetInfoSerializer : IXunitSerializer
{
    public bool IsSerializable(
        Type type,
        object? value,
        [NotNullWhen(false)] out string? failureReason
    )
    {
        if (type == typeof(FfMpegIdetInfo) && value is FfMpegIdetInfo)
        {
            failureReason = string.Empty;
            return true;
        }

        failureReason =
            $"Type {type.FullName} is not supported by {nameof(FfMpegIdetInfoSerializer)}.";
        return false;
    }

    public string Serialize(object value) =>
        value is FfMpegIdetInfo idetInfo
            ? JsonSerializer.Serialize(idetInfo)
            : throw new InvalidOperationException(
                $"Invalid type for serialization: {value.GetType().FullName} is not supported by {nameof(FfMpegIdetInfoSerializer)}."
            );

    // Deserializes to FfMpegIdetInfo, the type xUnit asked for, not to this serializer.
    // A null result throws rather than degrading to a bare object, since either would be handed to a
    // theory parameter typed FfMpegIdetInfo and fail there instead of here.
    public object Deserialize(Type type, string serializedValue) =>
        type == typeof(FfMpegIdetInfo)
            ? JsonSerializer.Deserialize<FfMpegIdetInfo>(serializedValue)
                ?? throw new InvalidOperationException(
                    $"Deserialization returned null for {type.FullName}: {serializedValue}"
                )
            : throw new ArgumentException(
                $"Invalid type for deserialization: {type.FullName} is not supported by {nameof(FfMpegIdetInfoSerializer)}"
            );
}
