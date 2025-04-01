using ModelContextProtocol.Server;
using System.ComponentModel;
using Authzed.Api.V1;
using Grpc.Core;

namespace SpiceDB_MCP;

[McpServerToolType]
public static class SpiceDbTools
{
    [McpServerTool,
     Description(
         "Get the SpiceDB schema in use. When in doubt, use this first to get an overview over the existing model to make other calls.")]
    public static string GetSchema(
        SchemaService.SchemaServiceClient spiceDbClient)
    {
        try
        {
            var request = new ReadSchemaRequest();
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
     Description(
         "Look up resources with permission on a resource in SpiceDB. Answers questions like 'On what <resourcetype> does <subject:id> have specific <permission>? e.g. On what projects does user:CTO have admin permissions?'")]
    public static async Task<string> LookupResources(
        PermissionsService.PermissionsServiceClient spiceDbClient,
        [Description("The resource object type")]
        string resourceObjectType,
        [Description("The permission to check")]
        string permission,
        [Description("The subject type to check")]
        string subjectType,
        [Description("The subject id to check")]
        string subjectId)
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
                return
                    $"No resources of type {resourceObjectType} found with {permission} permission for {subjectType}:{subjectId}.";
            }

            return
                $"Resources of type '{resourceObjectType}' with '{permission}' permission for {subjectType}:{subjectId}:\n" +
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
     Description(
         "Look up subjects with permission on a resource in SpiceDB. Answers questions like 'Who has permission on resource <x>?' e.g. 'What users can read document a?'")]
    public static async Task<string> LookupSubjects(
        PermissionsService.PermissionsServiceClient spiceDbClient,
        [Description("The resource type")] string resourceType,
        [Description("The resource ID")] string resourceId,
        [Description("The permission to check")]
        string permission,
        [Description("The subjectobjecttype to check")]
        string subjectObjectType)
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
                SubjectObjectType = subjectoObjectType,
                Consistency = new Consistency
                {
                    MinimizeLatency = true
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

    [McpServerTool,
     Description(
         "Read relationships in SpiceDB. All parameters are optional except resourceType. " +
         "IMPORTANT: Each time you wanna use this, you want to use LookupSubjects or LookupResources before " +
         "you want to use this, as it does not give you the computed permissions, only direct relations. " +
         "This is used for questions like 'what users do I have?' or 'What documents are there?' ONLY." +
         "Answer example: 'Relationships for <type>:id>:\n<type>:<id> has <relation> relationship with <type>:<id>, e.g. Relationships for project:bigproject:\nproject:bigproject has administrator relationship with user:CTO")]
    public static async Task<string> ReadRelationships(
        PermissionsService.PermissionsServiceClient spiceDbClient,
        [Description("The resource type (required)")]
        string resourceType,
        [Description("The resource ID (optional)")]
        string? resourceId = null,
        [Description("The relationship name to check (optional)")]
        string? relationshipName = null,
        [Description("The subject type (optional)")]
        string? subjectType = null,
        [Description("The subject ID (optional)")]
        string? subjectId = null,
        [Description("The subject relation (optional)")]
        string? subjectRelation = null)
    {
        try
        {
            var relationshipFilter = new RelationshipFilter
            {
                ResourceType = resourceType
            };

            if (!string.IsNullOrEmpty(resourceId))
                relationshipFilter.OptionalResourceId = resourceId;

            if (!string.IsNullOrEmpty(relationshipName))
                relationshipFilter.OptionalRelation = relationshipName;

            // Handle subject filtering if provided
            if (!string.IsNullOrEmpty(subjectType))
            {
                var subjectFilter = new SubjectFilter
                {
                    SubjectType = subjectType
                };

                if (!string.IsNullOrEmpty(subjectId))
                    subjectFilter.OptionalSubjectId = subjectId;

                if (!string.IsNullOrEmpty(subjectRelation))
                    subjectFilter.OptionalRelation = new SubjectFilter.Types.RelationFilter()
                    {
                        Relation = subjectRelation
                    };

                relationshipFilter.OptionalSubjectFilter = subjectFilter;
            }

            var request = new ReadRelationshipsRequest
            {
                RelationshipFilter = relationshipFilter
            };

            var response = spiceDbClient.ReadRelationships(request);
            var relationships = new List<string>();

            await foreach (var result in response.ResponseStream.ReadAllAsync())
            {
                var resource = $"{result.Relationship.Resource.ObjectType}:{result.Relationship.Resource.ObjectId}";
                var relation = result.Relationship.Relation;

                var subject = result.Relationship.Subject.Object != null
                    ? $"{result.Relationship.Subject.Object.ObjectType}" +
                      $":{result.Relationship.Subject.Object.ObjectId}"
                    : "N/A";

                relationships.Add($"{resource} has {relation} relationship with {subject}");
            }

            if (relationships.Count == 0)
            {
                var queryDescription = BuildQueryDescription(resourceType, resourceId, relationshipName, subjectType,
                    subjectId, subjectRelation);
                return $"No relationships found for {queryDescription}.";
            }

            var filterDescription = BuildQueryDescription(resourceType, resourceId, relationshipName, subjectType,
                subjectId, subjectRelation);
            return $"Relationships for {filterDescription}:\n" +
                   string.Join("\n", relationships);
        }
        catch (RpcException ex)
        {
            return $"Error reading relationships: {ex.Status.Detail}";
        }
        catch (Exception ex)
        {
            return $"Error reading relationships: {ex.Message}";
        }
    }

    //Note: JSON Array was not sent correctly by client which led to errors, so we switched to semicolon-separated. May investigate further.
    [McpServerTool,
     Description(
         "Check multiple permissions at once in SpiceDB. Accepts a semicolon-separated list of permission checks in the format 'resourceType:resourceId:permission:subjectType:subjectId'." +
         "IMPORTANT: Favor this over multiple calls to LookupResources or LookupSubjects, as it only does one request with a bunch of checks.")]
    public static async Task<string> CheckBulkPermissions(
        PermissionsService.PermissionsServiceClient spiceDbClient,
        [Description(
            "Semicolon-separated list of permission checks in format 'resourceType:resourceId:permission:subjectType:subjectId'. Example: 'document:doc1:view:user:john;folder:folder1:read:user:jane'")]
        string permissionChecks)
    {
        try

        {
            // Parse the semicolon-separated list of checks
            var checks = permissionChecks.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(check => check.Trim())
                .Where(check => !string.IsNullOrEmpty(check))
                .ToList();

            if (checks.Count == 0)
                return "Error: No valid permission checks provided";

            var request = new CheckBulkPermissionsRequest
            {
                Consistency = new Consistency { FullyConsistent = true }
            };

            var parsedChecks =
                new List<(string ResourceType, string ResourceId, string Permission, string SubjectType, string
                    SubjectId, string SubjectRelation)>();

            foreach (var check in checks)
            {
                // Split by colon, supporting optional subject relation
                var parts = check.Split(':', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length < 5)
                {
                    return
                        $"Error: Invalid check format: {check}. Expected format: 'resourceType:resourceId:permission:subjectType:subjectId[:subjectRelation]'";
                }

                var resourceType = parts[0].Trim();
                var resourceId = parts[1].Trim();
                var permission = parts[2].Trim();
                var subjectType = parts[3].Trim();
                var subjectId = parts[4].Trim();
                var subjectRelation = parts.Length > 5 ? parts[5].Trim() : null;

                parsedChecks.Add((resourceType, resourceId, permission, subjectType, subjectId,
                    subjectRelation ?? "none"));

                var item = new CheckBulkPermissionsRequestItem
                {
                    Resource = new ObjectReference
                    {
                        ObjectType = resourceType,
                        ObjectId = resourceId
                    },
                    Permission = permission,
                    Subject = new SubjectReference
                    {
                        Object = new ObjectReference
                        {
                            ObjectType = subjectType,
                            ObjectId = subjectId
                        }
                    }
                };

                // Add subject relation if provided
                if (!string.IsNullOrEmpty(subjectRelation) && subjectRelation != "none")
                {
                    item.Subject.OptionalRelation = subjectRelation;
                }

                request.Items.Add(item);
            }

            var response = await spiceDbClient.CheckBulkPermissionsAsync(request);

            var results = new List<string>();

            for (var i = 0; i < response.Pairs.Count; i++)
            {
                var pair = response.Pairs[i];
                var check = parsedChecks[i];

                var resourceRef = $"{check.ResourceType}:{check.ResourceId}";
                var subjectRef = $"{check.SubjectType}:{check.SubjectId}";
                if (!string.IsNullOrEmpty(check.SubjectRelation))
                    subjectRef += $"#{check.SubjectRelation}";

                var hasPermission = pair.Item.Permissionship ==
                                    CheckPermissionResponse.Types.Permissionship.HasPermission;

                results.Add(
                    $"{subjectRef} {(hasPermission ? "HAS" : "DOES NOT HAVE")} permission '{check.Permission}' on {resourceRef}");
            }

            return string.Join("\n", results);
        }
        catch (RpcException ex)
        {
            return $"Error checking bulk permissions: {ex.Status.Detail}";
        }
        catch (Exception ex)
        {
            return $"Error checking bulk permissions: {ex.Message}";
        }
    }

    // Helper method to build a human-readable description of the getrelations query
    private static string BuildQueryDescription(
        string resourceType,
        string? resourceId,
        string? relationshipName,
        string? subjectType,
        string? subjectId,
        string? subjectRelation)
    {
        var description = new List<string>();

        var resourceStr = resourceType;
        if (!string.IsNullOrEmpty(resourceId))
            resourceStr += $":{resourceId}";
        description.Add(resourceStr);

        if (!string.IsNullOrEmpty(relationshipName))
            description.Add($"relation '{relationshipName}'");
        if (string.IsNullOrEmpty(subjectType))
            return string.Join(", ", description);

        var subjectStr = $"subject '{subjectType}";

        if (!string.IsNullOrEmpty(subjectId))
            subjectStr += $":{subjectId}";
        if (!string.IsNullOrEmpty(subjectRelation))
            subjectStr += $"#{subjectRelation}";

        subjectStr += "'";
        description.Add(subjectStr);

        return string.Join(", ", description);
    }
}