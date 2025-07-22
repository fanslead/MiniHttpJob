namespace MiniHttpJob.Worker.Services;

/// <summary>
/// ��ҵ���з���ӿ�
/// </summary>
public interface IJobQueueService
{
    Task EnqueueJobAsync(JobExecutionCommand command);
    Task<JobExecutionCommand?> DequeueJobAsync(CancellationToken cancellationToken = default);
    Task<int> GetQueueSizeAsync();
    Task<int> GetRunningJobCountAsync();
    Task<List<int>> GetRunningJobIdsAsync();
    void AddRunningJob(int jobId);
    void RemoveRunningJob(int jobId);
}

/// <summary>
/// ����Channel����ҵ���з���ʵ��
/// </summary>
public class JobQueueService : IJobQueueService
{
    private readonly Channel<JobExecutionCommand> _jobChannel;
    private readonly ChannelWriter<JobExecutionCommand> _writer;
    private readonly ChannelReader<JobExecutionCommand> _reader;
    private readonly HashSet<int> _runningJobs = new();
    private readonly object _lock = new();
    private readonly ILogger<JobQueueService> _logger;

    public JobQueueService(ILogger<JobQueueService> logger, IConfiguration configuration)
    {
        _logger = logger;

        var maxQueueSize = configuration.GetValue("Worker:MaxQueueSize", 1000);

        // �����н�Channel����������ʱ������
        var options = new BoundedChannelOptions(maxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _jobChannel = Channel.CreateBounded<JobExecutionCommand>(options);
        _writer = _jobChannel.Writer;
        _reader = _jobChannel.Reader;

        _logger.LogInformation("Job queue service initialized with max queue size: {MaxQueueSize}", maxQueueSize);
    }

    public async Task EnqueueJobAsync(JobExecutionCommand command)
    {
        try
        {
            await _writer.WriteAsync(command);
            _logger.LogInformation("Job enqueued: JobId={JobId}, JobName={JobName}", command.JobId, command.JobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue job: JobId={JobId}", command.JobId);
            throw;
        }
    }

    public async Task<JobExecutionCommand?> DequeueJobAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (await _reader.WaitToReadAsync(cancellationToken))
            {
                if (_reader.TryRead(out var command))
                {
                    AddRunningJob(command.JobId);
                    _logger.LogDebug("Job dequeued: JobId={JobId}, JobName={JobName}", command.JobId, command.JobName);
                    return command;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // ����ȡ������������¼����
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to dequeue job");
        }

        return null;
    }

    public Task<int> GetQueueSizeAsync()
    {
        // Channelû��ֱ�ӻ�ȡ���д�С�ķ��������ﷵ�ع���ֵ
        // ��ʵ�����������У����Կ���ά��һ�������ļ�����
        return Task.FromResult(0);
    }

    public Task<int> GetRunningJobCountAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_runningJobs.Count);
        }
    }

    public Task<List<int>> GetRunningJobIdsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_runningJobs.ToList());
        }
    }

    public void AddRunningJob(int jobId)
    {
        lock (_lock)
        {
            _runningJobs.Add(jobId);
        }
        _logger.LogDebug("Job added to running list: JobId={JobId}", jobId);
    }

    public void RemoveRunningJob(int jobId)
    {
        lock (_lock)
        {
            _runningJobs.Remove(jobId);
        }
        _logger.LogDebug("Job removed from running list: JobId={JobId}", jobId);
    }
}