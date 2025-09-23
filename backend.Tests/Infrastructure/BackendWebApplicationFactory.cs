using AskyBackend.Options;
using AskyBackend.Speech;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AskyBackend.Tests.Infrastructure;

public sealed class BackendWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<AzureSpeechOptions>(opts =>
            {
                opts.SubscriptionKey = string.Empty;
                opts.Region = string.Empty;
            });

            services.PostConfigure<LlmServiceOptions>(opts =>
            {
                opts.BaseUrl = string.Empty;
            });

            services.AddSingleton<ISpeechSessionFactory>(sp => sp.GetRequiredService<NoopSpeechSessionFactory>());
        });

        return base.CreateHost(builder);
    }

    public async Task<HubConnection> CreateHubConnectionAsync(CancellationToken cancellationToken = default)
    {
        var client = Server.CreateHandler();
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(new Uri(Server.BaseAddress, "/hubs/asr"), options =>
            {
                options.HttpMessageHandlerFactory = _ => client;
            })
            .WithAutomaticReconnect()
            .Build();

        await hubConnection.StartAsync(cancellationToken);
        return hubConnection;
    }
}
