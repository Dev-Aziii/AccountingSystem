using System.Text.Json;

namespace AccountingSystem.Client.Services
{
    internal static class ApiErrorParser
    {
        internal static string Extract(string? rawContent, string fallbackMessage)
        {
            if (string.IsNullOrWhiteSpace(rawContent))
            {
                return fallbackMessage;
            }

            var trimmed = rawContent.Trim();

            try
            {
                using var document = JsonDocument.Parse(trimmed);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (TryReadString(root, "error", out var errorMessage))
                    {
                        return errorMessage;
                    }

                    if (TryReadString(root, "message", out var message))
                    {
                        return message;
                    }

                    if (TryReadFirstValidationError(root, out var validationMessage))
                    {
                        return validationMessage;
                    }

                    if (TryReadString(root, "title", out var title))
                    {
                        return title;
                    }
                }
                else if (root.ValueKind == JsonValueKind.String)
                {
                    var message = root.GetString();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        return message;
                    }
                }
            }
            catch (JsonException)
            {
                // Fall back to the raw response content when the payload is not JSON.
            }

            return trimmed;
        }

        private static bool TryReadString(JsonElement root, string propertyName, out string message)
        {
            message = string.Empty;

            if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var value = property.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            message = value;
            return true;
        }

        private static bool TryReadFirstValidationError(JsonElement root, out string message)
        {
            message = string.Empty;

            if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in errors.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var error in property.Value.EnumerateArray())
                {
                    var value = error.GetString();
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        continue;
                    }

                    message = value;
                    return true;
                }
            }

            return false;
        }
    }
}
