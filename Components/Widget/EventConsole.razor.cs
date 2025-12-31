using Microsoft.AspNetCore.Components;
using Radzen;
using System.Text.Json;
using Microsoft.JSInterop;

namespace GrinVideoEncoder.Components.Widget;

public partial class EventConsole
{
	public const int MAX_LINES = 1000;
	class Message(string text, DateTime? date = null, AlertStyle alertStyle = AlertStyle.Info)
	{
		public DateTime? Date { get; set; } = date;	
		public string Text { get; set; } = text;
		public AlertStyle AlertStyle { get; set; } = alertStyle;
	}

	[Parameter(CaptureUnmatchedValues = true)]
	public IDictionary<string, object> Attributes { get; set; } = null!;

	readonly List<Message> _messages = [];

	[Inject] IJSRuntime Js { get; set; } = null!;

	protected override async Task OnAfterRenderAsync(bool firstRender)
	{
		if (!firstRender)
		{
			await Js.InvokeVoidAsync("eval", $"document.getElementById('event-console').scrollTop = document.getElementById('event-console').scrollHeight");
		}
	}

	void OnClearClick()
	{
		Clear();
	}

	public void Clear()
	{
		_messages.Clear();

		InvokeAsync(StateHasChanged);
	}

	public void Log(string message, AlertStyle alertStyle = AlertStyle.Info)
	{
		_messages.Add(new Message(message, null, alertStyle));
		if (_messages.Count > MAX_LINES)
			_messages.RemoveAt(0);

		InvokeAsync(StateHasChanged);
	}

	public void Log(object value)
	{
		Log(JsonSerializer.Serialize(value));
	}
}