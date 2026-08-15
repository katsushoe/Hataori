namespace Hataori.Core.Tasks;

/// <summary>
/// Task間の関連を表します。
/// </summary>
public sealed record TaskRelation(string TaskId, string RelatedTaskId, string RelationType);
