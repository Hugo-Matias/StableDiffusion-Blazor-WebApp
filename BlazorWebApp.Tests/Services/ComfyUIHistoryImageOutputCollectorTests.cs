using System.Text.Json;
using BlazorWebApp.Services;
using FluentAssertions;

namespace BlazorWebApp.Tests.Services;

public class ComfyUIHistoryImageOutputCollectorTests
{
    [Fact]
    public void Collect_ShouldMapImagesByExpectedOutputNodeId()
    {
        var promptId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        using var doc = JsonDocument.Parse("""
        {
          "11111111-1111-1111-1111-111111111111": {
            "outputs": {
              "save_front": {
                "images": [
                  { "filename": "front_00001.png", "subfolder": "Character", "type": "output" }
                ]
              },
              "save_neutral": {
                "images": [
                  { "filename": "neutral_00001.png", "subfolder": "Character/Faces", "type": "output" }
                ]
              },
              "ignored": {
                "images": [
                  { "filename": "ignored.png", "subfolder": "", "type": "output" }
                ]
              }
            }
          }
        }
        """);

        var result = ComfyUIHistoryImageOutputCollector.Collect(
            doc.RootElement,
            promptId,
            ["save_front", "save_neutral", "missing"],
            "C:\\Comfy\\output");

        result.Keys.Should().BeEquivalentTo("save_front", "save_neutral", "missing");
        result["save_front"].Should().ContainSingle()
            .Which.FullPath.Should().Be(Path.Combine("C:\\Comfy\\output", "Character", "front_00001.png"));
        result["save_neutral"].Should().ContainSingle()
            .Which.RelativePath.Should().Be(Path.Combine("Character/Faces", "neutral_00001.png"));
        result["missing"].Should().BeEmpty();
    }
}