using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RemoteControlApi.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("AppDatabase")
    ?? "Server=10.220.130.125,1453;Database=SendNoti;User ID=MBD-AIOT;Password=123456ad!;TrustServerCertificate=True";

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

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
    await dbContext.Database.MigrateAsync();
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
