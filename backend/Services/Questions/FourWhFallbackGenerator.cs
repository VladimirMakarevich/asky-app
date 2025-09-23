using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using AskyBackend.Contracts;
using AskyBackend.Options;
using AskyBackend.Services.Context;
using Microsoft.Extensions.Options;

namespace AskyBackend.Services.Questions;

public sealed class FourWhFallbackGenerator : IQuestionFallbackGenerator
{
    private static readonly (string Template, string Tag)[] Templates =
    {
        ("What is the primary goal for {0}?", "goal"),
        ("Who is responsible for delivering {0}?", "ownership"),
        ("When do we expect key milestones for {0}?", "timeline"),
        ("Where will {0} have the biggest impact?", "scope"),
        ("How will we mitigate the main risks around {0}?", "risk")
    };

    private static readonly Regex SentenceRegex = new("(?<sentence>[^.!?]+[.!?])", RegexOptions.Compiled);

    private readonly QuestionGenerationOptions _options;

    public FourWhFallbackGenerator(IOptions<QuestionGenerationOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<QuestionItem> Generate(ConversationContextSnapshot snapshot, GenerateQuestionsOptions options)
    {
        var focus = DetermineFocus(snapshot, options);
        var askedRecently = new HashSet<string>(snapshot.AskedRecently, StringComparer.OrdinalIgnoreCase);
        var results = new List<QuestionItem>();

        foreach (var (template, tag) in Templates)
        {
            var text = string.Format(CultureInfo.InvariantCulture, template, focus);
            if (askedRecently.Contains(text))
            {
                continue;
            }

            results.Add(new QuestionItem(text, new[] { tag }, Confidence: 0.2, Novelty: 0.1));
            if (results.Count >= _options.FallbackQuestionCount)
            {
                break;
            }
        }

        return results;
    }

    private static string DetermineFocus(ConversationContextSnapshot snapshot, GenerateQuestionsOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Topic))
        {
            return options.Topic.Trim();
        }

        if (!string.IsNullOrWhiteSpace(snapshot.RollingSummary))
        {
            var sentence = ExtractLeadingSentence(snapshot.RollingSummary);
            if (!string.IsNullOrWhiteSpace(sentence))
            {
                return sentence.Trim();
            }
        }

        if (!string.IsNullOrWhiteSpace(snapshot.LastWindow))
        {
            var sentence = ExtractLeadingSentence(snapshot.LastWindow);
            if (!string.IsNullOrWhiteSpace(sentence))
            {
                return sentence.Trim();
            }
        }

        if (snapshot.KnownFacts.Count > 0)
        {
            var kvp = snapshot.KnownFacts.First();
            return $"the detail '{kvp.Key}: {kvp.Value}'";
        }

        return "this discussion";
    }

    private static string ExtractLeadingSentence(string text)
    {
        var match = SentenceRegex.Match(text);
        if (match.Success)
        {
            var sentence = match.Groups["sentence"].Value;
            if (sentence.Length > 120)
            {
                return sentence.Substring(0, 120).Trim() + "…";
            }

            return sentence;
        }

        if (text.Length > 120)
        {
            return text.Substring(0, 120).Trim() + "…";
        }

        return text;
    }
}
