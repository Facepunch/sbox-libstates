using Editor.NodeEditor;
using Editor;
using System;

namespace Sandbox.States.Editor;

public record TransitionEvent( TransitionItem Item ) : ILabelSource
{
	public Transition Transition => Item.Transition!;

	public string Title => "Event";

	public string? Description => Transition.Delay is not null
		? "This transition is taken after a delay."
		: "This transition is taken after the state machine receives this message.";

	public string? Icon => Transition.Delay is not null ? "timer" : Transition.Message is not null ? "email" : null;
	public string? Text => Transition.Delay is { } seconds ? FormatDuration( seconds ) : Transition.Message is { } message ? $"\"{message}\"" : null;

	public bool IsValid => Transition.Delay is not null || Transition.Message is not null;

	private string FormatDuration( float seconds )
	{
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
			menu.AddMenu( "Add Delay Trigger", "timer" ).AddLineEdit( "Seconds", value: "1", autoFocus: true, onSubmit:
				delayStr =>
				{
					if ( !float.TryParse( delayStr, out var seconds ) || seconds < 0f )
					{
						return;
					}

					Transition.Delay = seconds;
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

		if ( Transition.Delay is { } currentDelay )
		{
			menu.AddHeading( "Delay Trigger" );
			menu.AddLineEdit( "Seconds", value: currentDelay.ToString( "R" ), autoFocus: true, onSubmit:
				delayStr =>
				{
					if ( !float.TryParse( delayStr, out var seconds ) || seconds < 0f )
					{
						return;
					}

					Transition.Delay = seconds;
					Transition.Message = null;
					Item.ForceUpdate();

					SceneEditorSession.Active.Scene.EditLog( "Transition Delay Changed", Transition.StateMachine );
				} );
			menu.AddOption( "Clear", "clear", action: () =>
			{
				Transition.Delay = null;
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
					Transition.Delay = null;
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
		Transition.Delay = null;
	}

	public void DoubleClick()
	{
		var menu = new global::Editor.Menu { DeleteOnClose = true };

		BuildContextMenu( menu );

		menu.OpenAtCursor( true );
	}
}
