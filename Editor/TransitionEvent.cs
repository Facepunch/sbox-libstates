using Editor.NodeEditor;
using Editor;
using System;

namespace Sandbox.States.Editor;

public record TransitionEvent( TransitionItem Item ) : ILabelSource
{
	public Transition Transition => Item.Transition!;

	public string Title => "Event";

	public string? Description => Transition.HasDelay
		? FormatDelayLong( Transition.MinDelay, Transition.MaxDelay, Transition.Condition is not null )
		: "This transition is taken after the state machine receives this message.";

	public string? Icon => Transition.HasDelay
		? Transition.MaxDelay is null ? "alarm" : "hourglass_top"
		: Transition.Message is not null ? "email" : null;
	public string? Text => Transition.HasDelay
		? FormatDelayShort( Transition.MinDelay, Transition.MaxDelay )
		: Transition.Message is { } message ? $"\"{message}\"" : null;

	public bool IsValid => Transition.HasDelay || Transition.Message is not null;

	private static string FormatDelayShort( float? min, float? max )
	{
		if ( min is null && max is null )
		{
			return "N/A";
		}

		if ( max is null )
		{
			return $"={FormatDuration( min ?? 0f )}";
		}

		return $"{FormatDuration( min ?? 0f )} - {FormatDuration( max.Value )}";
	}

	private static string FormatDelayLong( float? min, float? max, bool hasCondition )
	{
		if ( min is null && max is null )
		{
			return "Can be taken at any time.";
		}

		var actualMin = min ?? 0f;
		var actualMax = max ?? actualMin;

		return actualMin >= actualMax
			? $"Only taken after exactly <b>{FormatDuration( actualMin )}</b>."
			: hasCondition
				? $"Taken as soon as a condition is met, but only between <b>{FormatDuration( actualMin )}</b> and <b>{FormatDuration( actualMax )}</b>."
				: $"Taken at a random time between <b>{FormatDuration( actualMin )}</b> and <b>{FormatDuration( actualMax )}</b>.";
	}

	private static string FormatDuration( float seconds )
	{
		if ( seconds < 0.001f )
		{
			return "0s";
		}

		var timeSpan = TimeSpan.FromSeconds( seconds );
		var result = "";

		if ( timeSpan.Hours > 0 )
		{
			result += $"{timeSpan.Hours}h";
		}

		if ( timeSpan.Minutes > 0 )
		{
			result += $"{timeSpan.Minutes}m";
		}

		if ( timeSpan.Seconds > 0 )
		{
			result += $"{timeSpan.Seconds}s";
		}

		if ( timeSpan.Milliseconds > 0 )
		{
			result += $"{timeSpan.Milliseconds}ms";
		}

		return result;
	}

	public void BuildContextMenu( global::Editor.Menu menu )
	{
		if ( !IsValid )
		{
			menu.AddMenu( "Add Trigger Time", "alarm" ).AddLineEdit( "Seconds", value: "1", autoFocus: true, onSubmit:
				delayStr =>
				{
					if ( !float.TryParse( delayStr, out var seconds ) || seconds < 0f )
					{
						return;
					}

					Transition.MinDelay = seconds;
					Transition.MaxDelay = null;
					Item.ForceUpdate();

					SceneEditorSession.Active.Scene.EditLog( "Transition Delay Added", Transition.StateMachine );
				} );
			menu.AddMenu( "Add Time Window", "hourglass_top" ).AddLineEdit( "Max Seconds", value: "1", autoFocus: true, onSubmit:
				delayStr =>
				{
					if ( !float.TryParse( delayStr, out var seconds ) || seconds < 0f )
					{
						return;
					}

					Transition.MinDelay = 0f;
					Transition.MaxDelay = seconds;
					Item.ForceUpdate();

					SceneEditorSession.Active.Scene.EditLog( "Transition Delay Added", Transition.StateMachine );
				} );
			menu.AddMenu( "Add Message Trigger", "email" ).AddLineEdit( "Value", value: "run", autoFocus: true, onSubmit:
				message =>
				{
					if ( string.IsNullOrEmpty( message ) )
					{
						return;
					}

					Transition.Message = message;
					Item.ForceUpdate();

					SceneEditorSession.Active.Scene.EditLog( "Transition Message Added", Transition.StateMachine );
				} );

			return;
		}

		if ( Transition.HasDelay )
		{
			var minDelay = Transition.MinDelay ?? 0f;
			var maxDelay = Transition.MaxDelay ?? minDelay;

			if ( Transition.MaxDelay is null )
			{
				menu.AddHeading( "Trigger Time" );
				menu.AddLineEdit( "Seconds", value: minDelay.ToString( "R" ), autoFocus: true, onSubmit:
					delayStr =>
					{
						if ( !float.TryParse( delayStr, out var seconds ) || seconds < 0f )
						{
							return;
						}

						Transition.MinDelay = seconds;
						Transition.MaxDelay = null;
						Item.ForceUpdate();

						SceneEditorSession.Active.Scene.EditLog( "Transition Delay Changed", Transition.StateMachine );
					} );
			}
			else
			{
				menu.AddHeading( "Time Window" );
				menu.AddLineEdit( "Min Seconds", value: minDelay.ToString( "R" ), autoFocus: false, onSubmit:
					delayStr =>
					{
						if ( !float.TryParse( delayStr, out var seconds ) || seconds < 0f )
						{
							return;
						}

						Transition.MinDelay = seconds;
						Item.ForceUpdate();

						SceneEditorSession.Active.Scene.EditLog( "Transition Delay Changed", Transition.StateMachine );
					} );
				menu.AddLineEdit( "Max Seconds", value: maxDelay.ToString( "R" ), autoFocus: false, onSubmit:
					delayStr =>
					{
						if ( !float.TryParse( delayStr, out var seconds ) || seconds < 0f )
						{
							return;
						}

						Transition.MaxDelay = seconds;
						Item.ForceUpdate();

						SceneEditorSession.Active.Scene.EditLog( "Transition Delay Changed", Transition.StateMachine );
					} );
			}

			menu.AddOption( "Clear", "clear", action: () =>
			{
				Transition.MinDelay = null;
				Transition.MaxDelay = null;
				Item.ForceUpdate();

				SceneEditorSession.Active.Scene.EditLog( "Transition Delay Removed", Transition.StateMachine );
			} );
		}
		else if ( Transition.Message is { } currentMessage )
		{
			menu.AddHeading( "Message Trigger" );
			menu.AddLineEdit( "Value", value: currentMessage, autoFocus: true, onSubmit:
				message =>
				{
					if ( string.IsNullOrEmpty( message ) )
					{
						return;
					}

					Transition.Message = message;
					Item.ForceUpdate();

					SceneEditorSession.Active.Scene.EditLog( "Transition Message Changed", Transition.StateMachine );
				} );
			menu.AddOption( "Clear", "clear", action: () =>
			{
				Transition.Message = null;
				Item.ForceUpdate();

				SceneEditorSession.Active.Scene.EditLog( "Transition Message Removed", Transition.StateMachine );
			} );
		}
	}

	public void Delete()
	{
		Transition.Message = null;
		Transition.MinDelay = null;
		Transition.MaxDelay = null;
	}

	public void DoubleClick()
	{
		var menu = new global::Editor.Menu { DeleteOnClose = true };

		BuildContextMenu( menu );

		menu.OpenAtCursor( true );
	}
}
