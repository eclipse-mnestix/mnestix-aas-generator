using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MnestixApi.HealthChecks;

/// <summary>
/// Provides standard JSON response formatting for health check results.
/// Follows the Microsoft-recommended format for health check responses.
/// </summary>
public static class HealthCheckResponseWriter
{
    /// <summary>
    /// Writes a health check response as JSON to the HTTP response using the standard format.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="healthReport">The health report containing check results.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    public static Task WriteResponse(HttpContext context, HealthReport healthReport)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var options = new JsonWriterOptions { Indented = true };

        using var memoryStream = new MemoryStream();
        using (var jsonWriter = new Utf8JsonWriter(memoryStream, options))
        {
            jsonWriter.WriteStartObject();
            
            // Write overall status (Healthy, Degraded, or Unhealthy)
            jsonWriter.WriteString("status", healthReport.Status.ToString());
            
            // Write total duration of all health checks
            jsonWriter.WriteString("totalDuration", healthReport.TotalDuration.ToString());

            // Write individual health check entries
            jsonWriter.WriteStartObject("entries");

            foreach (var entry in healthReport.Entries)
            {
                jsonWriter.WriteStartObject(entry.Key);
                
                // Write status for this specific check
                jsonWriter.WriteString("status", entry.Value.Status.ToString());
                
                // Write description if available
                if (!string.IsNullOrEmpty(entry.Value.Description))
                {
                    jsonWriter.WriteString("description", entry.Value.Description);
                }
                
                // Write duration for this specific check
                jsonWriter.WriteString("duration", entry.Value.Duration.ToString());

                // Write custom data (version info, etc.)
                if (entry.Value.Data.Count > 0)
                {
                    jsonWriter.WriteStartObject("data");

                    foreach (var item in entry.Value.Data)
                    {
                        jsonWriter.WritePropertyName(item.Key);
                        JsonSerializer.Serialize(jsonWriter, item.Value, item.Value?.GetType() ?? typeof(object));
                    }

                    jsonWriter.WriteEndObject();
                }

                // Write exception details if the check failed
                if (entry.Value.Exception != null)
                {
                    jsonWriter.WriteString("exception", entry.Value.Exception.Message);
                    jsonWriter.WriteString("exceptionType", entry.Value.Exception.GetType().Name);
                }

                jsonWriter.WriteEndObject();
            }

            jsonWriter.WriteEndObject(); // Close entries

            jsonWriter.WriteEndObject(); // Close root object
        }

        return context.Response.WriteAsync(
            Encoding.UTF8.GetString(memoryStream.ToArray()));
    }
}
