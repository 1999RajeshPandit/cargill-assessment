using Crr.Api.Repositories;
using Crr.Api.Services;
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
builder.Services.AddSingleton<IRiskPositionRepository, CsvRiskPositionRepository>();
builder.Services.AddSingleton<IRiskPositionService, RiskPositionService>();
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
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Corporate Risk Reporting API v1");
    options.RoutePrefix = "swagger";
});
app.MapControllers();

app.Run();
