using System.Linq;
using System.Text.Json;
using AskyBackend.Tests.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace AskyBackend.Tests.Integration;

public sealed class SignalRHubTests : IClassFixture<BackendWebApplicationFactory>
{
    private readonly BackendWebApplicationFactory _factory;

    public SignalRHubTests(BackendWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GenerateQuestions_ReturnsFallbackWhenLlmUnavailable()
    {
        await using var connection = await _factory.CreateHubConnectionAsync();
        var tcs = new TaskCompletionSource<IReadOnlyList<string>>();

        connection.On<JsonElement>("Questions", element =>
        {
            if (element.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                var items = data.EnumerateArray()
                    .Select(item => item.TryGetProperty("text", out var text) ? text.GetString() ?? string.Empty : string.Empty)
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToArray();
                tcs.TrySetResult(items);
            }
        });

        await connection.InvokeAsync("GenerateQuestions", new { topic = "launch plan" });

        var questions = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.NotEmpty(questions);
        Assert.All(questions, q => Assert.False(string.IsNullOrWhiteSpace(q)));
    }

    [Fact]
    public async Task SendAudioFrame_RejectsPayloadAboveLimit()
    {
        await using var connection = await _factory.CreateHubConnectionAsync();
        var oversizedPayload = new byte[9000];

        await Assert.ThrowsAsync<HubException>(() =>
            connection.InvokeAsync("SendAudioFrame", new
            {
                sequence = 1,
                timestamp = 0.0,
                payload = oversizedPayload
            }));
    }
}
