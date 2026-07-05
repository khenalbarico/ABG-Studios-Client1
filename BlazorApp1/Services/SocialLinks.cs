namespace BlazorApp1.Services;

/// <summary>Public contact/social links shown in the footer. Bound from wwwroot/appsettings.json.</summary>
public sealed class SocialLinks
{
    public string Facebook      { get; set; } = "";
    public string Instagram     { get; set; } = "";
    public string Tiktok        { get; set; } = "";
    public string BusinessEmail { get; set; } = "";
    public string BusinessPhone { get; set; } = "";
}
