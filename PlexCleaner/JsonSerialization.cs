using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace PlexCleaner;

internal static class JsonSerialization
{
    private static readonly ConditionalWeakTable<
        JsonSerializerContext,
        JsonSerializerOptions
    > s_writeOptions = [];

    public static string SerializeIgnoreEmptyStrings<T>(T value, JsonSerializerContext context)
    {
        JsonSerializerOptions options = s_writeOptions.GetValue(context, CreateWriteOptions);
        JsonTypeInfo<T> typeInfo =
            options.GetTypeInfo(typeof(T)) as JsonTypeInfo<T>
            ?? throw new InvalidOperationException(
                $"Json type info not found for {typeof(T).FullName}"
            );
        return JsonSerializer.Serialize(value, typeInfo);
    }

    private static JsonSerializerOptions CreateWriteOptions(JsonSerializerContext context)
    {
        JsonSerializerOptions options = new(context.Options)
        {
            TypeInfoResolver = new IgnoreEmptyStringTypeInfoResolver(context),
        };
        return options;
    }

    private sealed class IgnoreEmptyStringTypeInfoResolver(IJsonTypeInfoResolver inner)
        : IJsonTypeInfoResolver
    {
        public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
        {
            JsonTypeInfo? typeInfo = inner.GetTypeInfo(type, options);
            if (typeInfo?.Kind != JsonTypeInfoKind.Object)
            {
                return typeInfo;
            }

            foreach (JsonPropertyInfo property in typeInfo.Properties)
            {
                if (property.PropertyType != typeof(string))
                {
                    continue;
                }

                if (property.IsRequired)
                {
                    continue;
                }

                Func<object, object?, bool>? existing = property.ShouldSerialize;
                property.ShouldSerialize = (obj, value) =>
                    !string.IsNullOrEmpty(value as string)
                    && (existing?.Invoke(obj, value) ?? true);
            }

            return typeInfo;
        }
    }
}
