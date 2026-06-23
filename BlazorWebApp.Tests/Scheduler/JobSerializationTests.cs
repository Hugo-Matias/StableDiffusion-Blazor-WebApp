using System.Text.Json;
using BlazorWebApp.Scheduler;
using BlazorWebApp.Scheduler.Directives;
using BlazorWebApp.Scheduler.Models;
using BlazorWebApp.Scheduler.Targets;
using BlazorWebApp.Scheduler.Variations;
using FluentAssertions;

namespace BlazorWebApp.Tests.Scheduler;

public class JobSerializationTests
{
    [Fact]
    public void FullJob_RoundTrips_ThroughCompactAndDefaultOptions()
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = "Test Job",
            Description = "Round-trip test",
            Status = JobStatus.Draft,
            WorkflowId = Guid.NewGuid(),
            OutputConfig = new JobOutputConfig { ProjectName = "P", FolderName = "F" },
            Actions =
            {
                new JobAction
                {
                    Order = 0,
                    Label = "cfg sweep",
                    Limit = 10,
                    PermutationOrder = PermutationOrder.Sequential,
                    Directives =
                    {
                        new SetValueDirective
                        {
                            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "steps" },
                            Value = 20
                        },
                        new AppendPromptDirective { Text = "masterpiece", IsPrefix = true }
                    },
                    Variations =
                    {
                        new RangeVariation
                        {
                            Target = new FragmentTarget { FragmentId = "main_sampler", ParamKey = "cfg" },
                            Start = 1, End = 8, Step = 1
                        },
                        new WildcardVariation
                        {
                            Target = new PromptTarget(),
                            CollectionName = "artists",
                            Count = 3
                        }
                    }
                },
                new JobAction
                {
                    Order = 1,
                    Label = "A/B LoRA",
                    PermutationOrder = PermutationOrder.Random,
                    RandomPermutationSeed = 123,
                    Variations =
                    {
                        new ToggleVariation
                        {
                            Target = new LoraTarget { LoraName = "styleA" },
                            OnValue = true,
                            OffValue = false
                        }
                    }
                }
            },
            RunState = new JobRunState
            {
                CurrentActionIndex = 1,
                CurrentIterationIndex = 4,
                TotalIterations = 27,
                CompletedImages = 20,
                FailedImages = 1,
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var opts in new[] { SchedulerJsonOptions.Default, SchedulerJsonOptions.Compact })
        {
            var json = JsonSerializer.Serialize(job, opts);
            var back = JsonSerializer.Deserialize<Job>(json, opts);

            back.Should().NotBeNull();
            back!.Id.Should().Be(job.Id);
            back.Name.Should().Be("Test Job");
            back.Status.Should().Be(JobStatus.Draft);
            back.Actions.Should().HaveCount(2);

            var first = back.Actions[0];
            first.Directives.Should().HaveCount(2);
            first.Directives[0].Should().BeOfType<SetValueDirective>();
            first.Variations[0].Should().BeOfType<RangeVariation>();
            first.Variations[1].Should().BeOfType<WildcardVariation>();

            back.Actions[1].PermutationOrder.Should().Be(PermutationOrder.Random);
            back.Actions[1].RandomPermutationSeed.Should().Be(123);
            back.Actions[1].Variations[0].Should().BeOfType<ToggleVariation>();

            back.RunState.CompletedImages.Should().Be(20);
            back.RunState.TotalIterations.Should().Be(27);
        }
    }

    [Fact]
    public void JobStatus_SerializesAsString()
    {
        var json = JsonSerializer.Serialize(new Job { Status = JobStatus.Running },
            SchedulerJsonOptions.Compact);
        json.Should().Contain("\"status\":\"Running\"");
    }
}
