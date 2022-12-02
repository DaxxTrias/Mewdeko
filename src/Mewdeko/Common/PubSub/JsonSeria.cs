﻿using System.Text.Json;
using Mewdeko.Common.JsonConverters;

namespace Mewdeko.Common.PubSub;

public class JsonSeria : ISeria
{
    private readonly JsonSerializerOptions serializerOptions = new()
    {
        Converters =
        {
            new Rgba32Converter(), new CultureInfoConverter()
        }
    };

    public byte[] Serialize<T>(T data)
        => JsonSerializer.SerializeToUtf8Bytes(data, serializerOptions);

    public T? Deserialize<T>(byte[]? data)
    {
        if (data is null)
            return default;

        return JsonSerializer.Deserialize<T>(data, serializerOptions);
    }
}