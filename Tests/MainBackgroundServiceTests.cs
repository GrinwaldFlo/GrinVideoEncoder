using GrinVideoEncoder.Services;

namespace Tests;

public class MainBackgroundServiceTests : IDisposable
{
	private readonly string _trash;

	public MainBackgroundServiceTests()
	{
		_trash = Path.Combine(Path.GetTempPath(), "GrinTests_Trash_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_trash);
	}

	public void Dispose()
	{
		if (Directory.Exists(_trash))
			Directory.Delete(_trash, true);
		GC.SuppressFinalize(this);
	}

	/// <param name="ageMinutes">How long ago the file was written.</param>
	private string CreateFile(string relativePath, int ageMinutes)
	{
		string path = Path.Combine(_trash, relativePath);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, "x");
		File.SetLastWriteTime(path, DateTime.Now.AddMinutes(-ageMinutes));
		return path;
	}

	[Fact]
	public void CleanTrash_KeepsTheNewestFiles()
	{
		string newest = CreateFile("a.mp4", ageMinutes: 1);
		string middle = CreateFile(Path.Combine("sub", "b.mp4"), ageMinutes: 2);
		string oldest = CreateFile(Path.Combine("sub", "c.mp4"), ageMinutes: 3);

		MainBackgroundService.CleanTrash(_trash, keepCount: 2);

		Assert.True(File.Exists(newest));
		Assert.True(File.Exists(middle));
		Assert.False(File.Exists(oldest));
	}

	[Fact]
	public void CleanTrash_RemovesEmptyFolders_KeepsTheTrashItself()
	{
		CreateFile(Path.Combine("a", "b", "old.mp4"), ageMinutes: 10);
		string kept = CreateFile(Path.Combine("c", "new.mp4"), ageMinutes: 1);

		MainBackgroundService.CleanTrash(_trash, keepCount: 1);

		Assert.True(Directory.Exists(_trash));
		Assert.False(Directory.Exists(Path.Combine(_trash, "a")));
		Assert.True(File.Exists(kept));
	}

	[Fact]
	public void CleanTrash_FilesWithoutExtension_AreCounted()
	{
		string newest = CreateFile("new", ageMinutes: 1);
		string oldest = CreateFile("old", ageMinutes: 2);

		MainBackgroundService.CleanTrash(_trash, keepCount: 1);

		Assert.True(File.Exists(newest));
		Assert.False(File.Exists(oldest));
	}

	[Fact]
	public void CleanTrash_MissingFolder_DoesNothing()
	{
		MainBackgroundService.CleanTrash(Path.Combine(_trash, "missing"), keepCount: 1);
	}
}
