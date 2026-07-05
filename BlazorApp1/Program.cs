using BlazorApp1;
using BlazorApp1.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same-origin by default: the SWA reverse proxy routes /api/* to the linked
// Function App. Api:BaseAddress overrides this for local development.
var apiBase = builder.Configuration["Api:BaseAddress"] ?? builder.HostEnvironment.BaseAddress;

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped(sp => new ApiClient(new HttpClient { BaseAddress = new Uri(apiBase) }));
builder.Services.AddScoped(sp => new AuthService(new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) }));
builder.Services.AddSingleton(builder.Configuration.GetSection("Socials").Get<SocialLinks>() ?? new SocialLinks());
builder.Services.AddSingleton<AppGlobalError>();

await builder.Build().RunAsync();
