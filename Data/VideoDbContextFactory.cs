using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GrinVideoEncoder.Data;

/// <summary>
/// Design-time factory used by EF Core tools (dotnet ef migrations add, etc.).
/// </summary>
public class VideoDbContextFactory : IDesignTimeDbContextFactory<VideoDbContext>
{
	public VideoDbContext CreateDbContext(string[] args)
	{
		var optionsBuilder = new DbContextOptionsBuilder<VideoDbContext>();
		optionsBuilder.UseSqlite("Data Source=design_time.db");
		return new VideoDbContext(optionsBuilder.Options);
	}
}
