using BlazorApp1.Services;

namespace BlazorApp1.Tests;

public class ApiBaseUrlTests
{
    const string SwaOrigin = "https://lively-sea-123.azurestaticapps.net/";

    [Fact]
    public void Resolve_UsesConfiguredAddress_WhenSet()
    {
        var result = ApiBaseUrl.Resolve("http://localhost:7071/", SwaOrigin);

        Assert.Equal("http://localhost:7071/", result);
    }

    [Fact]
    public void Resolve_AppendsTrailingSlash_ToConfiguredAddress()
    {
        var result = ApiBaseUrl.Resolve("http://localhost:7071", SwaOrigin);

        Assert.Equal("http://localhost:7071/", result);
    }

    [Fact]
    public void Resolve_FallsBackToHostOrigin_WhenNotConfigured()
    {
        var result = ApiBaseUrl.Resolve(null, SwaOrigin);

        Assert.Equal(SwaOrigin, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_FallsBackToHostOrigin_WhenConfiguredValueIsBlank(string configured)
    {
        var result = ApiBaseUrl.Resolve(configured, SwaOrigin);

        Assert.Equal(SwaOrigin, result);
    }
}
