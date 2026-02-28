using GrinVideoEncoder;
using GrinVideoEncoder.Services;

namespace Tests;

public class MaintenanceServiceTests : IDisposable
{
	private readonly string _tempDir;

	public MaintenanceServiceTests()
	{
		_tempDir = Path.Combine(Path.GetTempPath(), "GrinTests_Maintenance_" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(_tempDir);
	}

	public void Dispose()
	{
		if (Directory.Exists(_tempDir))
			Directory.Delete(_tempDir, true);
		GC.SuppressFinalize(this);
	}

	[Fact]
	public void ClearFolder_RemovesAllFiles()
	{
		string subDir = Path.Combine(_tempDir, "cleartest");
		Directory.CreateDirectory(subDir);
		File.WriteAllText(Path.Combine(subDir, "file1.txt"), "a");
		File.WriteAllText(Path.Combine(subDir, "file2.txt"), "b");

		MaintenanceService.ClearFolder(subDir);

		Assert.Empty(Directory.GetFiles(subDir));
	}

	[Fact]
	public void ClearFolder_RemovesSubdirectories()
	{
		string subDir = Path.Combine(_tempDir, "cleartest2");
		string nested = Path.Combine(subDir, "nested");
		Directory.CreateDirectory(nested);
		File.WriteAllText(Path.Combine(nested, "deep.txt"), "content");

		MaintenanceService.ClearFolder(subDir);

		Assert.Empty(Directory.GetDirectories(subDir));
	}

	[Fact]
	public void ClearFolder_RemovesNestedFiles()
	{
		string subDir = Path.Combine(_tempDir, "cleartest3");
		string nested = Path.Combine(subDir, "a", "b");
		Directory.CreateDirectory(nested);
		File.WriteAllText(Path.Combine(nested, "deep.txt"), "x");
		File.WriteAllText(Path.Combine(subDir, "top.txt"), "y");

		MaintenanceService.ClearFolder(subDir);

		Assert.Empty(Directory.GetFiles(subDir, "*", SearchOption.AllDirectories));
		Assert.Empty(Directory.GetDirectories(subDir));
	}

	[Fact]
	public void ClearFolder_NonExistentFolder_DoesNotThrow()
	{
		string missing = Path.Combine(_tempDir, "doesnotexist");

		var ex = Record.Exception(() => MaintenanceService.ClearFolder(missing));

		Assert.Null(ex);
	}

	[Fact]
	public void ClearFolder_EmptyFolder_DoesNotThrow()
	{
		string emptyDir = Path.Combine(_tempDir, "emptydir");
		Directory.CreateDirectory(emptyDir);

		var ex = Record.Exception(() => MaintenanceService.ClearFolder(emptyDir));

		Assert.Null(ex);
		Assert.True(Directory.Exists(emptyDir));
	}

	[Fact]
	public void GetFolderInfos_ReturnsCorrectFolderCount()
	{
		var settings = new AppSettings { ConfigName = "MaintenanceTest" };
		settings.InitializePaths();
		try
		{
			Directory.CreateDirectory(settings.ProcessingPath);
			Directory.CreateDirectory(settings.TrashPath);
			Directory.CreateDirectory(settings.LogPath);
			Directory.CreateDirectory(settings.FailedPath);
			Directory.CreateDirectory(settings.TempPath);

			var service = new MaintenanceService(settings);

			var infos = service.GetFolderInfos();

			Assert.Equal(5, infos.Count);
		}
		finally
		{
			if (Directory.Exists(settings.WorkPath))
				Directory.Delete(settings.WorkPath, true);
		}
	}

	[Fact]
	public void GetFolderInfos_IncludesExpectedFolderNames()
	{
		var settings = new AppSettings { ConfigName = "MaintenanceTest2" };
		settings.InitializePaths();
		try
		{
			Directory.CreateDirectory(settings.ProcessingPath);
			Directory.CreateDirectory(settings.TrashPath);
			Directory.CreateDirectory(settings.LogPath);
			Directory.CreateDirectory(settings.FailedPath);
			Directory.CreateDirectory(settings.TempPath);

			var service = new MaintenanceService(settings);

			var infos = service.GetFolderInfos();
			var names = infos.Select(i => i.Name).ToList();

			Assert.Contains("Processing", names);
			Assert.Contains("Trash", names);
			Assert.Contains("Log", names);
			Assert.Contains("Failed", names);
			Assert.Contains("Temp", names);
		}
		finally
		{
			if (Directory.Exists(settings.WorkPath))
				Directory.Delete(settings.WorkPath, true);
		}
	}

	[Fact]
	public void GetFolderInfos_CountsFilesCorrectly()
	{
		var settings = new AppSettings { ConfigName = "MaintenanceTest3" };
		settings.InitializePaths();
		try
		{
			Directory.CreateDirectory(settings.ProcessingPath);
			Directory.CreateDirectory(settings.TrashPath);
			Directory.CreateDirectory(settings.LogPath);
			Directory.CreateDirectory(settings.FailedPath);
			Directory.CreateDirectory(settings.TempPath);

			File.WriteAllText(Path.Combine(settings.TempPath, "file1.tmp"), "data1");
			File.WriteAllText(Path.Combine(settings.TempPath, "file2.tmp"), "data2data2");

			var service = new MaintenanceService(settings);
			var infos = service.GetFolderInfos();
			var tempInfo = infos.First(i => i.Name == "Temp");

			Assert.Equal(2, tempInfo.FileCount);
			Assert.True(tempInfo.SizeBytes > 0);
		}
		finally
		{
			if (Directory.Exists(settings.WorkPath))
				Directory.Delete(settings.WorkPath, true);
		}
	}

	[Fact]
	public void GetFolderInfos_EmptyFolder_ReturnsZeroCounts()
	{
		var settings = new AppSettings { ConfigName = "MaintenanceTest4" };
		settings.InitializePaths();
		try
		{
			Directory.CreateDirectory(settings.ProcessingPath);
			Directory.CreateDirectory(settings.TrashPath);
			Directory.CreateDirectory(settings.LogPath);
			Directory.CreateDirectory(settings.FailedPath);
			Directory.CreateDirectory(settings.TempPath);

			var service = new MaintenanceService(settings);
			var infos = service.GetFolderInfos();

			foreach (var info in infos)
			{
				Assert.Equal(0, info.FileCount);
				Assert.Equal(0, info.SizeBytes);
			}
		}
		finally
		{
			if (Directory.Exists(settings.WorkPath))
				Directory.Delete(settings.WorkPath, true);
		}
	}
}
