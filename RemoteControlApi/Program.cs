using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RemoteControlApi.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// Configure SQL Server for persistence
var connectionString = builder.Configuration.GetConnectionString("Notifications")
    ?? "Server=(localdb)\\MSSQLLocalDB;Database=NotificationDb;Trusted_Connection=True;";

builder.Services.AddDbContext<NotificationDbContext>(options =>
    options.UseSqlServer(connectionString));
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
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    db.Database.EnsureCreated();
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
var provider = new FileExtensionContentTypeProvider();
provider.Mappings[".apk"] = "application/vnd.android.package-archive";

app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    ServeUnknownFileTypes = true
});

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

