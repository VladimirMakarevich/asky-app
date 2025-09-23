using AskyBackend.Contracts;
using AskyBackend.Options;
using AskyBackend.Services.Context;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OptionsFactory = Microsoft.Extensions.Options.Options;

namespace AskyBackend.Tests.Services;

public sealed class ConversationContextStoreTests
{
    [Fact]
    public async Task SlidingWindow_Removes_ExpiredEntries()
    {
        var options = OptionsFactory.Create(new ConversationContextOptions
        {
            SlidingWindow = TimeSpan.FromMilliseconds(30),
            MaxWindowCharacters = 200
        });
        var store = new InMemoryConversationContextStore(options, NullLogger<InMemoryConversationContextStore>.Instance);
        var connectionId = Guid.NewGuid().ToString();
        await store.CreateOrResetAsync(connectionId, CancellationToken.None);

        await store.RegisterFinalTranscriptAsync(connectionId, new FinalTranscript("first entry", 0, 1000), CancellationToken.None);
        await Task.Delay(40);
        await store.RegisterFinalTranscriptAsync(connectionId, new FinalTranscript("second entry", 1000, 1000), CancellationToken.None);

        var snapshot = await store.GetSnapshotAsync(connectionId, CancellationToken.None);
        Assert.Contains("second entry", snapshot.LastWindow);
        Assert.DoesNotContain("first entry", snapshot.LastWindow);
    }

    [Fact]
    public async Task AskedRecently_Respects_MaxSize_AndDeduplicates()
    {
        var options = OptionsFactory.Create(new ConversationContextOptions
        {
            MaxAskedRecently = 2
        });
        var store = new InMemoryConversationContextStore(options, NullLogger<InMemoryConversationContextStore>.Instance);
        var connectionId = Guid.NewGuid().ToString();
        await store.CreateOrResetAsync(connectionId, CancellationToken.None);

        await store.RegisterAskedQuestionAsync(connectionId, "Question A", CancellationToken.None);
        await store.RegisterAskedQuestionAsync(connectionId, "Question B", CancellationToken.None);
        await store.RegisterAskedQuestionAsync(connectionId, "Question A", CancellationToken.None);
        await store.RegisterAskedQuestionAsync(connectionId, "Question C", CancellationToken.None);

        var snapshot = await store.GetSnapshotAsync(connectionId, CancellationToken.None);
        Assert.Equal(2, snapshot.AskedRecently.Count);
        Assert.Contains("Question A", snapshot.AskedRecently);
        Assert.Contains("Question C", snapshot.AskedRecently);
        Assert.DoesNotContain("Question B", snapshot.AskedRecently);
    }
}
