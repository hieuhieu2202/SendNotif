using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RemoteControlApi.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AppDatabase") ?? "Data Source=AppData/app.db";
connectionString = EnsureSqliteAbsolutePath(connectionString, builder.Environment.ContentRootPath);

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

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

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

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

static string EnsureSqliteAbsolutePath(string connectionString, string contentRoot)
{
    const string dataSourceKey = "Data Source=";
    var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
    for (var i = 0; i < parts.Length; i++)
    {
        var part = parts[i].Trim();
        if (!part.StartsWith(dataSourceKey, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var path = part[dataSourceKey.Length..].Trim();
        if (Path.IsPathRooted(path))
        {
            continue;
        }

        var absolutePath = Path.Combine(contentRoot, path);
        var directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        parts[i] = $"{dataSourceKey}{absolutePath}";
    }

    return string.Join(';', parts);
}
