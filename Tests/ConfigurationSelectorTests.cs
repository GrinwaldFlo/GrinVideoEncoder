using GrinVideoEncoder.Startup;

namespace Tests;

public class ConfigurationSelectorTests
{
	[Theory]
	[InlineData(new[] { "--config", "home" }, "home")]
	[InlineData(new[] { "--urls", "x", "--config", "work" }, "work")]
	[InlineData(new[] { "--config" }, null)]
	[InlineData(new string[0], null)]
	[InlineData(new[] { "--CONFIG", "home" }, null)]
	public void GetConfigFromArgs_ReadsTheValueAfterTheFlag(string[] args, string? expected)
	{
		Assert.Equal(expected, ConfigurationSelector.GetConfigFromArgs(args));
	}

	/// <summary>
	/// The folder must be opened with its own name: on Linux, "Home" does not open the folder "home".
	/// </summary>
	[Fact]
	public void FindExistingConfig_DifferentCase_ReturnsTheNameOfTheFolder()
	{
		Assert.Equal("home", ConfigurationSelector.FindExistingConfig("Home", ["work", "home"]));
	}

	[Fact]
	public void FindExistingConfig_ExactName_PrefersIt()
	{
		Assert.Equal("Home", ConfigurationSelector.FindExistingConfig("Home", ["home", "Home"]));
	}

	[Fact]
	public void FindExistingConfig_Unknown_ReturnsNull()
	{
		Assert.Null(ConfigurationSelector.FindExistingConfig("other", ["home"]));
	}
}
