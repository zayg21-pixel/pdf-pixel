using Microsoft.AspNetCore.Builder;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

WebApplication app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/octet-stream"
});

app.Run();

