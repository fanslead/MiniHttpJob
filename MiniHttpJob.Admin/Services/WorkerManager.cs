namespace MiniHttpJob.Admin.Services;

/// <summary>
/// Worker管理器接口
/// </summary>
public interface IWorkerManager
{
    Task RegisterWorkerAsync(WorkerInfo workerInfo);
    Task UnregisterWorkerAsync(string workerId);
    Task<WorkerInfo?> GetWorkerByIdAsync(string workerId);
    Task<IEnumerable<WorkerInfo>> GetAllWorkerInfoAsync();
    Task<IEnumerable<WorkerInfo>> GetAvailableWorkerInfoAsync();
    Task UpdateWorkerStatusAsync(WorkerStatusUpdate status);
    Task UpdateWorkerHeartbeatAsync(WorkerHeartbeat heartbeat);
    Task UpdateJobExecutionResultAsync(JobExecutionResult result);
    Task<WorkerInfo?> SelectWorkerForJobAsync(int jobId);
    Task AddOrUpdateWorkerAsync(string workerId, Worker worker);
    Task<IEnumerable<Worker>> GetAllWorkersAsync();
    Task CleanupInactiveWorkersAsync();
}

/// <summary>
/// Worker管理器实现
/// </summary>
public class WorkerManager : IWorkerManager
{
    private readonly ILogger<WorkerManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, WorkerInfo> _workers = new();
    private readonly Dictionary<string, DateTime> _lastHeartbeats = new();
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, Worker> _concurrentWorkers = new();

    public WorkerManager(ILogger<WorkerManager> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public Task RegisterWorkerAsync(WorkerInfo workerInfo)
    {
        lock (_lock)
        {
            _workers[workerInfo.WorkerId] = workerInfo;
            _lastHeartbeats[workerInfo.WorkerId] = DateTime.UtcNow;
        }

        _logger.LogInformation("Worker registered: {WorkerId} - {InstanceName}",
            workerInfo.WorkerId, workerInfo.InstanceName);

        return Task.CompletedTask;
    }

    public Task UnregisterWorkerAsync(string workerId)
    {
        lock (_lock)
        {
            _workers.Remove(workerId);
            _lastHeartbeats.Remove(workerId);
        }

        _logger.LogInformation("Worker unregistered: {WorkerId}", workerId);
        return Task.CompletedTask;
    }

    public Task<WorkerInfo?> GetWorkerByIdAsync(string workerId)
    {
        lock (_lock)
        {
            _workers.TryGetValue(workerId, out var worker);
            return Task.FromResult(worker);
        }
    }

    public Task<IEnumerable<WorkerInfo>> GetAllWorkerInfoAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_workers.Values.AsEnumerable());
        }
    }

    public Task<IEnumerable<WorkerInfo>> GetAvailableWorkerInfoAsync()
    {
        lock (_lock)
        {
            var availableWorkers = _workers.Values
                .Where(w => w.Capacity.CurrentRunningJobs < w.Capacity.MaxConcurrentJobs)
                .Where(w => _lastHeartbeats.ContainsKey(w.WorkerId) &&
                           DateTime.UtcNow - _lastHeartbeats[w.WorkerId] < TimeSpan.FromMinutes(2))
                .AsEnumerable();

            return Task.FromResult(availableWorkers);
        }
    }

    public Task UpdateWorkerStatusAsync(WorkerStatusUpdate status)
    {
        lock (_lock)
        {
            if (_workers.TryGetValue(status.WorkerId, out var worker))
            {
                worker.Capacity = status.Capacity;
                _lastHeartbeats[status.WorkerId] = DateTime.UtcNow;
            }
        }

        return Task.CompletedTask;
    }

    public Task UpdateWorkerHeartbeatAsync(WorkerHeartbeat heartbeat)
    {
        lock (_lock)
        {
            if (_workers.TryGetValue(heartbeat.WorkerId, out var worker))
            {
                worker.Capacity = heartbeat.Capacity;
                _lastHeartbeats[heartbeat.WorkerId] = heartbeat.Timestamp;
            }
        }

        return Task.CompletedTask;
    }

    public async Task UpdateJobExecutionResultAsync(JobExecutionResult result)
    {
        // 更新Worker的当前运行作业数
        lock (_lock)
        {
            if (_workers.TryGetValue(result.WorkerId, out var worker))
            {
                worker.Capacity.CurrentRunningJobs = Math.Max(0, worker.Capacity.CurrentRunningJobs - 1);
            }
        }

        // 保存执行结果到数据库 - use service provider to get scoped context
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<JobDbContext>();

        var execution = new JobExecution
        {
            JobId = result.JobId,
            ExecutionTime = result.ExecutionTime,
            Status = result.Success ? "Success" : "Failed",
            ErrorMessage = result.ErrorMessage,
            Response = result.Response.Length > 2000 ? result.Response.Substring(0, 2000) + "..." : result.Response
        };

        dbContext.JobExecutions.Add(execution);
        await dbContext.SaveChangesAsync();

        _logger.LogInformation("Job execution result saved: JobId={JobId}, Success={Success}, WorkerId={WorkerId}",
            result.JobId, result.Success, result.WorkerId);
    }

    public Task<WorkerInfo?> SelectWorkerForJobAsync(int jobId)
    {
        lock (_lock)
        {
            // 选择最空闲的Worker（运行作业数最少的）
            var selectedWorker = _workers.Values
                .Where(w => w.Capacity.CurrentRunningJobs < w.Capacity.MaxConcurrentJobs)
                .Where(w => _lastHeartbeats.ContainsKey(w.WorkerId) &&
                           DateTime.UtcNow - _lastHeartbeats[w.WorkerId] < TimeSpan.FromMinutes(2))
                .OrderBy(w => w.Capacity.CurrentRunningJobs)
                .ThenBy(w => w.Capacity.CpuUsage)
                .FirstOrDefault();

            if (selectedWorker != null)
            {
                selectedWorker.Capacity.CurrentRunningJobs++;
            }

            return Task.FromResult(selectedWorker);
        }
    }

    public Task CleanupInactiveWorkersAsync()
    {
        var threshold = DateTime.UtcNow.AddMinutes(-5); // 5分钟未心跳则认为离线
        var inactiveWorkers = new List<string>();

        lock (_lock)
        {
            foreach (var kvp in _lastHeartbeats)
            {
                if (kvp.Value < threshold)
                {
                    inactiveWorkers.Add(kvp.Key);
                }
            }

            foreach (var workerId in inactiveWorkers)
            {
                _workers.Remove(workerId);
                _lastHeartbeats.Remove(workerId);
            }
        }

        if (inactiveWorkers.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} inactive workers: {WorkerIds}",
                inactiveWorkers.Count, string.Join(", ", inactiveWorkers));
        }

        return Task.CompletedTask;
    }

    public Task AddOrUpdateWorkerAsync(string workerId, Worker worker)
    {
        _concurrentWorkers[workerId] = worker;
        return Task.CompletedTask;
    }

    Task<IEnumerable<Worker>> IWorkerManager.GetAllWorkersAsync()
    {
        return Task.FromResult(_concurrentWorkers.Values.AsEnumerable());
    }

    Task IWorkerManager.CleanupInactiveWorkersAsync()
    {
        var inactiveWorkers = _concurrentWorkers.Where(w => w.Value.LastHeartbeat < DateTime.UtcNow.AddMinutes(-5)).ToList();
        foreach (var worker in inactiveWorkers)
        {
            _concurrentWorkers.TryRemove(worker.Key, out _);
        }
        return Task.CompletedTask;
    }
}

public class Worker
{
    public string Id { get; set; } = null!;
    public DateTime LastHeartbeat { get; set; }
}