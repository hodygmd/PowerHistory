namespace PowerHistory.Infrastructure.Tests;

using PowerHistory.Infrastructure.Backup;

public class BackupServiceTests
{
    [Fact]
    public void Create_CopiesTheHistoryFile()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "content\n");
        var service = new BackupService(Path.Combine(temp.Path, "backups"));

        var backup = service.Create(path);

        Assert.True(File.Exists(backup.Path));
        Assert.Equal("content\n", File.ReadAllText(backup.Path));
        Assert.Equal(Path.GetFullPath(path), backup.SourcePath);
    }

    [Fact]
    public void Create_UsesAUniqueNameForEveryBackup()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "content\n");
        var service = new BackupService(Path.Combine(temp.Path, "backups"));

        var first = service.Create(path);
        var second = service.Create(path);

        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.Path, second.Path);
        Assert.Equal(2, service.List(path).Count);
    }

    [Fact]
    public void List_ReturnsOnlyTheBackupsOfTheGivenSource()
    {
        using var temp = new TempDirectory();
        var first = temp.Write("first.txt", "a\n");
        var second = temp.Write("second.txt", "b\n");
        var service = new BackupService(Path.Combine(temp.Path, "backups"));

        service.Create(first);
        service.Create(second);

        var backups = service.List(first);

        Assert.Single(backups);
        Assert.Equal(Path.GetFullPath(first), backups[0].SourcePath);
    }

    [Fact]
    public void List_ReturnsNothingWhenThereIsNoBackupDirectory()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "a\n");
        var service = new BackupService(Path.Combine(temp.Path, "missing"));

        Assert.Empty(service.List(path));
    }

    [Fact]
    public void Restore_PutsTheBackupContentBack()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "original\n");
        var service = new BackupService(Path.Combine(temp.Path, "backups"));

        var backup = service.Create(path);
        File.WriteAllText(path, "changed\n");

        service.Restore(backup, path);

        Assert.Equal("original\n", File.ReadAllText(path));
    }

    [Fact]
    public void Restore_BacksUpTheCurrentStateBeforeRestoring()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "original\n");
        var service = new BackupService(Path.Combine(temp.Path, "backups"));

        var backup = service.Create(path);
        File.WriteAllText(path, "changed\n");

        service.Restore(backup, path);

        var backups = service.List(path);
        Assert.Equal(2, backups.Count);
        Assert.Contains(backups, entry => File.ReadAllText(entry.Path) == "changed\n");
    }

    [Fact]
    public void Create_ThrowsWhenTheSourceDoesNotExist()
    {
        using var temp = new TempDirectory();
        var service = new BackupService(Path.Combine(temp.Path, "backups"));

        Assert.Throws<FileNotFoundException>(() => service.Create(Path.Combine(temp.Path, "missing.txt")));
    }
}
