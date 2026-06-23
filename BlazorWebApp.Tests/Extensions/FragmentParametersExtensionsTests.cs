using BlazorWebApp.Models;
using Xunit;

namespace BlazorWebApp.Tests.Extensions;

public class FragmentParametersExtensionsTests
{
    [Fact]
    public void GetString_ReturnsStringValue()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["sampler_name"] = "euler"
            }
        };

        // Act
        var result = fragment.GetString("sampler_name");

        // Assert
        Assert.Equal("euler", result);
    }

    [Fact]
    public void GetString_WithMissingKey_ReturnsDefault()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>()
        };

        // Act
        var result = fragment.GetString("missing_key", "default_value");

        // Assert
        Assert.Equal("default_value", result);
    }

    [Fact]
    public void GetString_WithNullValue_ReturnsDefault()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["key"] = null!
            }
        };

        // Act
        var result = fragment.GetString("key", "default");

        // Assert
        Assert.Equal("default", result);
    }

    [Fact]
    public void GetInt_ReturnsIntValue()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["steps"] = 20
            }
        };

        // Act
        var result = fragment.GetInt("steps");

        // Assert
        Assert.Equal(20, result);
    }

    [Fact]
    public void GetInt_ConvertsFromLong()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = 12345L
            }
        };

        // Act
        var result = fragment.GetInt("value");

        // Assert
        Assert.Equal(12345, result);
    }

    [Fact]
    public void GetInt_ConvertsFromDouble()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = 42.7
            }
        };

        // Act
        var result = fragment.GetInt("value");

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void GetInt_ConvertsFromString()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = "123"
            }
        };

        // Act
        var result = fragment.GetInt("value");

        // Assert
        Assert.Equal(123, result);
    }

    [Fact]
    public void GetInt_WithInvalidString_ReturnsDefault()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = "not_a_number"
            }
        };

        // Act
        var result = fragment.GetInt("value", 99);

        // Assert
        Assert.Equal(99, result);
    }

    [Fact]
    public void GetDouble_ReturnsDoubleValue()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["cfg"] = 7.5
            }
        };

        // Act
        var result = fragment.GetDouble("cfg");

        // Assert
        Assert.Equal(7.5, result);
    }

    [Fact]
    public void GetDouble_ConvertsFromInt()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = 42
            }
        };

        // Act
        var result = fragment.GetDouble("value");

        // Assert
        Assert.Equal(42.0, result);
    }

    [Fact]
    public void GetDouble_ConvertsFromString()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = "3.14159"
            }
        };

        // Act
        var result = fragment.GetDouble("value");

        // Assert
        Assert.Equal(3.14159, result);
    }

    [Fact]
    public void GetLong_ReturnsLongValue()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["seed"] = 123456789012345L
            }
        };

        // Act
        var result = fragment.GetLong("seed");

        // Assert
        Assert.Equal(123456789012345L, result);
    }

    [Fact]
    public void GetLong_ConvertsFromInt()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = 42
            }
        };

        // Act
        var result = fragment.GetLong("value");

        // Assert
        Assert.Equal(42L, result);
    }

    [Fact]
    public void GetLong_ConvertsFromString()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = "-1"
            }
        };

        // Act
        var result = fragment.GetLong("value");

        // Assert
        Assert.Equal(-1L, result);
    }

    [Fact]
    public void GetBool_ReturnsBoolValue()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["enabled"] = true
            }
        };

        // Act
        var result = fragment.GetBool("enabled");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetBool_ConvertsFromString()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = "true"
            }
        };

        // Act
        var result = fragment.GetBool("value");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetBool_ConvertsFromInt_NonZeroIsTrue()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = 1
            }
        };

        // Act
        var result = fragment.GetBool("value");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void GetBool_ConvertsFromInt_ZeroIsFalse()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = 0
            }
        };

        // Act
        var result = fragment.GetBool("value");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetFloat_ReturnsFloatValue()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = 3.14f
            }
        };

        // Act
        var result = fragment.GetFloat("value");

        // Assert
        Assert.Equal(3.14f, result, precision: 2);
    }

    [Fact]
    public void GetFloat_ConvertsFromDouble()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["value"] = 2.5
            }
        };

        // Act
        var result = fragment.GetFloat("value");

        // Assert
        Assert.Equal(2.5f, result);
    }

    [Fact]
    public void SetValue_SetsTypedValue()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>()
        };

        // Act
        fragment.SetValue("steps", 30);

        // Assert
        Assert.Equal(30, fragment.Values["steps"]);
    }

    [Fact]
    public void SetValue_OverwritesExistingValue()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["steps"] = 20
            }
        };

        // Act
        fragment.SetValue("steps", 40);

        // Assert
        Assert.Equal(40, fragment.Values["steps"]);
    }

    [Fact]
    public void HasValue_ReturnsTrueForExistingKey()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["key"] = "value"
            }
        };

        // Act & Assert
        Assert.True(fragment.HasValue("key"));
    }

    [Fact]
    public void HasValue_ReturnsFalseForMissingKey()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>()
        };

        // Act & Assert
        Assert.False(fragment.HasValue("missing"));
    }

    [Fact]
    public void GetValue_ReturnsRawValue()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>
            {
                ["key"] = "value"
            }
        };

        // Act
        var result = fragment.GetValue("key");

        // Assert
        Assert.Equal("value", result);
    }

    [Fact]
    public void GetValue_ReturnsNullForMissingKey()
    {
        // Arrange
        var fragment = new FragmentParameters
        {
            Values = new Dictionary<string, object>()
        };

        // Act
        var result = fragment.GetValue("missing");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Extensions_WorkWithNullValues()
    {
        // Arrange
        var fragment = new FragmentParameters { Values = null };

        // Act & Assert - should not throw, return defaults
        Assert.Equal("default", fragment.GetString("key", "default"));
        Assert.Equal(42, fragment.GetInt("key", 42));
        Assert.Equal(3.14, fragment.GetDouble("key", 3.14));
        Assert.Equal(100L, fragment.GetLong("key", 100L));
        Assert.True(fragment.GetBool("key", true));
        Assert.Equal(1.5f, fragment.GetFloat("key", 1.5f));
    }
}
