using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using System.Net.Http.Headers;
using Authzed.Api.V1;
using Grpc.Core;
using Grpc.Net.Client;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

builder.Services.AddSingleton<SchemaService.SchemaServiceClient>(_ =>
{
    var callCredentials = CallCredentials.FromInterceptor((_, metadata) =>
    {
        var testToken = Environment.GetEnvironmentVariable("SPICEDB_PSK") 
                        ?? throw new Exception("No SpiceDB token provided.");
        metadata.Add("Authorization", $"Bearer {testToken}");
        return Task.CompletedTask;
    });
    
    var grpcCredentials = new CompositeCredentials(ChannelCredentials.Insecure, callCredentials);
    var channel = GrpcChannel.ForAddress("http://localhost:50051", new GrpcChannelOptions
    {
        Credentials = grpcCredentials,
        UnsafeUseInsecureChannelCallCredentials = true
    });
    
    return new SchemaService.SchemaServiceClient(channel);
});

// SpiceDB client using gRPC
builder.Services.AddSingleton<PermissionsService.PermissionsServiceClient>(serviceProvider => 
{
    var callCredentials = CallCredentials.FromInterceptor((context, metadata) =>
    {
        var testtoken = "testkey";
        metadata.Add("Authorization", $"Bearer {testtoken}");
        return Task.CompletedTask;
    });
    
    var grpcCredentials = new CompositeCredentials(ChannelCredentials.Insecure, callCredentials);
    var channel = GrpcChannel.ForAddress("http://localhost:50051", new GrpcChannelOptions
    {
        Credentials = grpcCredentials,
        UnsafeUseInsecureChannelCallCredentials = true
    });
    
    return new PermissionsService.PermissionsServiceClient(channel);
});

var app = builder.Build();

await app.RunAsync();


internal class CompositeCredentials : ChannelCredentials
{
    private readonly ChannelCredentials channelCredentials;
    private readonly CallCredentials callCredentials;    

    public CompositeCredentials(ChannelCredentials channelCredentials, CallCredentials callCredentials)
    {
        this.channelCredentials = channelCredentials ?? throw new ArgumentNullException(nameof(channelCredentials));
        this.callCredentials = callCredentials ?? throw new ArgumentNullException(nameof(callCredentials));       
    }

    public override void InternalPopulateConfiguration(ChannelCredentialsConfiguratorBase configurator, object state)
    {
        configurator.SetCompositeCredentials(state, channelCredentials, callCredentials);        
    }
}