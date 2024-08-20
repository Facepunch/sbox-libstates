using System;
using System.Collections.Generic;
using System.Linq;
using Editor;
using Editor.NodeEditor;
using Facepunch.ActionGraphs;

namespace Sandbox.States.Editor;

public sealed partial class TransitionItem : GraphicsItem, IContextMenuSource, IDeletable, IComparable<TransitionItem>
{
	public Transition? Transition { get; }
	public StateItem Source { get; }
	public StateItem? Target { get; set; }
	public Vector2 TargetPosition { get; set; }

	private TransitionLabel? _eventLabel;
	private TransitionLabel? _conditionLabel;
	private TransitionLabel? _actionLabel;

	public TransitionItem( Transition? transition, StateItem source, StateItem? target )
		: base( null )
	{
		Transition = transition;

		Source = source;
		Target = target;

		Selectable = true;
		HoverEvents = true;

		ZIndex = -10;

		if ( Transition is not null )
		{
			Source.PositionChanged += OnStatePositionChanged;
			Target!.PositionChanged += OnStatePositionChanged;
		}

		Layout();
	}

	protected override void OnDestroy()
	{
		if ( Transition is null ) return;

		Source.PositionChanged -= OnStatePositionChanged;
		Target!.PositionChanged -= OnStatePositionChanged;
	}

	private void OnStatePositionChanged()
	{
		Layout();
	}

	private (Vector2 Start, Vector2 End, Vector2 Tangent)? GetSceneStartEnd()
	{
		// TODO: transitions to self

		var (index, count) = Source.View.GetTransitionPosition( this );

		var sourceCenter = Source.Center;
		var targetCenter = Target?.Center ?? TargetPosition;

		if ( (targetCenter - sourceCenter).IsNearZeroLength )
		{
			return null;
		}

		var tangent = (targetCenter - sourceCenter).Normal;
		var normal = tangent.Perpendicular;

		if ( Target is null || Target.State.Id.CompareTo( Source.State.Id ) < 0 )
		{
			normal = -normal;
		}

		var maxWidth = Source.Radius * 2f;
		var usedWidth = count * 48f;

		var itemWidth = Math.Min( usedWidth, maxWidth ) / count;
		var offset = (index - count * 0.5f + 0.5f) * itemWidth;
		var curve = MathF.Sqrt( Source.Radius * Source.Radius - offset * offset );

		var start = sourceCenter + tangent * curve;
		var end = targetCenter - tangent * (Target is null ? 0f : curve);

		return (start + offset * normal, end + offset * normal, tangent);
	}

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

	private (bool Hovered, bool Selected) GetSelectedState()
	{
		var selected = Selected || Source.Selected || Transition is null;
		var hovered = Hovered || Source.Hovered;

		return (hovered, selected);
	}

	private Color GetPenColor( bool hovered, bool selected )
	{
		return selected
			? Color.Yellow: hovered
				? Color.White: Color.White.Darken( 0.125f );
	}

	protected override void OnPaint()
	{
		var start = new Vector2( 0f, Size.y * 0.5f );
		var end = new Vector2( Size.x, Size.y * 0.5f );
		var tangent = new Vector2( 1f, 0f );

		var normal = tangent.Perpendicular;

		var (hovered, selected) = GetSelectedState();
		var thickness = selected || hovered ? 6f : 4f;
		var pulse = MathF.Pow( Math.Max( 1f - (Transition?.LastTransitioned ?? float.PositiveInfinity), 0f ), 8f );
		var pulseScale = 1f + pulse * 3f;

		thickness *= pulseScale;

		var offset = thickness * 0.5f * normal;

		var color = GetPenColor( hovered, selected );
		
		var arrowEnd = Vector2.Lerp( end, start, pulse );
		var lineEnd = arrowEnd - tangent * 14f;

		Paint.ClearPen();
		Paint.SetBrushLinear( start, end, color.Darken( 0.667f / pulseScale ), color );
		Paint.DrawPolygon( start - offset, lineEnd - offset, lineEnd + offset, start + offset );

		var arrowScale = hovered || selected ? 1.25f : pulseScale;

		Paint.SetBrush( color );
		Paint.DrawArrow( arrowEnd - tangent * 16f * arrowScale, arrowEnd, 12f * arrowScale );
	}

	public void Layout()
	{
		PrepareGeometryChange();

		if ( GetSceneStartEnd() is not var (start, end, tangent) )
		{
			Size = 0f;
		}
		else
		{
			var diff = end - start;
			var length = diff.Length;

			Position = start - tangent.Perpendicular * 8f;
			Size = new Vector2( length, 16f );
			Rotation = MathF.Atan2( diff.y, diff.x ) * 180f / MathF.PI;
		}

		UpdateLabels();
		Update();
	}

	private void UpdateLabel( ref TransitionLabel? label, string? icon, string? text, Color color )
	{
		if ( string.IsNullOrEmpty( icon ) && string.IsNullOrEmpty( text ) )
		{
			ClearLabel( ref label );
			return;
		}

		label ??= new TransitionLabel( this );

		label.Icon = icon;
		label.Text = text;
		label.Color = color;
	}

	private void UpdateLabel( ref TransitionLabel? label, Delegate? action, string defaultIcon, Color color )
	{
		if ( !action.TryGetActionGraphImplementation( out var graph, out _ ) )
		{
			ClearLabel( ref label );
			return;
		}

		var icon = graph.Icon ?? defaultIcon;
		var title = graph.Title ?? "Unnamed";

		if ( graph.HasErrors() )
		{
			icon = "error";
			color = Color.Red.Darken( 0.05f ); // Pure red doesn't work??
		}

		UpdateLabel( ref label, icon, title, color );
	}

	private void ClearLabel( ref TransitionLabel? label )
	{
		label?.Destroy();
		label = null;
	}

	private void AlignLabels( bool source, params TransitionLabel?[] labels )
	{
		var count = labels.Count( x => x != null );
		if ( count == 0 ) return;

		const float margin = 8f;

		var maxWidth = (Width - margin * 2f) / count;

		foreach ( var label in labels )
		{
			if ( label is null ) continue;

			label.MaxWidth = maxWidth;
			label.Layout();
		}

		var totalWidth = labels.Sum( x => x?.Width ?? 0f );
		var origin = source
			? new Vector2( margin, Size.y * 0.5f - 24f )
			: new Vector2( Width - totalWidth - margin, Size.y * 0.5f );

		foreach ( var label in labels )
		{
			if ( label is null ) continue;

			label.Position = origin;
			origin.x += label.Width;

			label.Update();
		}
	}

	private void UpdateLabels()
	{
		var (hovered, selected) = GetSelectedState();
		var color = GetPenColor( hovered, selected );

		//
		// 1: Update label text
		//

		if ( Transition?.Delay is { } seconds )
		{
			UpdateLabel( ref _eventLabel, "timer", FormatDuration( seconds ), color );
		}
		else if ( Transition?.Message is { } message )
		{
			UpdateLabel( ref _eventLabel, "email", $"\"{message}\"", color );
		}
		else
		{
			ClearLabel( ref _eventLabel );
		}

		UpdateLabel( ref _conditionLabel, Transition?.Condition, "question_mark", color );
		UpdateLabel( ref _actionLabel, Transition?.OnTransition, "directions_run", color );

		//
		// 2: Reposition
		//

		AlignLabels( true, _eventLabel, _conditionLabel );
		AlignLabels( false, _actionLabel );
	}

	public void Delete()
	{
		Transition!.Remove();
		Destroy();

		SceneEditorSession.Active.Scene.EditLog( "Transition Removed", Transition.StateMachine );
	}

	private void EditGraph<T>( T action )
		where T : Delegate
	{
		if ( action.TryGetActionGraphImplementation( out var graph, out _ ) )
		{
			EditorEvent.Run( "actiongraph.inspect", graph );
		}
	}

	public void OnContextMenu( ContextMenuEvent e )
	{
		if ( Transition is null ) return;

		e.Accepted = true;
		Selected = true;

		var menu = new global::Editor.Menu();

		menu.AddHeading( "Transition" );

		if ( Transition.Condition is not null )
		{
			var subMenu = menu.AddMenu( "Condition", "question_mark" );

			subMenu.AddOption( "Edit", "edit", action: () => EditGraph( Transition.Condition ) );
			subMenu.AddOption( "Clear", "clear", action: () =>
			{
				Transition.Condition = null;
				Update();

				SceneEditorSession.Active.Scene.EditLog( "Transition Condition Removed", Transition.StateMachine );
			} );
		}
		else
		{
			menu.AddOption( "Add Condition", "question_mark", action: () =>
			{
				Transition.Condition = Source.View.CreateGraph<Func<bool>>( "Condition" );
				EditGraph( Transition.Condition );
				Update();

				SceneEditorSession.Active.Scene.EditLog( "Transition Condition Added", Transition.StateMachine );
			} );
		}

		if ( Transition.Delay is { } currentDelay )
		{
			var subMenu = menu.AddMenu( "Delay", "timer" );
			subMenu.AddLineEdit( "Seconds", value: currentDelay.ToString( "R" ), autoFocus: true, onSubmit:
				delayStr =>
				{
					if ( !float.TryParse( delayStr, out var seconds ) || seconds < 0f )
					{
						return;
					}

					Transition.Delay = seconds;
					Transition.Message = null;
					Update();

					SceneEditorSession.Active.Scene.EditLog( "Transition Delay Changed", Transition.StateMachine );
				} );
			subMenu.AddOption( "Clear", "timer_off", action: () =>
			{
				Transition.Delay = null;
				Update();

				SceneEditorSession.Active.Scene.EditLog( "Transition Delay Removed", Transition.StateMachine );
			} );
		}
		else if ( Transition.Message is { } currentMessage )
		{
			var subMenu = menu.AddMenu( "Message", "email" );
			subMenu.AddLineEdit( "Value", value: currentMessage, autoFocus: true, onSubmit:
				message =>
				{
					if ( string.IsNullOrEmpty( message ) )
					{
						return;
					}

					Transition.Message = message;
					Transition.Delay = null;
					Update();

					SceneEditorSession.Active.Scene.EditLog( "Transition Message Changed", Transition.StateMachine );
				} );
			subMenu.AddOption( "Clear", "unsubscribe", action: () =>
			{
				Transition.Message = null;
				Update();

				SceneEditorSession.Active.Scene.EditLog( "Transition Message Removed", Transition.StateMachine );
			} );
		}
		else
		{
			menu.AddMenu( "Add Delay", "timer" ).AddLineEdit( "Seconds", value: "1", autoFocus: true, onSubmit:
				delayStr =>
				{
					if ( !float.TryParse( delayStr, out var seconds ) || seconds < 0f )
					{
						return;
					}

					Transition.Delay = seconds;
					Update();

					SceneEditorSession.Active.Scene.EditLog( "Transition Delay Added", Transition.StateMachine );
				} );
			menu.AddMenu( "Add Message", "email" ).AddLineEdit( "Value", value: "run", autoFocus: true, onSubmit:
				message =>
				{
					if ( string.IsNullOrEmpty( message ) )
					{
						return;
					}

					Transition.Message = message;
					Update();

					SceneEditorSession.Active.Scene.EditLog( "Transition Message Added", Transition.StateMachine );
				} );
		}

		menu.AddSeparator();

		if ( Transition.OnTransition is not null )
		{
			var subMenu = menu.AddMenu( "Action", "directions_run" );

			subMenu.AddOption( "Edit", "edit", action: () =>
			{
				EditGraph( Transition.OnTransition );
			} );
			subMenu.AddOption( "Clear", "clear", action: () =>
			{
				Transition.OnTransition = null;
				Update();

				SceneEditorSession.Active.Scene.EditLog( "Transition Action Removed", Transition.StateMachine );
			} );
		}
		else
		{
			menu.AddOption( "Add Action", "directions_run", action: () =>
			{
				Transition.OnTransition = Source.View.CreateGraph<Action>( "Action" );
				EditGraph( Transition.OnTransition );
				Update();

				SceneEditorSession.Active.Scene.EditLog( "Transition Action Added", Transition.StateMachine );
			} );
		}

		menu.AddSeparator();

		menu.AddOption( "Delete", "delete", action: Delete );

		menu.OpenAtCursor( true );
	}

	protected override void OnHoverEnter( GraphicsHoverEvent e )
	{
		base.OnHoverEnter( e );
		ForceUpdate();
	}

	protected override void OnHoverLeave( GraphicsHoverEvent e )
	{
		base.OnHoverLeave( e );
		ForceUpdate();
	}

	protected override void OnSelectionChanged()
	{
		base.OnSelectionChanged();
		ForceUpdate();
	}
	public void Frame()
	{
		if ( Transition is null || Transition.LastTransitioned > 1f )
		{
			return;
		}

		Update();
	}

	public void ForceUpdate()
	{
		Update();
		UpdateLabels();
	}

	public int CompareTo( TransitionItem? other )
	{
		return Source.State.Id.CompareTo( other?.Source.State.Id ?? -1 );
	}
}
