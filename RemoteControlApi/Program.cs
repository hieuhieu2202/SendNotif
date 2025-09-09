using Microsoft.AspNetCore.Http.Features;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.CustomSchemaIds(t => t.FullName?.Replace('+', '.') ?? t.Name);
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "RemoteControlApi", Version = "v1" });
});

// (match với controller: ~1.5GB)
builder.Services.Configure<FormOptions>(o => { o.MultipartBodyLengthLimit = 1_500_000_000; });

var app = builder.Build();

// Bật Swagger cả Prod và set server URL theo request (tôn trọng PathBase)
app.UseSwagger(c =>
{
    c.PreSerializeFilters.Add((swagger, httpReq) =>
    {
        var basePath = httpReq.PathBase.HasValue ? httpReq.PathBase.Value : string.Empty;
        swagger.Servers = new List<OpenApiServer>
        {
            new OpenApiServer { Url = $"{httpReq.Scheme}://{httpReq.Host.Value}{basePath}" }
        };
    });
});
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("v1/swagger.json", "RemoteControlApi v1");
    c.RoutePrefix = "swagger"; // => /<base>/swagger
});

// Static files (uploads,…)
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseHttpsRedirection();

app.MapControllers();

// Redirect "/" -> "/<base>/swagger"
app.MapGet("/", ctx =>
{
    var basePath = ctx.Request.PathBase.HasValue ? ctx.Request.PathBase.Value : string.Empty;
    ctx.Response.Redirect($"{basePath}/swagger", permanent: false);
    return Task.CompletedTask;
});

app.Run();
