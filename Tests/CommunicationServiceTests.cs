using GrinVideoEncoder.Services;

namespace Tests;

public class CommunicationServiceTests
{
	[Fact]
	public void DefaultValues_AreCorrect()
	{
		var service = new CommunicationService();

		Assert.False(service.PreventSleep);
		Assert.False(service.AskTreatFiles);
		Assert.False(service.ScheduledShutdownEnabled);
		Assert.False(service.AskClose);
		Assert.Equal(DateTime.MaxValue, service.ScheduledShutdownTime);
		Assert.NotNull(service.VideoProcessToken);
		Assert.NotNull(service.Status);
	}

	[Fact]
	public void PreventSleep_CanBeToggled()
	{
		var service = new CommunicationService();

		service.PreventSleep = true;
		Assert.True(service.PreventSleep);

		service.PreventSleep = false;
		Assert.False(service.PreventSleep);
	}

	[Fact]
	public void AskTreatFiles_CanBeToggled()
	{
		var service = new CommunicationService();

		service.AskTreatFiles = true;
		Assert.True(service.AskTreatFiles);

		service.AskTreatFiles = false;
		Assert.False(service.AskTreatFiles);
	}

	[Fact]
	public void ScheduledShutdownEnabled_CanBeToggled()
	{
		var service = new CommunicationService();

		service.ScheduledShutdownEnabled = true;
		Assert.True(service.ScheduledShutdownEnabled);

		service.ScheduledShutdownEnabled = false;
		Assert.False(service.ScheduledShutdownEnabled);
	}

	[Fact]
	public void ScheduledShutdownTime_CanBeSet()
	{
		var service = new CommunicationService();
		var expected = new DateTime(2025, 12, 31, 23, 59, 59);

		service.ScheduledShutdownTime = expected;

		Assert.Equal(expected, service.ScheduledShutdownTime);
	}

	[Fact]
	public void VideoProcessToken_IsNotCancelled_ByDefault()
	{
		var service = new CommunicationService();

		Assert.False(service.VideoProcessToken.IsCancellationRequested);
	}

	[Fact]
	public void VideoProcessToken_CanBeCancelled()
	{
		var service = new CommunicationService();

		service.VideoProcessToken.Cancel();

		Assert.True(service.VideoProcessToken.IsCancellationRequested);
	}

	[Fact]
	public void Status_HasDefaultValues()
	{
		var service = new CommunicationService();

		Assert.Equal("...", service.Status.Filename.Value);
		Assert.False(service.Status.IsRunning.Value);
		Assert.Equal("...", service.Status.Status.Value);
		Assert.Null(service.Status.EncodingPercent.Value);
	}

	[Fact]
	public void VideoProcessToken_CanBeReplaced()
	{
		var service = new CommunicationService();
		service.VideoProcessToken.Cancel();

		service.VideoProcessToken = new CancellationTokenSource();

		Assert.False(service.VideoProcessToken.IsCancellationRequested);
	}

	[Fact]
	public void AskClose_CanBeToggled()
	{
		var service = new CommunicationService();

		service.AskClose = true;
		Assert.True(service.AskClose);

		service.AskClose = false;
		Assert.False(service.AskClose);
	}

	[Fact]
	public void ScheduledShutdownTime_DefaultIsMaxValue()
	{
		var service = new CommunicationService();

		Assert.Equal(DateTime.MaxValue, service.ScheduledShutdownTime);
	}

	[Fact]
	public void Status_UpdatesAreReflected()
	{
		var service = new CommunicationService();

		service.Status.IsRunning.OnNext(true);
		service.Status.Filename.OnNext("encoding.mp4");
		service.Status.Status.OnNext("Encoding...");
		service.Status.EncodingPercent.OnNext(42.5);

		Assert.True(service.Status.IsRunning.Value);
		Assert.Equal("encoding.mp4", service.Status.Filename.Value);
		Assert.Equal("Encoding...", service.Status.Status.Value);
		Assert.Equal(42.5, service.Status.EncodingPercent.Value);
	}
}
