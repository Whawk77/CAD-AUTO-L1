using System.Text.Json;
using System.Text.Json.Serialization;

namespace CatiaAiPanel.Core;

public static class CodexOutputSchema
{
    public const string Json = """
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "assistantMessage": { "type": "string" },
        "needsClarification": { "type": "boolean" },
        "macro": {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "language": { "type": "string", "enum": ["CATScript", "VBScript"] },
            "entryPoint": { "type": "string", "const": "AIEntry" },
            "code": { "type": "string" }
          },
          "required": ["language", "entryPoint", "code"]
        },
        "operationKind": { "type": "string", "enum": ["read", "write"] },
        "targetDocumentMode": { "type": "string", "enum": ["currentDocument", "newPart"] },
        "reconstructionPlan": {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "readyToModel": { "type": "boolean" },
            "strategy": { "type": "string" },
            "missingMeasurements": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "key": { "type": "string" },
                  "description": { "type": "string" },
                  "quantity": { "type": "string" },
                  "reference": { "type": "string" },
                  "status": { "type": "string", "enum": ["missing", "partial", "present"] },
                  "evidence": { "type": "string" }
                },
                "required": ["key", "description", "quantity", "reference", "status", "evidence"]
              }
            },
            "assumptions": { "type": "array", "items": { "type": "string" } }
          },
          "required": ["readyToModel", "strategy", "missingMeasurements", "assumptions"]
        },
        "expectedEffects": { "type": "array", "items": { "type": "string" } },
        "riskFlags": { "type": "array", "items": { "type": "string" } },
        "verificationHints": { "type": "array", "items": { "type": "string" } }
      },
      "required": ["assistantMessage", "needsClarification", "macro", "operationKind", "targetDocumentMode", "reconstructionPlan", "expectedEffects", "riskFlags", "verificationHints"]
    }
    """;
}

public sealed record ParsedCodexLine(
    string Type,
    string Message,
    string? ThreadId,
    string? AgentMessage,
    bool IsError);

public static class CodexJsonlParser
{
    public static ParsedCodexLine Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return new("empty", "", null, null, false);
        try
        {
            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            var type = ReadString(root, "type") ?? "unknown";
            var threadId = ReadString(root, "thread_id");
            string? agentMessage = null;
            var message = type;
            var isError = type is "error" or "turn.failed";

            if (root.TryGetProperty("item", out var item))
            {
                var itemType = ReadString(item, "type") ?? "item";
                var text = ReadString(item, "text");
                if (itemType == "agent_message") agentMessage = text;
                message = string.IsNullOrWhiteSpace(text) ? $"{type}: {itemType}" : text;
            }
            else if (root.TryGetProperty("error", out var error))
            {
                message = error.ValueKind == JsonValueKind.String
                    ? error.GetString() ?? type
                    : ReadString(error, "message") ?? error.ToString();
            }
            else
            {
                message = ReadString(root, "message") ?? type;
            }

            return new(type, message, threadId, agentMessage, isError);
        }
        catch (JsonException)
        {
            return new("invalid-json", line, null, null, true);
        }
    }

    public static MacroProposal ParseProposal(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };
        return JsonSerializer.Deserialize<MacroProposal>(json, options)
               ?? throw new InvalidDataException("Codex 没有返回宏提案。");
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
