using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Mewdeko.Services;

/// <summary>
///     Serializes, redacts, and diffs dashboard audit state snapshots.
/// </summary>
public static class AuditChangeSerializer
{
    private static readonly string[] SensitiveFragments =
        ["token", "secret", "password", "apikey", "api_key", "authorization", "clientsecret"];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>
    ///     Takes an eager, redacted JSON snapshot of an object.
    /// </summary>
    /// <param name="value">The value to snapshot.</param>
    /// <returns>A redacted JSON snapshot, or null when the value is null.</returns>
    public static JsonNode? Snapshot(object? value)
    {
        if (value is null)
            return null;
        try
        {
            var node = JsonSerializer.SerializeToNode(value, SerializerOptions);
            Redact(node);
            return node;
        }
        catch
        {
            return JsonValue.Create(value.ToString());
        }
    }

    /// <summary>
    ///     Builds a before/after diff document from two snapshots.
    /// </summary>
    /// <param name="before">The original snapshot.</param>
    /// <param name="after">The updated snapshot.</param>
    /// <returns>A JSON diff document, or null when nothing changed.</returns>
    public static string? BuildDiff(JsonNode? before, JsonNode? after)
    {
        if (before is null && after is null)
            return null;

        if (before is JsonObject beforeObj && after is JsonObject afterObj)
        {
            var changedBefore = new JsonObject();
            var changedAfter = new JsonObject();
            var changed = new JsonArray();

            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in beforeObj)
                keys.Add(kv.Key);
            foreach (var kv in afterObj)
                keys.Add(kv.Key);

            foreach (var key in keys)
            {
                beforeObj.TryGetPropertyValue(key, out var bVal);
                afterObj.TryGetPropertyValue(key, out var aVal);
                if (NodeEquals(bVal, aVal))
                    continue;

                changed.Add(key);
                changedBefore[key] = bVal?.DeepClone();
                changedAfter[key] = aVal?.DeepClone();
            }

            if (changed.Count == 0)
                return null;

            return new JsonObject
            {
                ["before"] = changedBefore,
                ["after"] = changedAfter,
                ["changed"] = changed
            }.ToJsonString();
        }

        return new JsonObject
        {
            ["before"] = before?.DeepClone(),
            ["after"] = after?.DeepClone()
        }.ToJsonString();
    }

    /// <summary>
    ///     Builds a fallback document holding just the redacted request body.
    /// </summary>
    /// <param name="body">The request body object.</param>
    /// <returns>A JSON change document, or null when there is no body.</returns>
    public static string? BuildRequestBody(object? body)
    {
        var node = Snapshot(body);
        if (node is null)
            return null;
        return new JsonObject { ["after"] = node }.ToJsonString();
    }

    private static void Redact(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(kv => kv.Key).ToList())
                {
                    if (IsSensitive(key))
                        obj[key] = "[redacted]";
                    else
                        Redact(obj[key]);
                }

                break;
            case JsonArray arr:
                foreach (var item in arr)
                    Redact(item);
                break;
        }
    }

    private static bool IsSensitive(string key)
    {
        foreach (var fragment in SensitiveFragments)
        {
            if (key.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool NodeEquals(JsonNode? a, JsonNode? b)
    {
        if (a is null && b is null)
            return true;
        if (a is null || b is null)
            return false;
        return a.ToJsonString() == b.ToJsonString();
    }
}
