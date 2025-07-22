var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/worker-log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "MiniHttpJob Worker API",
        Version = "v1",
        Description = "HTTP Job Execution Worker - Distributed task execution service"
    });
});

// Add health checks with memory and dependency checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("Worker is running"))
    .AddCheck("memory", () =>
    {
        var allocated = GC.GetTotalMemory(false);
        var threshold = 500 * 1024 * 1024; // 500MB threshold
        return allocated < threshold
            ? Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy($"Memory usage: {allocated / 1024 / 1024}MB")
            : Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Degraded($"High memory usage: {allocated / 1024 / 1024}MB");
    });

// Add business services
builder.Services.AddSingleton<IJobQueueService, JobQueueService>();
builder.Services.AddSingleton<ISignalRClientService, SignalRClientService>();
builder.Services.AddScoped<IJobExecutorService, JobExecutorService>();

// Add background services
builder.Services.AddHostedService<JobWorkerService>();

// Configure HttpClient with improved settings
builder.Services.AddHttpClient("JobClient", client =>
{
    var timeoutSeconds = builder.Configuration.GetValue("Worker:DefaultTimeoutSeconds", 30);
    client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    client.DefaultRequestHeaders.Add("User-Agent", "MiniHttpJob-Worker/1.0");
    client.DefaultRequestHeaders.Add("Accept", "*/*");
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler()
{
    AllowAutoRedirect = true,
    MaxAutomaticRedirections = 3,
    UseProxy = false // Disable proxy for better performance in most cases
});

// Configure CORS for development only
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:3000", "https://localhost:7000")
                  .WithMethods("GET", "POST")
                  .WithHeaders("Content-Type")
                  .AllowCredentials();
        });
    });
}

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

var app = builder.Build();

// Add global exception handling middleware
app.UseMiddleware<GlobalExceptionMiddleware>();

// Add response compression
app.UseResponseCompression();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MiniHttpJob Worker API v1");
        c.RoutePrefix = string.Empty;
        c.DocumentTitle = "MiniHttpJob Worker - Distributed Job Execution Service";
    });

    app.UseCors();
}

// Add health check endpoint with detailed response
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds,
            timestamp = DateTime.UtcNow
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

// Security headers for production
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        await next();
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Graceful shutdown with cleanup
app.Lifetime.ApplicationStopping.Register(() =>
{
    Log.Information("Worker is shutting down gracefully...");
});

// Ensure Serilog is properly disposed on shutdown
app.Lifetime.ApplicationStopped.Register(Log.CloseAndFlush);

Log.Information("MiniHttpJob Worker started successfully on {Environment} environment", app.Environment.EnvironmentName);
app.Run();
