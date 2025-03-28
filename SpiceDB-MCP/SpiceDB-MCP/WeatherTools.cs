using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Authzed.Api.V1;
using Grpc.Core;

namespace SpiceDB_MCP;

[McpServerToolType]
public static class WeatherTools
{
    [McpServerTool, Description("Get weather alerts for a US state.")]
    public static async Task<string> GetAlerts(
        HttpClient client,
        [Description("The US state to get alerts for.")] string state)
    {
        try
        {
            var url = $"/alerts/active/area/{state}";
            var response = await client.GetAsync(url);
            var content = await response.Content.ReadAsStringAsync();
            
            if (!response.IsSuccessStatusCode)
            {
                return $"Error retrieving alerts: {(int)response.StatusCode} {response.StatusCode}";
            }
            
            var jsonElement = JsonDocument.Parse(content).RootElement;
            var alerts = jsonElement.GetProperty("features").EnumerateArray();

            if (!alerts.Any())
            {
                return "No active alerts for this state.";
            }

            return string.Join("\n--\n", alerts.Select(alert =>
            {
                JsonElement properties = alert.GetProperty("properties");
                var instruction = "";
                if (properties.TryGetProperty("instruction", out var instructionElement) && 
                    instructionElement.ValueKind != JsonValueKind.Null)
                {
                    instruction = instructionElement.GetString() ?? "";
                }
                
                return $"""
                        Event: {properties.GetProperty("event").GetString()}
                        Area: {properties.GetProperty("areaDesc").GetString()}
                        Severity: {properties.GetProperty("severity").GetString()}
                        Description: {properties.GetProperty("description").GetString()}
                        Instruction: {instruction}
                        """;
            }));
        }
        catch (Exception ex)
        {
            return $"Error retrieving alerts: {ex.Message}";
        }
    }

    [McpServerTool,
     Description(@"
          Get weather forecast for a location.
          Args:
          Latitude: Latitude of the location.
          Longitude: Longitude of the location.
    ")]
    public static async Task<string> GetForecast(
        HttpClient client,
        [Description("Latitude of the location.")] double latitude,
        [Description("Longitude of the location.")] double longitude)
    {
        var jsonElement = await client.GetFromJsonAsync<JsonElement>($"/points/{latitude.ToString(CultureInfo.InvariantCulture)},{longitude.ToString(CultureInfo.InvariantCulture)}");
        var forecastUrl = jsonElement.GetProperty("properties").GetProperty("forecast").GetString();

        jsonElement = await client.GetFromJsonAsync<JsonElement>(forecastUrl);
        var periods = jsonElement.GetProperty("properties").GetProperty("periods").EnumerateArray();

        return string.Join("\n---\n", periods.Select(period => $"""
                                                                {period.GetProperty("name").GetString()}
                                                                Temperature: {period.GetProperty("temperature").GetInt32()}°F
                                                                Wind: {period.GetProperty("windSpeed").GetString()} {period.GetProperty("windDirection").GetString()}
                                                                Forecast: {period.GetProperty("detailedForecast").GetString()}
                                                                """));
    }

    [McpServerTool,
     Description("Get the SpiceDB schema in use.")]
    public static string GetSchema(
        SchemaService.SchemaServiceClient spiceDbClient)
    {
        try
        {
            ReadSchemaRequest request = new ReadSchemaRequest();
            var response = spiceDbClient.ReadSchema(request);
            var schema = response.SchemaText;
            return schema;
        }
        catch (RpcException ex)
        {
            return $"Error looking up Schema: {ex.Status.Detail}";
        }
        catch (Exception ex)
        {
            return $"Error looking up Schema: {ex.Message}";
        }
    }

    [McpServerTool, 
     Description("Look up subjects with permission on a resource in SpiceDB")]
    public static async Task<string> LookupSubjects(
        PermissionsService.PermissionsServiceClient spiceDbClient,
        [Description("The resource type")] string resourceType,
        [Description("The resource ID")] string resourceId,
        [Description("The permission to check")] string permission,
        [Description("The subjectobejcttype to check")] string subjectoOjectType)
    {
        try
        {
            var request = new LookupSubjectsRequest
            {
                Resource = new ObjectReference
                {
                    ObjectType = resourceType,
                    ObjectId = resourceId
                },
                Permission = permission,
                SubjectObjectType = subjectoOjectType,
                Consistency = new Consistency
                {
                    FullyConsistent = true
                }
            };

            var response = spiceDbClient.LookupSubjects(request);
            var subjects = new List<string>();
            
            await foreach (var result in response.ResponseStream.ReadAllAsync())
            {
                var subject = result.Subject.SubjectObjectId;
                subjects.Add($"{subject}");
            }

            if (subjects.Count == 0)
            {
                return $"No subjects found with {permission} permission on {resourceType}:{resourceId}.";
            }

            return $"Subjects with '{permission}' permission on {resourceType}:{resourceId}:\n" + 
                   string.Join("\n", subjects);
        }
        catch (RpcException ex)
        {
            return $"Error looking up subjects: {ex.Status.Detail}";
        }
        catch (Exception ex)
        {
            return $"Error looking up subjects: {ex.Message}";
        }
    }
}