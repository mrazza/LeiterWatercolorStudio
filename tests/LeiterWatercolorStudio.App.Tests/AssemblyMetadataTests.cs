using System;
using System.Reflection;
using Xunit;

namespace LeiterWatercolorStudio.App.Tests;

public class AssemblyMetadataTests
{
    [Fact]
    public void Assembly_HasExpectedVersioningAndMetadata()
    {
        Assembly assembly = typeof(Program).Assembly;
        Version? version = assembly.GetName().Version;
        Assert.NotNull(version);
        Assert.NotEqual(new Version(0, 0, 0, 0), version);
        Assert.NotEqual(new Version(1, 0, 0, 0), version);
        Assert.Equal(0, version.Major);
        Assert.Equal(1, version.Minor);
        Assert.Equal(2, version.Build);

        string? product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
        Assert.Equal("Leiter Watercolor Studio", product);

        string? company = assembly.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company;
        Assert.Equal("Matt Razza", company);

        string? copyright = assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
        Assert.NotNull(copyright);
        Assert.Contains("Matt Razza", copyright);
    }
}
