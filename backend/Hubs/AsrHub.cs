using AskyBackend.Contracts;
using AskyBackend.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AskyBackend.Hubs;

public sealed class AsrHub : Hub
{
    private readonly IConversationSessionManager _sessionManager;
    private readonly ILogger<AsrHub> _logger;

    public AsrHub(IConversationSessionManager sessionManager, ILogger<AsrHub> logger)
    {
        _sessionManager = sessionManager;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        await _sessionManager.RegisterConnectionAsync(Context.ConnectionId, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("Session", new { state = "started" }, Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Connection {ConnectionId} disconnected. Reason: {Reason}", Context.ConnectionId, exception?.Message);
        await _sessionManager.CleanupConnectionAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public Task SendAudioFrame(AudioFrameDto frame)
    {
        return _sessionManager.HandleAudioFrameAsync(Context.ConnectionId, frame, Context.ConnectionAborted);
    }

    public Task StopStream()
    {
        return _sessionManager.StopStreamAsync(Context.ConnectionId, Context.ConnectionAborted);
    }

    public async Task GenerateQuestions(GenerateQuestionsOptions? options)
    {
        var list = await _sessionManager.GenerateQuestionsAsync(
            Context.ConnectionId,
            options ?? new GenerateQuestionsOptions(),
            Context.ConnectionAborted);

        await Clients.Caller.SendAsync("Questions", new { data = list }, Context.ConnectionAborted);
    }
}
