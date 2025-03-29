using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Authzed.Api.V1;
using Grpc.Core;

namespace SpiceDB_MCP;

[McpServerToolType]
public static class SpiceDbMcp
{
    [McpServerTool,
     Description("Get the SpiceDB schema in use. Useful to get an overview over existing definitions, relations and permissions and the overall structure")]
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
     Description("Look up resources with permission on a resource in SpiceDB. Answers questions like 'On what <resourcetype> does <subject:id> have specific <permission>? e.g. On what projects does user:CTO have admin permissions?'")]
    public static async Task<string> LookupResources(
        PermissionsService.PermissionsServiceClient spiceDbClient,
        [Description("The resource object type")] string resourceObjectType,
        [Description("The permission to check")] string permission,
        [Description("The subject type to check")] string subjectType,
        [Description("The subject id to check")] string subjectId)
    {
        try
        {
            var request = new LookupResourcesRequest
            {
                Subject = new SubjectReference()
                {
                    Object = new ObjectReference()
                    {
                        ObjectType = subjectType,
                        ObjectId = subjectId
                    }
                },
                Permission = permission,
                ResourceObjectType = resourceObjectType,
                Consistency = new Consistency
                {
                    FullyConsistent = true
                }
            };

            var response = spiceDbClient.LookupResources(request);
            var resources = new List<string>();
            
            await foreach (var result in response.ResponseStream.ReadAllAsync())
            {
                var resource = result.ResourceObjectId;
                resources.Add($"{resource}");
            }

            if (resources.Count == 0)
            {
                return $"No resources of type {resourceObjectType} found with {permission} permission for {subjectType}:{subjectId}.";
            }

            return $"Resources of type '{resourceObjectType}' with '{permission}' permission for {subjectType}:{subjectId}:\n" + 
                   string.Join("\n", resources);
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
    
    [McpServerTool, 
     Description("Look up subjects with permission on a resource in SpiceDB. Answers questions like 'Who has permission on resource <x>?'")]
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