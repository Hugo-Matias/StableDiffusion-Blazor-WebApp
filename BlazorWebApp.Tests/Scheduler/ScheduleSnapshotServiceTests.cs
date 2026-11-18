using BlazorWebApp.Scheduler;
using BlazorWebApp.Scheduler.Models;
using FluentAssertions;

namespace BlazorWebApp.Tests.Scheduler;

public class ScheduleSnapshotServiceTests
{
    [Fact]
    public void HasPending_DefaultsToFalse()
    {
        var svc = new ScheduleSnapshotService();
        svc.HasPending.Should().BeFalse();
        svc.Consume().Should().BeNull();
    }

    [Fact]
    public void Set_ThenConsume_ReturnsAndClears()
    {
        var svc = new ScheduleSnapshotService();
        var job = new Job { Name = "snapshot" };

        svc.Set(job);
        svc.HasPending.Should().BeTrue();

        var consumed = svc.Consume();
        consumed.Should().BeSameAs(job);
        svc.HasPending.Should().BeFalse();
        svc.Consume().Should().BeNull();
    }

    [Fact]
    public void Set_Overwrites_PreviousSnapshot()
    {
        var svc = new ScheduleSnapshotService();
        svc.Set(new Job { Name = "first" });
        svc.Set(new Job { Name = "second" });

        svc.Consume()!.Name.Should().Be("second");
    }
}
