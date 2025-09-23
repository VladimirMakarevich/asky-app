using AskyBackend.Hubs;
using AskyBackend.Options;
using AskyBackend.Services;
using AskyBackend.Services.Context;
using AskyBackend.Services.Questions;
using AskyBackend.Services.Questions.Llm;
using AskyBackend.Services.Summarization;
using AskyBackend.Speech;
using AskyBackend.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("default", policy =>
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(_ => true));
});

builder.Services.AddSignalR();
builder.Services.Configure<AzureSpeechOptions>(builder.Configuration.GetSection("AzureSpeech"));
builder.Services.Configure<ConversationContextOptions>(builder.Configuration.GetSection("ConversationContext"));
builder.Services.Configure<RollingSummaryOptions>(builder.Configuration.GetSection("RollingSummary"));
builder.Services.Configure<LlmServiceOptions>(builder.Configuration.GetSection("LlmService"));
builder.Services.Configure<QuestionGenerationOptions>(builder.Configuration.GetSection("QuestionGeneration"));
builder.Services.Configure<PiiRedactionOptions>(builder.Configuration.GetSection("PiiRedaction"));
builder.Services.Configure<ThrottlingOptions>(builder.Configuration.GetSection("Throttling"));
builder.Services.AddSingleton<IConversationSessionManager, ConversationSessionManager>();
builder.Services.AddSingleton<AzureSpeechSessionFactory>();
builder.Services.AddSingleton<NoopSpeechSessionFactory>();
builder.Services.AddSingleton<IConversationContextStore, InMemoryConversationContextStore>();
builder.Services.AddSingleton<IRollingSummaryService, HeuristicRollingSummaryService>();
builder.Services.AddSingleton<IThrottlingService, ThrottlingService>();
builder.Services.AddSingleton<ITelemetryRecorder, TelemetryRecorder>();
builder.Services.AddSingleton<ISpeechSessionFactory>(sp =>
{
    var azureFactory = sp.GetRequiredService<AzureSpeechSessionFactory>();
    if (azureFactory.IsConfigured)
    {
        return azureFactory;
    }

    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogWarning("Azure Speech is not configured. Falling back to NoopSpeechSessionFactory.");
    return sp.GetRequiredService<NoopSpeechSessionFactory>();
});
builder.Services.AddHttpClient<LlmQuestionClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<LlmServiceOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(opts.BaseUrl))
    {
        client.BaseAddress = new Uri(opts.BaseUrl, UriKind.Absolute);
    }

    client.Timeout = System.Threading.Timeout.InfiniteTimeSpan;
});

builder.Services.AddSingleton<ILlmQuestionClient>(sp =>
{
    var options = sp.GetRequiredService<IOptions<LlmServiceOptions>>().Value;
    if (string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        sp.GetRequiredService<ILoggerFactory>().CreateLogger("Startup")
            .LogWarning("LLM service is not configured. Question generation will return empty results.");
        return new NullLlmQuestionClient();
    }

    return ActivatorUtilities.CreateInstance<LlmQuestionClient>(sp);
});

builder.Services.AddSingleton<IQuestionFallbackGenerator, FourWhFallbackGenerator>();
builder.Services.AddSingleton<IPiiRedactor, SimplePiiRedactor>();
builder.Services.AddSingleton<IQuestionGenerationService, QuestionGenerationService>();

builder.Services.AddApplicationInsightsTelemetry();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("default");

app.MapHub<AsrHub>("/hubs/asr");
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
