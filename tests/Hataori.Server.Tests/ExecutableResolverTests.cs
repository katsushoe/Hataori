using FluentAssertions;
using Hataori.Application;

namespace Hataori.Server.Tests;

public sealed class ExecutableResolverTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"hataori-exeresolve-{Guid.NewGuid():N}");

    public ExecutableResolverTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Resolve_BareNameWithoutExtension_FindsCmdShimViaPathExt()
    {
        var cmdPath = Path.Combine(_directory, "codex.cmd");
        File.WriteAllText(cmdPath, "@echo off");

        var resolved = ExecutableResolver.Resolve("codex", [_directory], [".com", ".exe", ".bat", ".cmd"]);

        resolved.Should().Be(cmdPath);
    }

    [Fact]
    public void Resolve_PreferesEarlierExtensionOverLater()
    {
        File.WriteAllText(Path.Combine(_directory, "tool.exe"), string.Empty);
        var cmdPath = Path.Combine(_directory, "tool.cmd");
        File.WriteAllText(cmdPath, string.Empty);

        var resolved = ExecutableResolver.Resolve("tool", [_directory], [".cmd", ".exe"]);

        resolved.Should().Be(cmdPath);
    }

    [Fact]
    public void Resolve_NoMatchAnywhere_ReturnsOriginalCommandUnchanged()
    {
        var resolved = ExecutableResolver.Resolve("does-not-exist-anywhere", [_directory], [".exe", ".cmd"]);

        resolved.Should().Be("does-not-exist-anywhere");
    }

    [Fact]
    public void Resolve_AbsolutePath_ReturnsUnchangedWithoutSearching()
    {
        var absolutePath = Path.Combine(_directory, "missing.exe");

        var resolved = ExecutableResolver.Resolve(absolutePath, [_directory], [".exe"]);

        resolved.Should().Be(absolutePath);
    }

    [Fact]
    public void Resolve_NameAlreadyHasExtension_OnlyMatchesThatExactFile()
    {
        var exactPath = Path.Combine(_directory, "claude.exe");
        File.WriteAllText(exactPath, string.Empty);

        var resolved = ExecutableResolver.Resolve("claude.exe", [_directory], [".com", ".exe"]);

        resolved.Should().Be(exactPath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
