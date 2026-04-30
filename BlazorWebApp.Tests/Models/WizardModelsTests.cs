using System.Text.Json;
using BlazorWebApp.Models;
using FluentAssertions;
using Xunit;

namespace BlazorWebApp.Tests.Models;

public class WizardModelsTests
{
    [Fact]
    public void WizardBody_RoundTrips_Through_CompactJson()
    {
        var body = new WizardBody
        {
            Stage = WizardStage.Iteration,
            IntroSectionIndex = 4,
            TurnCount = 7,
            CurrentDraft = "a lone lighthouse keeper",
            PendingQuestion = "What should we focus on next?",
            PendingOptions =
            {
                new WizardOption { Label = "Add fog", Hint = "evening mist" },
                new WizardOption { Label = "Specify camera angle" }
            },
            LastShownLabels = { "Add fog", "Specify camera angle" },
            LastActionVerb = "add",
            History =
            {
                new WizardTurn
                {
                    Source = "intro:subject",
                    Question = "Pick a subject",
                    Choice = "A solitary lighthouse keeper",
                    DraftBefore = "",
                    DraftAfter = "a lone lighthouse keeper",
                }
            },
        };

        var json = JsonSerializer.Serialize(body, WizardJsonOptions.Compact);
        var rt = JsonSerializer.Deserialize<WizardBody>(json, WizardJsonOptions.Compact)!;

        rt.Stage.Should().Be(WizardStage.Iteration);
        rt.IntroSectionIndex.Should().Be(4);
        rt.TurnCount.Should().Be(7);
        rt.CurrentDraft.Should().Be(body.CurrentDraft);
        rt.PendingQuestion.Should().Be(body.PendingQuestion);
        rt.PendingOptions.Should().HaveCount(2);
        rt.PendingOptions[0].Label.Should().Be("Add fog");
        rt.PendingOptions[0].Hint.Should().Be("evening mist");
        rt.LastShownLabels.Should().BeEquivalentTo(body.LastShownLabels);
        rt.LastActionVerb.Should().Be("add");
        rt.History.Should().HaveCount(1);
        rt.History[0].Source.Should().Be("intro:subject");
    }

    [Fact]
    public void Validator_TryParse_ReturnsTrue_For_WellFormedResponse()
    {
        var raw = """
        {
          "question": "What should we focus on next?",
          "options": [
            { "label": "Add fog", "hint": "evening mist" },
            { "label": "Specify camera angle" },
            { "label": "Time of day" }
          ]
        }
        """;

        WizardResponseValidator.TryParse(raw, out var resp).Should().BeTrue();
        resp.Question.Should().Be("What should we focus on next?");
        resp.Options.Should().HaveCount(3);
        resp.Options![0].Label.Should().Be("Add fog");
        resp.Options[0].Hint.Should().Be("evening mist");
    }

    [Fact]
    public void Validator_TryParse_StripsCodeFences_AndProse()
    {
        var raw = """
        Sure! Here is the JSON:
        ```json
        {"question":"Q?","options":[{"label":"A"},{"label":"B"},{"label":"C"}]}
        ```
        """;

        WizardResponseValidator.TryParse(raw, out var resp).Should().BeTrue();
        resp.Question.Should().Be("Q?");
        resp.Options.Should().HaveCount(3);
    }

    [Fact]
    public void Validator_Sanitize_TrimsTrailingPunctuation_AndDedupes()
    {
        var raw = new WizardLLMResponse
        {
            Question = "  What now?  ",
            Options = new List<WizardOption>
            {
                new() { Label = "  Add fog. " },
                new() { Label = "ADD FOG" },              // duplicate after normalization
                new() { Label = "Specify camera angle!" },
                new() { Label = "" },                       // dropped
                new() { Label = "Time of day" },
            }
        };

        var s = WizardResponseValidator.Sanitize(raw);
        s.Question.Should().Be("What now?");
        s.Options.Should().HaveCount(3);
        s.Options![0].Label.Should().Be("Add fog");
        s.Options[1].Label.Should().Be("Specify camera angle");
        s.Options[2].Label.Should().Be("Time of day");
    }

    [Fact]
    public void Validator_Sanitize_ClampsTooManyOptions_To_Six()
    {
        var raw = new WizardLLMResponse
        {
            Question = "Q?",
            Options = Enumerable.Range(0, 10)
                .Select(i => new WizardOption { Label = $"Option {i}" })
                .ToList()
        };

        var s = WizardResponseValidator.Sanitize(raw);
        s.Options.Should().HaveCount(WizardResponseValidator.MaxOptions);
    }

    [Fact]
    public void Validator_TryParse_ReturnsFalse_When_TooFewOptions()
    {
        var raw = """{"question":"Q?","options":[{"label":"only"}]}""";

        WizardResponseValidator.TryParse(raw, out _).Should().BeFalse();
    }

    [Fact]
    public void Validator_TryParse_ReturnsFalse_When_NoJson()
    {
        WizardResponseValidator.TryParse("plain text reply with no braces", out _).Should().BeFalse();
        WizardResponseValidator.TryParse("", out _).Should().BeFalse();
        WizardResponseValidator.TryParse(null, out _).Should().BeFalse();
    }

    [Fact]
    public void Validator_Sanitize_ClampsLabelLength_To_Sixty()
    {
        var longLabel = new string('x', 80);
        var raw = new WizardLLMResponse
        {
            Question = "Q?",
            Options = new List<WizardOption>
            {
                new() { Label = longLabel },
                new() { Label = "B" },
                new() { Label = "C" },
            }
        };

        var s = WizardResponseValidator.Sanitize(raw);
        s.Options![0].Label.Length.Should().Be(WizardResponseValidator.MaxLabelLength);
    }
}
