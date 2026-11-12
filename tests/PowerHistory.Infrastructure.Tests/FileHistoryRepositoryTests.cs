namespace PowerHistory.Infrastructure.Tests;

using PowerHistory.Core.Models;
using PowerHistory.Infrastructure.History;

public class FileHistoryRepositoryTests
{
    private readonly FileHistoryRepository _repository = new();

    [Fact]
    public void Read_PreservesContentAndOrder()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "first\nsecond\nthird\n");

        var document = _repository.Read(path);

        Assert.Equal(new[] { "first", "second", "third" }, document.Entries.Select(entry => entry.Text));
        Assert.Equal(new[] { 0, 1, 2 }, document.Entries.Select(entry => entry.Index));
    }

    [Fact]
    public void Read_EmptyFileReturnsNoEntries()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", string.Empty);

        var document = _repository.Read(path);

        Assert.Empty(document.Entries);
    }

    [Fact]
    public void Read_FileWithOnlyANewLineReturnsOneEmptyEntry()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "\n");

        var document = _repository.Read(path);

        Assert.Single(document.Entries);
        Assert.Equal(string.Empty, document.Entries[0].Text);
    }

    [Fact]
    public void Read_KeepsEmptyLinesBetweenCommands()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "a\n\nb\n");

        var document = _repository.Read(path);

        Assert.Equal(new[] { "a", "", "b" }, document.Entries.Select(entry => entry.Text));
    }

    [Fact]
    public void Read_DetectsWindowsNewLines()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "a\r\nb\r\n");

        var document = _repository.Read(path);

        Assert.Equal("\r\n", document.Format.NewLine);
        Assert.True(document.Format.EndsWithNewLine);
    }

    [Fact]
    public void Read_KeepsSpecialCharactersAndSpaces()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "  echo 'áéí ñ'\nGet-ChildItem -Path \"C:\\Program Files\"\n");

        var document = _repository.Read(path);

        Assert.Equal("  echo 'áéí ñ'", document.Entries[0].Text);
        Assert.Equal("Get-ChildItem -Path \"C:\\Program Files\"", document.Entries[1].Text);
    }

    [Fact]
    public void Write_KeepsTheOriginalShape()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "a\r\nb\r\n", utf8Bom: true);

        var document = _repository.Read(path);
        _repository.Write(document);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3));
        Assert.Equal("a\r\nb\r\n", File.ReadAllText(path));
    }

    [Fact]
    public void Write_ReplacesTheContent()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "a\nb\nc\n");

        var document = _repository.Read(path);
        _repository.Write(document with { Entries = [document.Entries[0], document.Entries[2]] });

        Assert.Equal("a\nc\n", File.ReadAllText(path));
    }

    [Fact]
    public void Write_KeepsAMissingTrailingNewline()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "a\nb");

        var document = _repository.Read(path);
        _repository.Write(document);

        Assert.Equal("a\nb", File.ReadAllText(path));
    }

    [Fact]
    public void HasChangedSince_ReturnsFalseWhenTheFileIsUnchanged()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "a\n");

        var document = _repository.Read(path);

        Assert.False(_repository.HasChangedSince(path, document.ContentHash));
    }

    [Fact]
    public void HasChangedSince_ReturnsTrueWhenTheFileWasModified()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "a\n");

        var document = _repository.Read(path);
        File.WriteAllText(path, "a\nb\n");

        Assert.True(_repository.HasChangedSince(path, document.ContentHash));
    }

    [Fact]
    public void HasChangedSince_ReturnsTrueWhenTheFileDisappeared()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "a\n");

        var document = _repository.Read(path);
        File.Delete(path);

        Assert.True(_repository.HasChangedSince(path, document.ContentHash));
    }

    [Fact]
    public void Write_WhenTheDestinationIsLockedKeepsTheOriginalContent()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "original\n");

        var document = _repository.Read(path);

        using (var _ = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            Assert.ThrowsAny<IOException>(() =>
                _repository.Write(document with { Entries = [new HistoryEntry(0, "changed")] }));
        }

        Assert.Equal("original\n", File.ReadAllText(path));
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
    }

    [Fact]
    public void Write_DoesNotLeaveTemporaryFilesBehind()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("history.txt", "a\n");

        var document = _repository.Read(path);
        _repository.Write(document);

        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp"));
        Assert.Empty(Directory.GetFiles(temp.Path, ".*"));
    }
}
