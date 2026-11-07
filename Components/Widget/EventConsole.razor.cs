using Microsoft.AspNetCore.Components;
using Radzen;
using System.Text.Json;
using Microsoft.JSInterop;

namespace GrinVideoEncoder.Components.Widget;

public partial class EventConsole
{
	public const int MAX_LINES = 1000;
	private readonly List<Message> _messages = [];

	// No await inside the lock: the render loop reads the messages while services log
	private readonly Lock _messagesLock = new();

	[Parameter(CaptureUnmatchedValues = true)]
	public IDictionary<string, object> Attributes { get; set; } = null!;

	[Inject] private IJSRuntime Js { get; set; } = null!;

	[Parameter] public string Title { get; set; } = "Log";

	public async Task Clear()
	{
		lock (_messagesLock)
			_messages.Clear();

		await InvokeAsync(StateHasChanged);
	}

	public async Task LogAsync(string message, AlertStyle alertStyle = AlertStyle.Info)
	{
		lock (_messagesLock)
		{
			_messages.Add(new Message(message, null, alertStyle));
			if (_messages.Count > MAX_LINES)
				_messages.RemoveAt(0);
		}

		await InvokeAsync(StateHasChanged);
	}

	public async Task LogAsync(object value)
	{
		await LogAsync(JsonSerializer.Serialize(value));
	}

	// Helper method for the render loop to safely access messages
	protected IReadOnlyList<Message> GetMessages()
	{
		lock (_messagesLock)
			return [.. _messages];
	}

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!firstRender)
		{
			await Js.InvokeVoidAsync("eval", $"document.getElementById('event-console').scrollTop = document.getElementById('event-console').scrollHeight");
		}
	}

	public class Message(string text, DateTime? date = null, AlertStyle alertStyle = AlertStyle.Info)
	{
		public AlertStyle AlertStyle { get; set; } = alertStyle;
		public DateTime? Date { get; set; } = date;
		public string Text { get; set; } = text;
	}
}