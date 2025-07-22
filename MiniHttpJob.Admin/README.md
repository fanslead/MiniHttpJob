# MiniHttpJob - HTTP任务调度系统

一个基于ASP.NET Core和Quartz.NET的纯HTTP Job定时调度系统。

## 功能特性

### 核心功能
- ? 纯HTTP Job：通过HTTP请求执行任务
- ? 定时调度：支持基于Cron表达式的任务调度
- ? 任务管理：支持任务的新增、删除、修改、暂停、恢复
- ? 数据恢复：程序宕机后，自动从数据库拉取数据恢复
- ? 可扩展性：支持分布式部署，任务高可用
- ? 监控与日志：记录任务执行历史、状态及异常

### 技术架构
- **Web框架**: ASP.NET Core 8.0 Minimal API
- **任务调度**: Quartz.NET 3.14.0
- **数据库**: SQLite (支持集群模式)
- **日志**: Serilog
- **依赖注入**: Microsoft.Extensions.DependencyInjection

## 项目结构

```
MiniHttpJob.Admin/
├── Controllers/          # API控制器
│   ├── JobController.cs     # 任务管理API
│   └── MonitorController.cs # 监控统计API
├── Services/             # 服务层
│   ├── IJobService.cs       # 任务服务接口
│   ├── JobService.cs        # 任务服务实现
│   ├── IJobSchedulerService.cs # 调度服务接口
│   └── JobSchedulerService.cs  # 调度服务实现
├── Models/               # 数据模型
│   ├── Job.cs              # 任务模型
│   └── JobExecution.cs     # 任务执行历史模型
├── DTOs/                 # 数据传输对象
│   └── JobDtos.cs          # 任务相关DTO
├── Data/                 # 数据上下文
│   └── JobDbContext.cs     # EF Core数据上下文
├── Quartz/               # Quartz配置
│   ├── HttpJob.cs          # HTTP任务执行器
│   ├── SimpleJobFactory.cs # 任务工厂
│   └── QuartzConfiguration.cs # Quartz配置
├── Configuration/        # 配置模型
│   └── Options.cs          # 配置选项
├── GlobalUsings.cs       # 全局引用
├── Program.cs            # 程序入口
└── appsettings.json      # 配置文件
```

## API接口

### 任务管理API

#### 创建任务
```http
POST /api/job
Content-Type: application/json

{
  "name": "测试任务",
  "cronExpression": "0/30 * * * * ?",
  "httpMethod": "POST",
  "url": "https://api.example.com/webhook",
  "headers": "{\"Authorization\": \"Bearer token\"}",
  "body": "{\"message\": \"Hello World\"}"
}
```

#### 获取所有任务
```http
GET /api/job
```

#### 获取单个任务
```http
GET /api/job/{id}
```

#### 更新任务
```http
PUT /api/job/{id}
Content-Type: application/json

{
  "name": "更新的任务",
  "cronExpression": "0/60 * * * * ?",
  "httpMethod": "GET",
  "url": "https://api.example.com/status",
  "headers": "{}",
  "body": ""
}
```

#### 删除任务
```http
DELETE /api/job/{id}
```

#### 暂停任务
```http
POST /api/job/{id}/pause
```

#### 恢复任务
```http
POST /api/job/{id}/resume
```

#### 获取任务执行历史
```http
GET /api/job/{id}/history
```

### 监控API

#### 获取仪表板数据
```http
GET /api/monitor/dashboard
```

#### 获取统计信息
```http
GET /api/monitor/statistics
```

## 配置说明

### appsettings.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=MiniHttpJob.db"
  },
  "JobScheduler": {
    "EnableClustering": true,
    "InstanceName": "MiniHttpJobScheduler",
    "MaxConcurrentJobs": 10,
    "MisfireThreshold": 60000
  },
  "HttpClient": {
    "TimeoutSeconds": 30,
    "MaxRetries": 3,
    "FollowRedirects": true
  }
}
```

## 快速开始

### 1. 克隆项目
```bash
git clone <repository-url>
cd MiniHttpJob
```

### 2. 恢复依赖
```bash
dotnet restore
```

### 3. 运行数据库迁移
```bash
dotnet ef database update --project MiniHttpJob.Admin
```

### 4. 启动应用
```bash
dotnet run --project MiniHttpJob.Admin
```

### 5. 访问Swagger UI
```
https://localhost:5001/swagger
```

## Cron表达式示例

| 表达式 | 说明 |
|--------|------|
| `0/30 * * * * ?` | 每30秒执行一次 |
| `0 0/5 * * * ?` | 每5分钟执行一次 |
| `0 0 9-17 * * ?` | 每天9点到17点，每小时执行一次 |
| `0 0 12 * * ?` | 每天中午12点执行 |
| `0 0 12 ? * MON-FRI` | 工作日中午12点执行 |

## 分布式部署

系统支持分布式部署，多个实例可以共享同一个数据库，Quartz.NET的集群功能确保任务不会重复执行。

### 集群配置
```json
{
  "JobScheduler": {
    "EnableClustering": true,
    "InstanceName": "MiniHttpJobScheduler"
  }
}
```

## 监控和日志

### 日志
- 控制台输出
- 文件日志（按天滚动）：`logs/log-{Date}.txt`

### 监控指标
- 任务总数、活跃任务数、暂停任务数
- 24小时/7天执行统计
- 成功率统计
- 最近执行记录

## 开发计划

- [ ] Web管理界面
- [ ] 任务依赖关系
- [ ] 任务执行超时控制
- [ ] 更多HTTP认证方式
- [ ] 任务执行结果回调
- [ ] 更多数据库支持（MySQL、PostgreSQL）
- [ ] Docker容器化部署

## 许可证

MIT License