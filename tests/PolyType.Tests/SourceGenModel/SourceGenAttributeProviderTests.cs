using PolyType.SourceGenModel;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace PolyType.Tests.SourceGenModel;

public class SourceGenAttributeProviderTests
{
    [Fact]
    public void GetAttribute_Generic_ReturnsFirstMatchingAttribute()
    {
        // Arrange
        var provider = new SourceGenAttributeProvider(() => new Attribute[]
        {
            new DescriptionAttribute("Test description"),
            new RequiredAttribute(),
            new DisplayNameAttribute("Test display name")
        });

        // Act
        var description = provider.GetAttribute<DescriptionAttribute>();
        var required = provider.GetAttribute<RequiredAttribute>();
        var displayName = provider.GetAttribute<DisplayNameAttribute>();

        // Assert
        Assert.NotNull(description);
        Assert.Equal("Test description", description.Description);
        Assert.NotNull(required);
        Assert.NotNull(displayName);
        Assert.Equal("Test display name", displayName.DisplayName);
    }

    [Fact]
    public void GetAttribute_Generic_ReturnsNullWhenNotFound()
    {
        // Arrange
        var provider = new SourceGenAttributeProvider(() => new Attribute[]
        {
            new DescriptionAttribute("Test description")
        });

        // Act
        var result = provider.GetAttribute<RequiredAttribute>();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GetAttribute_Generic_ReturnsNullWhenEmpty()
    {
        // Arrange
        var provider = new SourceGenAttributeProvider(() => Array.Empty<Attribute>());

        // Act
        var result = provider.GetAttribute<RequiredAttribute>();

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void IsDefined_Generic_ReturnsTrueWhenAttributeExists()
    {
        // Arrange
        var provider = new SourceGenAttributeProvider(() => new Attribute[]
        {
            new DescriptionAttribute("Test description"),
            new RequiredAttribute()
        });

        // Act & Assert
        Assert.True(provider.IsDefined<DescriptionAttribute>());
        Assert.True(provider.IsDefined<RequiredAttribute>());
    }

    [Fact]
    public void IsDefined_Generic_ReturnsFalseWhenAttributeDoesNotExist()
    {
        // Arrange
        var provider = new SourceGenAttributeProvider(() => new Attribute[]
        {
            new DescriptionAttribute("Test description")
        });

        // Act & Assert
        Assert.False(provider.IsDefined<RequiredAttribute>());
        Assert.False(provider.IsDefined<DisplayNameAttribute>());
    }

    [Fact]
    public void IsDefined_Generic_ReturnsFalseWhenEmpty()
    {
        // Arrange
        var provider = new SourceGenAttributeProvider(() => Array.Empty<Attribute>());

        // Act & Assert
        Assert.False(provider.IsDefined<RequiredAttribute>());
    }

    [Fact]
    public void GetAttribute_Generic_ReturnsFirstWhenMultipleExist()
    {
        // Arrange
        var provider = new SourceGenAttributeProvider(() => new Attribute[]
        {
            new DescriptionAttribute("First description"),
            new DescriptionAttribute("Second description")
        });

        // Act
        var result = provider.GetAttribute<DescriptionAttribute>();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("First description", result.Description);
    }

    [Fact]
    public void IsDefined_Generic_ReturnsTrueWhenMultipleExist()
    {
        // Arrange
        var provider = new SourceGenAttributeProvider(() => new Attribute[]
        {
            new DescriptionAttribute("First description"),
            new DescriptionAttribute("Second description")
        });

        // Act & Assert
        Assert.True(provider.IsDefined<DescriptionAttribute>());
    }
}
