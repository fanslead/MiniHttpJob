# MiniHttpJob - HTTP�������ϵͳ

һ������ASP.NET Core��Quartz.NET�Ĵ�HTTP Job��ʱ����ϵͳ��

## ��������

### ���Ĺ���
- ? ��HTTP Job��ͨ��HTTP����ִ������
- ? ��ʱ���ȣ�֧�ֻ���Cron���ʽ���������
- ? �������֧�������������ɾ�����޸ġ���ͣ���ָ�
- ? ���ݻָ�������崻����Զ������ݿ���ȡ���ݻָ�
- ? ����չ�ԣ�֧�ֲַ�ʽ��������߿���
- ? �������־����¼����ִ����ʷ��״̬���쳣

### �����ܹ�
- **Web���**: ASP.NET Core 8.0 Minimal API
- **�������**: Quartz.NET 3.14.0
- **���ݿ�**: SQLite (֧�ּ�Ⱥģʽ)
- **��־**: Serilog
- **����ע��**: Microsoft.Extensions.DependencyInjection

## ��Ŀ�ṹ

```
MiniHttpJob.Admin/
������ Controllers/          # API������
��   ������ JobController.cs     # �������API
��   ������ MonitorController.cs # ���ͳ��API
������ Services/             # �����
��   ������ IJobService.cs       # �������ӿ�
��   ������ JobService.cs        # �������ʵ��
��   ������ IJobSchedulerService.cs # ���ȷ���ӿ�
��   ������ JobSchedulerService.cs  # ���ȷ���ʵ��
������ Models/               # ����ģ��
��   ������ Job.cs              # ����ģ��
��   ������ JobExecution.cs     # ����ִ����ʷģ��
������ DTOs/                 # ���ݴ������
��   ������ JobDtos.cs          # �������DTO
������ Data/                 # ����������
��   ������ JobDbContext.cs     # EF Core����������
������ Quartz/               # Quartz����
��   ������ HttpJob.cs          # HTTP����ִ����
��   ������ SimpleJobFactory.cs # ���񹤳�
��   ������ QuartzConfiguration.cs # Quartz����
������ Configuration/        # ����ģ��
��   ������ Options.cs          # ����ѡ��
������ GlobalUsings.cs       # ȫ������
������ Program.cs            # �������
������ appsettings.json      # �����ļ�
```

## API�ӿ�

### �������API

#### ��������
```http
POST /api/job
Content-Type: application/json

{
  "name": "��������",
  "cronExpression": "0/30 * * * * ?",
  "httpMethod": "POST",
  "url": "https://api.example.com/webhook",
  "headers": "{\"Authorization\": \"Bearer token\"}",
  "body": "{\"message\": \"Hello World\"}"
}
```

#### ��ȡ��������
```http
GET /api/job
```

#### ��ȡ��������
```http
GET /api/job/{id}
```

#### ��������
```http
PUT /api/job/{id}
Content-Type: application/json

{
  "name": "���µ�����",
  "cronExpression": "0/60 * * * * ?",
  "httpMethod": "GET",
  "url": "https://api.example.com/status",
  "headers": "{}",
  "body": ""
}
```

#### ɾ������
```http
DELETE /api/job/{id}
```

#### ��ͣ����
```http
POST /api/job/{id}/pause
```

#### �ָ�����
```http
POST /api/job/{id}/resume
```

#### ��ȡ����ִ����ʷ
```http
GET /api/job/{id}/history
```

### ���API

#### ��ȡ�Ǳ������
```http
GET /api/monitor/dashboard
```

#### ��ȡͳ����Ϣ
```http
GET /api/monitor/statistics
```

## ����˵��

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

## ���ٿ�ʼ

### 1. ��¡��Ŀ
```bash
git clone <repository-url>
cd MiniHttpJob
```

### 2. �ָ�����
```bash
dotnet restore
```

### 3. �������ݿ�Ǩ��
```bash
dotnet ef database update --project MiniHttpJob.Admin
```

### 4. ����Ӧ��
```bash
dotnet run --project MiniHttpJob.Admin
```

### 5. ����Swagger UI
```
https://localhost:5001/swagger
```

## Cron���ʽʾ��

| ���ʽ | ˵�� |
|--------|------|
| `0/30 * * * * ?` | ÿ30��ִ��һ�� |
| `0 0/5 * * * ?` | ÿ5����ִ��һ�� |
| `0 0 9-17 * * ?` | ÿ��9�㵽17�㣬ÿСʱִ��һ�� |
| `0 0 12 * * ?` | ÿ������12��ִ�� |
| `0 0 12 ? * MON-FRI` | ����������12��ִ�� |

## �ֲ�ʽ����

ϵͳ֧�ֲַ�ʽ���𣬶��ʵ�����Թ���ͬһ�����ݿ⣬Quartz.NET�ļ�Ⱥ����ȷ�����񲻻��ظ�ִ�С�

### ��Ⱥ����
```json
{
  "JobScheduler": {
    "EnableClustering": true,
    "InstanceName": "MiniHttpJobScheduler"
  }
}
```

## ��غ���־

### ��־
- ����̨���
- �ļ���־�������������`logs/log-{Date}.txt`

### ���ָ��
- ������������Ծ����������ͣ������
- 24Сʱ/7��ִ��ͳ��
- �ɹ���ͳ��
- ���ִ�м�¼

## �����ƻ�

- [ ] Web�������
- [ ] ����������ϵ
- [ ] ����ִ�г�ʱ����
- [ ] ����HTTP��֤��ʽ
- [ ] ����ִ�н���ص�
- [ ] �������ݿ�֧�֣�MySQL��PostgreSQL��
- [ ] Docker����������

## ���֤

MIT License