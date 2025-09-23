using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AskyBackend.Contracts;
using AskyBackend.Options;
using AskyBackend.Services.Context;
using Microsoft.Extensions.Options;

namespace AskyBackend.Services.Questions;

public sealed class SimplePiiRedactor : IPiiRedactor
{
    private static readonly Regex EmailRegex = new("[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new("(?:\\+\\d{1,3}[\\s-]?)?(?:\\(\\d{2,3}\\)[\\s-]?)?\\d{3}[\\s-]?\\d{2,4}[\\s-]?\\d{2,4}", RegexOptions.Compiled);

    private readonly PiiRedactionOptions _options;

    public SimplePiiRedactor(IOptions<PiiRedactionOptions> options)
    {
        _options = options.Value;
    }

    public (ConversationContextSnapshot Snapshot, GenerateQuestionsOptions Options) Redact(
        ConversationContextSnapshot snapshot,
        GenerateQuestionsOptions options)
    {
        if (!_options.Enabled)
        {
            return (snapshot, options);
        }

        var sanitizedFacts = snapshot.KnownFacts
            .Select(kvp => (Key: Sanitize(kvp.Key), Value: Sanitize(kvp.Value)))
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key) && !string.IsNullOrWhiteSpace(pair.Value))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);

        var sanitizedAsked = snapshot.AskedRecently
            .Select(Sanitize)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        var sanitizedSnapshot = snapshot with
        {
            RollingSummary = Sanitize(snapshot.RollingSummary),
            LastWindow = Sanitize(snapshot.LastWindow),
            KnownFacts = sanitizedFacts,
            AskedRecently = sanitizedAsked
        };

        var sanitizedOptions = new GenerateQuestionsOptions(
            Topic: Sanitize(options.Topic),
            PreferredStyle: Sanitize(options.PreferredStyle),
            ForceRefresh: options.ForceRefresh);

        return (sanitizedSnapshot, sanitizedOptions);
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var result = EmailRegex.Replace(value, "<pii:email>");
        result = PhoneRegex.Replace(result, "<pii:phone>");
        return result;
    }
}
