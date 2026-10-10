using Crr.Api.Services;
using Crr.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Corporate Risk Reporting API",
        Version = "v1",
        Description = "Browse, filter, summarize, and export the synthetic risk-position dataset."
    });
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});
var connectionString = builder.Configuration.GetConnectionString("RiskPositions");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Configure ConnectionStrings:RiskPositions in appsettings.json, .NET user-secrets, or the ConnectionStrings__RiskPositions environment variable.");
builder.Services.AddDbContext<RiskPositionDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped<IRiskPositionService, RiskPositionService>();
builder.Services.AddScoped<RiskPositionCsvImporter>();
builder.Services.AddHttpClient<IPythonAiSummaryClient, PythonAiSummaryClient>(client =>
{
    var baseUrl = builder.Configuration["PythonAi:BaseUrl"] ?? "http://localhost:8000/";
    client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : $"{baseUrl}/");
    client.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()));

var app = builder.Build();
if (args.Length == 2 && args[0] == "--import-csv")
{
    var csvPath = Path.GetFullPath(args[1]);
    if (!Path.IsPathRooted(args[1]) && !File.Exists(csvPath))
    {
        var repositoryRelativePath = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", args[1]));
        if (File.Exists(repositoryRelativePath)) csvPath = repositoryRelativePath;
    }
    await using var scope = app.Services.CreateAsyncScope();
    var count = await scope.ServiceProvider.GetRequiredService<RiskPositionCsvImporter>()
        .ImportAsync(csvPath, CancellationToken.None);
    Console.WriteLine($"Imported {count:N0} risk positions.");
    return;
}
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Corporate Risk Reporting API v1");
    options.RoutePrefix = "swagger";
});
app.MapControllers();


app.Run();

