using System;
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

    public object Deserialize(Type type, string serializedValue) =>
        type == typeof(FfMpegIdetInfo)
            ? JsonSerializer.Deserialize<FfMpegIdetInfoSerializer>(serializedValue) ?? new object()
            : throw new ArgumentException(
                $"Invalid type for deserialization: {type.FullName} is not supported by {nameof(FfMpegIdetInfoSerializer)}"
            );
}
