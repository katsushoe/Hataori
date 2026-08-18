using Hataori.Core.Tasks;

namespace Hataori.Application.Tasks;

/// <summary>
/// Taskライフサイクルのユースケースを提供します。
/// </summary>
public sealed class TaskService
{
    private readonly ITaskRepository _repository;
    private readonly TimeProvider _timeProvider;

    public TaskService(ITaskRepository repository, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<HataoriTask> StartAsync(
        string taskId,
        string taskName,
        string agentId,
        string? conversationId,
        string? originMessageId,
        string summary,
        string currentWork,
        CancellationToken cancellationToken)
    {
        var task = HataoriTask.Start(taskId, taskName, agentId, conversationId, originMessageId, summary, currentWork, _timeProvider.GetUtcNow());
        await _repository.AddAsync(task, cancellationToken).ConfigureAwait(false);
        return task;
    }

    public async Task<HataoriTask> HeartbeatAsync(string taskId, string currentWork, int progressPercent, CancellationToken cancellationToken)
    {
        return await HeartbeatAsync(taskId, currentWork, progressPercent, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HataoriTask> HeartbeatAsync(string taskId, string currentWork, int progressPercent, string? message, CancellationToken cancellationToken)
    {
        var task = await GetRequiredAsync(taskId, cancellationToken).ConfigureAwait(false);
        task.Heartbeat(currentWork, progressPercent, _timeProvider.GetUtcNow());
        await _repository.UpdateAsync(task, "heartbeat", message, cancellationToken).ConfigureAwait(false);
        return task;
    }

    public async Task<HataoriTask> CompleteAsync(string taskId, string result, CancellationToken cancellationToken)
    {
        var task = await GetRequiredAsync(taskId, cancellationToken).ConfigureAwait(false);
        task.Complete(result, _timeProvider.GetUtcNow());
        await _repository.UpdateAsync(task, "completed", cancellationToken).ConfigureAwait(false);
        return task;
    }

    public Task<IReadOnlyList<HataoriTask>> ListAsync(HataoriTaskStatus? status, string? agentId, CancellationToken cancellationToken)
    {
        return _repository.ListAsync(status, agentId, cancellationToken);
    }

    /// <summary>
    /// 提案するTaskの名称・概要と、他Agentのactive Taskとのキーワード重複を検索します。
    /// 構造化されたscope項目がないため、あくまで簡易な参考情報です。
    /// </summary>
    public async Task<IReadOnlyList<HataoriTask>> FindConflictsAsync(string taskName, string? summary, string? excludeAgentId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskName);
        var active = await _repository.ListAsync(HataoriTaskStatus.Active, null, cancellationToken).ConfigureAwait(false);
        var keywords = Tokenize(taskName).Concat(Tokenize(summary ?? string.Empty)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (keywords.Count == 0)
        {
            return [];
        }

        return active
            .Where(task => string.IsNullOrWhiteSpace(excludeAgentId) || !string.Equals(task.AgentId, excludeAgentId, StringComparison.OrdinalIgnoreCase))
            .Where(task => Tokenize(task.TaskName).Concat(Tokenize(task.Summary)).Concat(Tokenize(task.CurrentWork)).Any(keywords.Contains))
            .ToArray();
    }

    /// <summary>
    /// 「修正」「作業」のような、どのTaskにもほぼ必ず現れる汎用的な作業動詞・名詞です。
    /// 2文字bigramマッチではこれらが無関係なTask同士を誤って一致させるため、キーワードから除外します。
    /// </summary>
    private static readonly HashSet<string> GenericWorkTerms = new(StringComparer.Ordinal)
    {
        "修正", "作業", "実装", "対応", "確認", "変更", "更新", "追加", "削除",
        "完了", "開始", "終了", "実行", "調査", "改修", "検討", "設定", "管理",
    };

    /// <summary>
    /// 空白・記号を境界に文字種の連続区間へ分割し、日本語（CJK）区間はさらに2文字ずつの重なる部分文字列へ展開します。
    /// 単語分かち書きのない日本語のTask名・概要でも部分一致を検出するための簡易処理で、形態素解析等は使用しません。
    /// </summary>
    private static IEnumerable<string> Tokenize(string text)
    {
        foreach (var run in SplitScriptRuns(text))
        {
            if (IsCjk(run[0]))
            {
                for (var i = 0; i < run.Length - 1; i++)
                {
                    var bigram = run.Substring(i, 2);
                    if (!GenericWorkTerms.Contains(bigram))
                    {
                        yield return bigram;
                    }
                }
            }
            else if (run.Length >= 2)
            {
                yield return run;
            }
        }
    }

    private static IEnumerable<string> SplitScriptRuns(string text)
    {
        var run = new System.Text.StringBuilder();
        bool? runIsCjk = null;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character) || char.IsPunctuation(character) || char.IsSymbol(character))
            {
                if (run.Length > 0)
                {
                    yield return run.ToString();
                    run.Clear();
                }

                runIsCjk = null;
                continue;
            }

            var isCjk = IsCjk(character);
            if (runIsCjk is not null && runIsCjk != isCjk)
            {
                yield return run.ToString();
                run.Clear();
            }

            run.Append(character);
            runIsCjk = isCjk;
        }

        if (run.Length > 0)
        {
            yield return run.ToString();
        }
    }

    private static bool IsCjk(char character) =>
        character is (>= '぀' and <= 'ヿ') or (>= '一' and <= '鿿') or (>= 'ｦ' and <= 'ﾝ');

    public Task<HataoriTask?> GetAsync(string taskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        return _repository.GetAsync(taskId, cancellationToken);
    }

    public Task<IReadOnlyList<TaskHistoryEntry>> GetHistoryAsync(string taskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        return _repository.GetHistoryAsync(taskId, cancellationToken);
    }

    public Task<IReadOnlyList<TaskRelation>> GetRelationsAsync(string taskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        return _repository.GetRelationsAsync(taskId, cancellationToken);
    }

    public async Task AddRelationAsync(string taskId, string relatedTaskId, string relationType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(relatedTaskId);
        ArgumentException.ThrowIfNullOrWhiteSpace(relationType);
        await GetRequiredAsync(taskId, cancellationToken).ConfigureAwait(false);
        await GetRequiredAsync(relatedTaskId, cancellationToken).ConfigureAwait(false);
        await _repository.AddRelationAsync(new TaskRelation(taskId, relatedTaskId, relationType), cancellationToken).ConfigureAwait(false);
    }

    public Task<HataoriTask> CancelAsync(string taskId, string? result, CancellationToken cancellationToken)
    {
        return EndAsync(taskId, HataoriTaskStatus.Cancelled, "cancelled", result, cancellationToken);
    }

    public Task<HataoriTask> FailAsync(string taskId, string result, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(result);
        return EndAsync(taskId, HataoriTaskStatus.Failed, "failed", result, cancellationToken);
    }

    public Task<HataoriTask> ExpireAsync(string taskId, CancellationToken cancellationToken)
    {
        return EndAsync(taskId, HataoriTaskStatus.Expired, "expired", null, cancellationToken);
    }

    private async Task<HataoriTask> EndAsync(string taskId, HataoriTaskStatus status, string eventType, string? result, CancellationToken cancellationToken)
    {
        var task = await GetRequiredAsync(taskId, cancellationToken).ConfigureAwait(false);
        task.End(status, result, _timeProvider.GetUtcNow());
        await _repository.UpdateAsync(task, eventType, cancellationToken).ConfigureAwait(false);
        return task;
    }

    private async Task<HataoriTask> GetRequiredAsync(string taskId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);
        return await _repository.GetAsync(taskId, cancellationToken).ConfigureAwait(false)
            ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");
    }
}
