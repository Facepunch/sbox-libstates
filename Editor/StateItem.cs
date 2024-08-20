using System;
using System.Linq;
using Editor;
using Editor.NodeEditor;
using Facepunch.ActionGraphs;

namespace Sandbox.States.Editor;

public sealed class StateItem : GraphicsItem, IContextMenuSource, IDeletable
{
	public static Color PrimaryColor { get; } = Color.Parse( "#5C79DB" )!.Value;
	public static Color InitialColor { get; } = Color.Parse( "#BCA5DB" )!.Value;

	public StateMachineView View { get; }
	public State State { get; }

	public float Radius => 64f;

	public event Action? PositionChanged;

	private bool _rightMousePressed;

	private int _lastHash;

	public StateItem( StateMachineView view, State state )
	{
		View = view;
		State = state;

		Size = new Vector2( Radius * 2f, Radius * 2f );
		Position = state.EditorPosition;

		Movable = true;
		Selectable = true;
		HoverEvents = true;
	}

	public override Rect BoundingRect => base.BoundingRect.Grow( 16f );

	public override bool Contains( Vector2 localPos )
	{
		return (LocalRect.Center - localPos).LengthSquared < Radius * Radius;
	}

	protected override void OnPaint()
	{
		var borderColor = Selected 
			? Color.Yellow : Hovered
			? Color.White : Color.White.Darken( 0.125f );

		var fillColor = State.StateMachine?.InitialState == State
			? InitialColor
			: PrimaryColor;

		fillColor = fillColor
			.Lighten( Selected ? 0.5f : Hovered ? 0.25f : 0f )
			.Desaturate( Selected ? 0.5f : Hovered ? 0.25f : 0f );

		Paint.SetBrushRadial( LocalRect.Center - LocalRect.Size * 0.125f, Radius * 1.5f, fillColor.Lighten( 0.5f ), fillColor.Darken( 0.75f ) );
		Paint.DrawCircle( Size * 0.5f, Size );

		Paint.SetPen( borderColor, Selected || Hovered ? 3f : 2f );
		Paint.SetBrushRadial( LocalRect.Center, Radius, 0.75f, Color.Black.WithAlpha( 0f ), 1f, Color.Black.WithAlpha( 0.25f ) );
		Paint.DrawCircle( Size * 0.5f, Size );

		if ( State.StateMachine?.CurrentState == State )
		{
			Paint.ClearBrush();
			Paint.DrawCircle( Size * 0.5f, Size + 8f );
		}

		var titleRect = (State.OnEnterState ?? State.OnUpdateState ?? State.OnLeaveState) is not null
			? new Rect( 0f, Size.y * 0.35f - 12f, Size.x, 24f )
			: new Rect( 0f, Size.y * 0.5f - 12f, Size.x, 24f );

		var isEmoji = IsEmoji( State.Name );

		Paint.ClearBrush();

		if ( isEmoji )
		{
			Paint.SetFont( "roboto", Size.y * 0.5f, 600 );
			Paint.SetPen( Color.White );
			Paint.DrawText( new Rect( 0f, -4f, Size.x, Size.y ), State.Name );
		}
		else
		{
			Paint.SetFont( "roboto", 12f, 600 );
			Paint.SetPen( Color.Black.WithAlpha( 0.5f ) );
			Paint.DrawText( new Rect( titleRect.Position + 2f, titleRect.Size ), State.Name );
		}

		Paint.SetFont( "roboto", 12f, 600 );

		DrawActionIcons( Color.Black.WithAlpha( 0.5f ), true, isEmoji );

		if ( !isEmoji )
		{
			Paint.SetPen( borderColor );
			Paint.DrawText( titleRect, State.Name );
		}

		DrawActionIcons( borderColor, false, isEmoji );
	}

	private bool IsEmoji( string text )
	{
		// TODO: this is probably missing tons of them
		return text.Length == 2 && text[0] >= 0x8000 && char.ConvertToUtf32( text, 0 ) != -1;
	}

	private void DrawActionIcons( Color color, bool shadow, bool isEmoji )
	{
		var pos = new Vector2( Size.x * 0.5f, Size.y * (isEmoji ? 0.5f : 0.6f) ) - 12f;
		var actionCount = (State.OnEnterState is not null ? 1 : 0)
			+ (State.OnUpdateState is not null ? 1 : 0)
			+ (State.OnLeaveState is not null ? 1 : 0);

		pos.x -= (actionCount - 1) * 16f;

		if ( shadow && isEmoji )
		{
			Paint.ClearPen();
			Paint.SetBrush( Color.Black.WithAlpha( 0.9f ) );
			Paint.DrawRect( new Rect( pos.x - 4f, pos.y - 4f, actionCount * 32f, 32f ), 3f );

			Paint.ClearBrush();
		}

		if ( shadow )
		{
			pos += 2f;
		}

		DrawActionIcon( State.OnEnterState, "login", color, shadow, ref pos );
		DrawActionIcon( State.OnUpdateState, "update", color, shadow, ref pos );
		DrawActionIcon( State.OnLeaveState, "logout", color, shadow, ref pos );
	}

	private void DrawActionIcon( Action? action, string icon, Color color, bool shadow, ref Vector2 pos )
	{
		if ( !action.TryGetActionGraphImplementation( out var graph, out _ ) ) return;

		if ( graph.HasErrors() )
		{
			Paint.SetPen( shadow ? color : Color.Red.WithAlpha( color.a ) );
			icon = "error";
		}
		else
		{
			Paint.SetPen( color );

			if ( !string.IsNullOrEmpty( graph.Icon ) )
			{
				icon = graph.Icon;
			}
		}

		Paint.DrawIcon( new Rect( pos.x, pos.y, 24f, 24f ), icon, 20f );
		pos.x += 32f;
	}

	protected override void OnMousePressed( GraphicsMouseEvent e )
	{
		base.OnMousePressed( e );

		if ( e.RightMouseButton )
		{
			_rightMousePressed = true;

			e.Accepted = true;
		}
	}

	protected override void OnMouseReleased( GraphicsMouseEvent e )
	{
		base.OnMouseReleased( e );

		if ( e.RightMouseButton && _rightMousePressed )
		{
			_rightMousePressed = false;

			e.Accepted = true;
		}
	}

	protected override void OnMouseMove( GraphicsMouseEvent e )
	{
		if ( _rightMousePressed && !Contains( e.LocalPosition ) )
		{
			_rightMousePressed = false;

			View.StartCreatingTransition( this );
		}

		base.OnMouseMove( e );
	}

	private void UpdateTransitions()
	{
		foreach ( var transition in State.Transitions )
		{
			View.GetTransitionItem( transition )?.ForceUpdate();
		}
	}

	protected override void OnHoverEnter( GraphicsHoverEvent e )
	{
		base.OnHoverEnter( e );
		UpdateTransitions();
	}

	protected override void OnHoverLeave( GraphicsHoverEvent e )
	{
		base.OnHoverLeave( e );
		UpdateTransitions();
	}

	protected override void OnSelectionChanged()
	{
		base.OnSelectionChanged();
		UpdateTransitions();
	}

	public void OnContextMenu( ContextMenuEvent e )
	{
		e.Accepted = true;
		Selected = true;

		var menu = new global::Editor.Menu();

		menu.AddHeading( "State" );

		menu.AddMenu( "Rename", "edit" ).AddLineEdit( "Rename", State.Name, onSubmit: value =>
		{
			State.Name = value ?? "Unnamed";
			Update();

			SceneEditorSession.Active.Scene.EditLog( "State Renamed", State.StateMachine );
		}, autoFocus: true );

		if ( State.StateMachine.InitialState != State )
		{
			menu.AddOption( "Make Initial", "start", action: () =>
			{
				State.StateMachine.InitialState = State;
				Update();

				SceneEditorSession.Active.Scene.EditLog( "Initial State Assigned", State.StateMachine );
			} );
		}

		menu.AddOption( "Delete", "delete", action: Delete );

		menu.AddSeparator();
		menu.AddHeading( "Actions" );
		AddActionOptions( menu, "Enter Action", "login", () => State.OnEnterState, action => State.OnEnterState = action );
		AddActionOptions( menu, "Update Action", "update", () => State.OnUpdateState, action => State.OnUpdateState = action );
		AddActionOptions( menu, "Leave Action", "logout", () => State.OnLeaveState, action => State.OnLeaveState = action );

		menu.OpenAtCursor( true );
	}

	private void AddActionOptions( global::Editor.Menu menu, string title, string icon, Func<Action?> getter, Action<Action?> setter )
	{
		if ( getter() is {} action )
		{
			var subMenu = menu.AddMenu( title, icon );

			subMenu.AddOption( "Edit", "edit", action: () =>
			{
				if ( action.TryGetActionGraphImplementation( out var graph, out _ ) )
				{
					EditorEvent.Run( "actiongraph.inspect", graph );
				}
			} );
			subMenu.AddOption( "Clear", "clear", action: () =>
			{
				setter( null );
				Update();

				SceneEditorSession.Active.Scene.EditLog( $"State {title} Removed", State.StateMachine );
			} );
		}
		else
		{
			menu.AddOption( $"Add {title}", icon, action: () =>
			{
				var graph = View.CreateGraph<Action>( title );
				setter( graph );
				EditorEvent.Run( "actiongraph.inspect", (ActionGraph)graph );
				Update();

				SceneEditorSession.Active.Scene.EditLog( $"State {title} Added", State.StateMachine );
			} );
		}
	}

	protected override void OnMoved()
	{
		State.EditorPosition = Position.SnapToGrid( View.GridSize );
		SceneEditorSession.Active.Scene.EditLog( "State Moved", State.StateMachine );

		UpdatePosition();
	}

	public void UpdatePosition()
	{
		Position = State.EditorPosition;

		PositionChanged?.Invoke();
	}

	protected override void OnDestroy()
	{
		base.OnDestroy();

		var transitions = View.Items.OfType<TransitionItem>()
			.Where( x => x.Source == this || x.Target == this )
			.ToArray();

		foreach ( var transition in transitions )
		{
			transition.Destroy();
		}
	}

	public void Delete()
	{
		if ( State.StateMachine.InitialState == State )
		{
			State.StateMachine.InitialState = null;
		}

		var transitions = View.Items.OfType<TransitionItem>()
			.Where( x => x.Source == this || x.Target == this )
			.ToArray();

		foreach ( var transition in transitions )
		{
			transition.Delete();
		}

		State.Remove();
		Destroy();

		SceneEditorSession.Active.Scene.EditLog( "State Removed", State.StateMachine );
	}

	public void Frame()
	{
		var hash = HashCode.Combine( State.StateMachine.InitialState == State, State.StateMachine.CurrentState == State );
		if ( hash == _lastHash ) return;

		_lastHash = hash;
		Update();
	}
}
