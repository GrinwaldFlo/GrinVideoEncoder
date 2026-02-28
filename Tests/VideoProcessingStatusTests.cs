using GrinVideoEncoder.Data;

namespace Tests;

public class VideoProcessingStatusTests
{
	[Fact]
	public void Filename_DefaultValue_IsEllipsis()
	{
		var status = new VideoProcessingStatus();

		Assert.Equal("...", status.Filename.Value);
	}

	[Fact]
	public void Filename_OnNext_UpdatesValue()
	{
		var status = new VideoProcessingStatus();

		status.Filename.OnNext("video.mp4");

		Assert.Equal("video.mp4", status.Filename.Value);
	}

	[Fact]
	public void IsRunning_DefaultValue_IsFalse()
	{
		var status = new VideoProcessingStatus();

		Assert.False(status.IsRunning.Value);
	}

	[Fact]
	public void IsRunning_OnNext_UpdatesValue()
	{
		var status = new VideoProcessingStatus();

		status.IsRunning.OnNext(true);

		Assert.True(status.IsRunning.Value);
	}

	[Fact]
	public void IsRunning_MultipleUpdates_ReflectsLatest()
	{
		var status = new VideoProcessingStatus();

		status.IsRunning.OnNext(true);
		status.IsRunning.OnNext(false);
		status.IsRunning.OnNext(true);

		Assert.True(status.IsRunning.Value);
	}

	[Fact]
	public void Status_DefaultValue_IsEllipsis()
	{
		var status = new VideoProcessingStatus();

		Assert.Equal("...", status.Status.Value);
	}

	[Fact]
	public void Status_OnNext_UpdatesValue()
	{
		var status = new VideoProcessingStatus();

		status.Status.OnNext("Encoding...");

		Assert.Equal("Encoding...", status.Status.Value);
	}

	[Fact]
	public void EncodingPercent_DefaultValue_IsNull()
	{
		var status = new VideoProcessingStatus();

		Assert.Null(status.EncodingPercent.Value);
	}

	[Fact]
	public void EncodingPercent_OnNext_UpdatesValue()
	{
		var status = new VideoProcessingStatus();

		status.EncodingPercent.OnNext(55.5);

		Assert.Equal(55.5, status.EncodingPercent.Value);
	}

	[Fact]
	public void EncodingPercent_CanBeResetToNull()
	{
		var status = new VideoProcessingStatus();

		status.EncodingPercent.OnNext(75.0);
		status.EncodingPercent.OnNext(null);

		Assert.Null(status.EncodingPercent.Value);
	}

	[Fact]
	public void Filename_Subscriber_ReceivesUpdates()
	{
		var status = new VideoProcessingStatus();
		var received = new List<string>();

		using var sub = status.Filename.Subscribe(v => received.Add(v));
		status.Filename.OnNext("file1.mp4");
		status.Filename.OnNext("file2.mkv");

		// BehaviorSubject emits current value on subscribe, then the two updates
		Assert.Equal(3, received.Count);
		Assert.Equal("...", received[0]);
		Assert.Equal("file1.mp4", received[1]);
		Assert.Equal("file2.mkv", received[2]);
	}

	[Fact]
	public void EncodingPercent_Subscriber_ReceivesProgressUpdates()
	{
		var status = new VideoProcessingStatus();
		var received = new List<double?>();

		using var sub = status.EncodingPercent.Subscribe(v => received.Add(v));
		status.EncodingPercent.OnNext(0.0);
		status.EncodingPercent.OnNext(50.0);
		status.EncodingPercent.OnNext(100.0);

		Assert.Equal(4, received.Count);
		Assert.Null(received[0]);
		Assert.Equal(0.0, received[1]);
		Assert.Equal(50.0, received[2]);
		Assert.Equal(100.0, received[3]);
	}

	[Fact]
	public void EncodingPercent_ZeroPercent_IsNotNull()
	{
		var status = new VideoProcessingStatus();

		status.EncodingPercent.OnNext(0.0);

		Assert.NotNull(status.EncodingPercent.Value);
		Assert.Equal(0.0, status.EncodingPercent.Value);
	}
}
