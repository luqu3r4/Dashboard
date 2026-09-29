using DashBoard.Core;
using DashBoard.Modules.Sistema;
using DashBoard.Web;
using DashBoard.Web.Components;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo("/keys"));

IDashboardModule[] modules = [new SistemaModule()];
foreach (var module in modules)
{
    module.ConfigureServices(builder.Services, builder.Configuration);
    builder.Services.AddSingleton(module);
}
var catalog = new ModuleCatalog(modules);
builder.Services.AddSingleton(catalog);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies([.. catalog.Assemblies]);

app.Run();
