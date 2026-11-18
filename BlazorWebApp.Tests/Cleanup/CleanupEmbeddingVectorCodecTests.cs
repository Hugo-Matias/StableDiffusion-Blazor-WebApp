using BlazorWebApp.Services.Cleanup;
using FluentAssertions;

namespace BlazorWebApp.Tests.Cleanup;

public class CleanupEmbeddingVectorCodecTests
{
    private readonly CleanupEmbeddingVectorCodec _codec = new();

    [Fact]
    public void Normalize_ReturnsUnitVector()
    {
        var normalized = _codec.Normalize(new[] { 3f, 4f });

        normalized[0].Should().BeApproximately(0.6f, 0.0001f);
        normalized[1].Should().BeApproximately(0.8f, 0.0001f);
        _codec.CosineSimilarity(normalized, normalized).Should().BeApproximately(1f, 0.0001f);
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsLittleEndianFloat32()
    {
        var vector = new[] { 0.25f, -0.5f, 1.5f };

        var bytes = _codec.Serialize(vector);
        var roundTripped = _codec.Deserialize(bytes, vector.Length);

        bytes.Should().HaveCount(vector.Length * sizeof(float));
        roundTripped.Should().Equal(vector);
    }

    [Fact]
    public void Deserialize_RejectsDimensionMismatch()
    {
        var bytes = _codec.Serialize(new[] { 1f, 2f });

        var act = () => _codec.Deserialize(bytes, 3);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Vector byte length does not match the expected dimensions.*");
    }

    [Fact]
    public void Serialize_RejectsNonFiniteValues()
    {
        var act = () => _codec.Serialize(new[] { 1f, float.NaN });

        act.Should().Throw<ArgumentException>()
            .WithMessage("Embedding vector values must be finite.*");
    }
}