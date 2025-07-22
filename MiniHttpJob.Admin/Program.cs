var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .WriteTo.Console()
    .WriteTo.File("logs/admin-log-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddMiniControllers();
builder.Services.AddEndpointsApiExplorer();

// Add API versioning
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.ApiVersionReader = ApiVersionReader.Combine(
        new QueryStringApiVersionReader("version"),
        new UrlSegmentApiVersionReader()
    );
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "MiniHttpJob Admin API",
        Version = "v1",
        Description = "HTTP Job Scheduler Management System API - Supports Cron expression based scheduled tasks"
    });
});

// Configure options
builder.Services.Configure<JobSchedulerOptions>(
    builder.Configuration.GetSection(JobSchedulerOptions.SectionName));
builder.Services.Configure<HttpClientOptions>(
    builder.Configuration.GetSection(HttpClientOptions.SectionName));
builder.Services.Configure<MonitoringOptions>(
    builder.Configuration.GetSection(MonitoringOptions.SectionName));

// Add EF Core DbContext
builder.Services.AddDbContext<JobDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseSqlite(connectionString);

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Add basic health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<JobDbContext>("database")
    .AddCheck("memory", () =>
    {
        var allocated = GC.GetTotalMemory(false);
        var threshold = 512 * 1024 * 1024; // 512MB threshold
        return allocated < threshold
            ? HealthCheckResult.Healthy($"Memory usage: {allocated / 1024 / 1024}MB")
            : HealthCheckResult.Degraded($"High memory usage: {allocated / 1024 / 1024}MB");
    });

// Add SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(30);
    options.MaximumReceiveMessageSize = 102400; // 100KB limit
});

// Add Quartz.NET services
builder.Services.AddQuartzScheduler(builder.Configuration);

// Add core business services
builder.Services.AddScoped<IJobService, JobService>();

// Register WorkerManager as scoped to allow DbContext dependency
builder.Services.AddScoped<IWorkerManager, WorkerManager>();

// Add performance metrics service (missing registration)
builder.Services.AddSingleton<IPerformanceMetricsService, PerformanceMetricsService>();

// Add FluentValidation
builder.Services.AddFluentValidationAutoValidation();

// Configure HttpClient
var httpClientOptions = builder.Configuration.GetSection(HttpClientOptions.SectionName).Get<HttpClientOptions>() ?? new HttpClientOptions();
builder.Services.AddHttpClient("JobClient", client =>
{
    client.Timeout = TimeSpan.FromSeconds(httpClientOptions.TimeoutSeconds);
    client.DefaultRequestHeaders.Add("User-Agent", "MiniHttpJob/1.0");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Configure CORS
builder.Services.AddCors(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:3000", "http://localhost:5173", "https://localhost:7000")
                  .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                  .WithHeaders("Content-Type", "Authorization", "X-Requested-With")
                  .AllowCredentials();
        });
    }
    else
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
        options.AddDefaultPolicy(policy =>
        {
            if (allowedOrigins.Length > 0)
            {
                policy.WithOrigins(allowedOrigins)
                      .WithMethods("GET", "POST", "PUT", "DELETE")
                      .WithHeaders("Content-Type", "Authorization")
                      .AllowCredentials();
            }
            else
            {
                policy.AllowAnyOrigin()
                      .WithMethods("GET", "POST", "PUT", "DELETE")
                      .WithHeaders("Content-Type");
            }
        });
    }
});

// Add response compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json" });
});

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseResponseCompression();

// Ensure database is created and seeded in development
using (var scope = app.Services.CreateScope())
{
    try
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<JobDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        // Seed sample data in development
        if (app.Environment.IsDevelopment())
        {
            await DataSeeder.SeedAsync(dbContext);
        }

        Log.Information("Database initialized successfully");
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Database initialization failed");
        throw;
    }
}

// Start Quartz Scheduler (optional)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var schedulerService = scope.ServiceProvider.GetService<IJobSchedulerService>();
        if (schedulerService != null)
        {
            await schedulerService.StartAsync();
            Log.Information("Job scheduler started successfully");
        }
        else
        {
            Log.Warning("Job scheduler service not available");
        }
    }
    catch (Exception ex)
    {
        Log.Warning(ex, "Failed to start job scheduler - continuing without it");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MiniHttpJob Admin API v1");
        c.DocumentTitle = "MiniHttpJob - HTTP Job Scheduler Management System";
        c.DefaultModelsExpandDepth(-1);
    });
}

app.UseCors();
app.MapHub<JobHub>("/jobHub");

// Add health check endpoints
app.MapHealthChecks("/health", new HealthCheckOptions
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
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        }));
    }
});

app.MapHealthChecks("/health/ready");

// Security headers for production
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["X-Content-Security-Policy"] = "default-src 'self'";
        await next();
    });
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapMiniController();

app.Lifetime.ApplicationStopping.Register(() =>
{
    Log.Information("Application is shutting down...");
});

app.Lifetime.ApplicationStopped.Register(Log.CloseAndFlush);

Log.Information("MiniHttpJob Admin started successfully on {Environment}", app.Environment.EnvironmentName);
app.Run();

// Make Program class accessible for integration testing
public partial class Program { }
