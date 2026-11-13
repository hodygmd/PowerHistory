namespace PowerHistory.Infrastructure.Tests;

using PowerHistory.Infrastructure.Configuration;

public class OptionsStoreTests
{
    [Fact]
    public void Load_ReturnsTheDefaultsWhenTheFileDoesNotExist()
    {
        using var temp = new TempDirectory();
        var store = new OptionsStore(Path.Combine(temp.Path, "config.json"));

        var options = store.Load();

        Assert.Equal(0, options.PageSize);
        Assert.True(options.RequireConfirmation);
        Assert.Null(options.DefaultHistoryPath);
    }

    [Fact]
    public void SaveAndLoad_RoundTripsTheOptions()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "config.json");
        var store = new OptionsStore(path);

        store.Save(new PowerHistoryOptions
        {
            DefaultHistoryPath = "C:\\history.txt",
            BackupDirectory = "C:\\backups",
            PageSize = 50,
            RequireConfirmation = false,
            CaseSensitiveSearch = true,
        });

        var options = store.Load();

        Assert.Equal("C:\\history.txt", options.DefaultHistoryPath);
        Assert.Equal("C:\\backups", options.BackupDirectory);
        Assert.Equal(50, options.PageSize);
        Assert.False(options.RequireConfirmation);
        Assert.True(options.CaseSensitiveSearch);
    }

    [Fact]
    public void Load_ReturnsTheDefaultsWhenTheFileIsCorrupted()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "config.json");
        File.WriteAllText(path, "{ not json");
        var store = new OptionsStore(path);

        var options = store.Load();

        Assert.Equal(0, options.PageSize);
    }
}
