# MiniHttpJob - Distributed HTTP Job Scheduler

[![.NET 8](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Build Status](https://img.shields.io/badge/build-passing-brightgreen.svg)]()

A modern, distributed HTTP Job Scheduler built with ASP.NET Core and Quartz.NET, designed for reliability, scalability, and ease of use.

## ?? Features

### Core Capabilities
- **HTTP Job Execution**: Execute scheduled tasks via HTTP requests with full REST API support
- **Cron-based Scheduling**: Flexible scheduling using industry-standard Cron expressions
- **Complete Job Management**: Create, read, update, delete, pause, and resume jobs via REST API
- **Automatic Recovery**: Persistent job storage with automatic recovery from system failures
- **Distributed Architecture**: Admin/Worker separation for horizontal scaling
- **Comprehensive Monitoring**: Real-time execution tracking, performance metrics, and health checks
- **Production-Ready**: Built-in logging, error handling, and monitoring capabilities

### Technical Highlights
- **Modern Architecture**: ASP.NET Core 8.0 with clean architecture principles
- **Robust Scheduling**: Quartz.NET 3.14.0 with clustering support
- **Multiple Databases**: SQLite (default), PostgreSQL support
- **Structured Logging**: Serilog with JSON formatting and multiple sinks
- **Real-time Communication**: SignalR for live status updates
- **Enterprise Security**: CORS, security headers, input validation
- **Health Monitoring**: Comprehensive health checks and metrics collection

## ?? Project Structure
MiniHttpJob/
©À©¤©¤ MiniHttpJob.Admin/           # Management & Scheduling Service
©¦   ©À©¤©¤ Controllers/             # REST API Controllers
©¦   ©¦   ©À©¤©¤ JobController.cs     # Job Management API
©¦   ©¦   ©À©¤©¤ MonitorController.cs # Monitoring & Statistics
©¦   ©¦   ©¸©¤©¤ TestController.cs    # Health & Testing endpoints
©¦   ©À©¤©¤ Services/                # Business Logic
©¦   ©¦   ©À©¤©¤ JobService.cs        # Job CRUD operations
©¦   ©¦   ©À©¤©¤ JobSchedulerService.cs # Quartz integration
©¦   ©¦   ©¸©¤©¤ WorkerManager.cs     # Worker coordination
©¦   ©À©¤©¤ Data/                    # Data Access Layer
©¦   ©¦   ©À©¤©¤ JobDbContext.cs      # EF Core context
©¦   ©¦   ©¸©¤©¤ DataSeeder.cs        # Sample data seeding
©¦   ©À©¤©¤ Quartz/                  # Quartz.NET components
©¦   ©¦   ©À©¤©¤ HttpJob.cs           # Job execution logic
©¦   ©¦   ©¸©¤©¤ DistributedHttpJob.cs # Distributed execution
©¦   ©¸©¤©¤ Hubs/                    # SignalR real-time communication
©À©¤©¤ MiniHttpJob.Worker/          # Job Execution Service
©¦   ©À©¤©¤ Services/                # Worker services
©¦   ©¦   ©À©¤©¤ JobExecutorService.cs # HTTP request execution
©¦   ©¦   ©À©¤©¤ JobQueueService.cs   # Queue management
©¦   ©¦   ©¸©¤©¤ SignalRClientService.cs # Admin communication
©¦   ©¸©¤©¤ Program.cs               # Worker startup
©À©¤©¤ MiniHttpJob.Shared/          # Shared Components
©¦   ©À©¤©¤ DTOs/                    # Data Transfer Objects
©¦   ©À©¤©¤ Validators/              # FluentValidation rules
©¦   ©À©¤©¤ Middleware/              # Global exception handling
©¦   ©¸©¤©¤ SignalR/                 # SignalR contracts
©¸©¤©¤ MiniHttpJob.Tests/           # Unit & Integration Tests
    ©¸©¤©¤ JobExecutorServiceTests.cs # Test suites
## ??? Installation & Setup

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Git
- (Optional) PostgreSQL for production use

### Quick Start

1. **Clone the Repository**git clone https://github.com/your-org/MiniHttpJob.git
cd MiniHttpJob
2. **Restore Dependencies**dotnet restore
3. **Initialize Database**# For development (SQLite)
   dotnet ef database update --project MiniHttpJob.Admin
4. **Start the Admin Service**dotnet run --project MiniHttpJob.Admin
5. **Start the Worker Service** (Optional for distributed setup)dotnet run --project MiniHttpJob.Worker
6. **Access the Application**
   - Swagger UI: `https://localhost:7000/swagger`
   - Health Check: `https://localhost:7000/health`

## ?? API Documentation

### Job Management Endpoints

#### Create a New JobPOST /api/job
Content-Type: application/json

{
  "name": "Daily Health Check",
  "cronExpression": "0 0 9 * * ?",
  "httpMethod": "GET",
  "url": "https://api.example.com/health",
  "headers": "{\"Authorization\": \"Bearer your-token\"}",
  "body": ""
}
#### Get All JobsGET /api/job
#### Get Job by IDGET /api/job/{id}
#### Update JobPUT /api/job/{id}
Content-Type: application/json
#### Delete JobDELETE /api/job/{id}
#### Pause/Resume JobPOST /api/job/{id}/pause
POST /api/job/{id}/resume
#### Get Job Execution HistoryGET /api/job/{id}/history
### Monitoring Endpoints

#### Dashboard StatisticsGET /api/monitor/dashboard
#### Detailed StatisticsGET /api/monitor/statistics
#### Health CheckGET /health
GET /health/ready
## ?? Configuration

### Environment-Specific Settings

#### Development (`appsettings.json`){
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=MiniHttpJob.db"
  },
  "JobScheduler": {
    "EnableClustering": false,
    "MaxConcurrentJobs": 10
  }
}
#### Production (`appsettings.Production.json`){
  "ConnectionStrings": {
    "DefaultConnection": "${DATABASE_CONNECTION_STRING}"
  },
  "JobScheduler": {
    "EnableClustering": true,
    "MaxConcurrentJobs": 20
  },
  "Monitoring": {
    "EnableTracing": true,
    "Alerting": {
      "Enabled": true
    }
  }
}
### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `DATABASE_CONNECTION_STRING` | Database connection string | SQLite local file |
| `ADMIN_URL` | Admin service URL for workers | `https://localhost:7000` |
| `MAX_CONCURRENT_JOBS` | Maximum concurrent job execution | `10` |
| `INSTANCE_NAME` | Scheduler instance name | `MiniHttpJobScheduler` |

## ?? Cron Expression Examples

| Expression | Description |
|------------|-------------|
| `0/30 * * * * ?` | Every 30 seconds |
| `0 0/5 * * * ?` | Every 5 minutes |
| `0 0 9-17 * * ?` | Every hour between 9 AM and 5 PM |
| `0 0 12 * * ?` | Daily at noon |
| `0 0 12 ? * MON-FRI` | Weekdays at noon |
| `0 0 0 1 * ?` | First day of every month |

## ??? Production Deployment

### Docker Deployment

1. **Build Images**docker build -t minihttpjob-admin -f MiniHttpJob.Admin/Dockerfile .
docker build -t minihttpjob-worker -f MiniHttpJob.Worker/Dockerfile .
2. **Run with Docker Compose**docker-compose up -d
### Kubernetes Deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: minihttpjob-admin
spec:
  replicas: 2
  selector:
    matchLabels:
      app: minihttpjob-admin
  template:
    spec:
      containers:
      - name: admin
        image: minihttpjob-admin:latest
        env:
        - name: DATABASE_CONNECTION_STRING
          value: "Host=postgres;Database=minihttpjob;Username=user;Password=pass"
## ?? Monitoring & Operations

### Health Checks

The system provides comprehensive health monitoring:

- **Database connectivity**: Ensures EF Core can connect to the database
- **Worker connectivity**: Monitors distributed worker health
- **Memory usage**: Tracks system resource consumption
- **Job execution**: Validates job processing pipeline

### Logging

Structured logging with Serilog includes:

- **Request/Response logging**: All API interactions
- **Job execution tracking**: Detailed execution logs
- **Error reporting**: Comprehensive exception details
- **Performance metrics**: Response times and throughput

### Metrics Collection

Key performance indicators:

- Job execution success/failure rates
- Average execution times
- System resource utilization
- Worker distribution statistics

## ?? Testing

### Run Unit Testsdotnet test MiniHttpJob.Tests
### Run with Coveragedotnet test --collect:"XPlat Code Coverage"
### Integration Testingdotnet test --filter Category=Integration
## ?? Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### Development Guidelines

- Follow C# coding conventions
- Write unit tests for new features
- Update documentation for API changes
- Use semantic versioning for releases

## ?? Roadmap

- [ ] **Q1 2024**: Docker containerization and Kubernetes support
- [ ] **Q2 2024**: Web-based management dashboard
- [ ] **Q3 2024**: Advanced monitoring and alerting
- [ ] **Q4 2024**: Plugin system for custom job types

## ?? License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## ?? Support

- **Documentation**: [Wiki](https://github.com/your-org/MiniHttpJob/wiki)
- **Issues**: [GitHub Issues](https://github.com/your-org/MiniHttpJob/issues)
- **Discussions**: [GitHub Discussions](https://github.com/your-org/MiniHttpJob/discussions)

## ?? Acknowledgments

- [Quartz.NET](https://www.quartz-scheduler.net/) for job scheduling
- [ASP.NET Core](https://docs.microsoft.com/aspnet/core/) for web framework
- [Entity Framework Core](https://docs.microsoft.com/ef/core/) for data access
- [Serilog](https://serilog.net/) for structured logging