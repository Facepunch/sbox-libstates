﻿using System;
using System.Diagnostics;
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

	public override bool Contains( Vector2 localPos )
	{
		if ( Transition is null )
		{
			return false;
		}

		if ( GetLocalStartEnd() is not var (start, end, tangent) )
		{
			return false;
		}

		var t = Vector2.Dot( localPos - start, tangent );

		if ( t < 0f || t > (end - start).Length ) return false;

		var s = Vector2.Dot( localPos - start, tangent.Perpendicular );

		return Math.Abs( s ) <= 24f;
	}

	private void OnStatePositionChanged()
	{
		Layout();
	}

	private (Vector2 Start, Vector2 End, Vector2 Tangent)? GetLocalStartEnd()
	{
		var (index, count) = Source.View.GetTransitionPosition( this );

		var sourceCenter = FromScene( Source.Center );
		var targetCenter = Target is null ? FromScene( TargetPosition ) : FromScene( Target.Center );

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
		var usedWidth = count * 24f;

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

	private enum TransitionKind
	{
		Default,
		Conditional,
		Event
	}

	private TransitionKind GetTransitionKind()
	{
		if ( Transition?.Condition.TryGetActionGraphImplementation( out var graph, out _ ) is true )
		{
			return TransitionKind.Conditional;
		}

		return TransitionKind.Default;
	}

	private (string Icon, string Title)? GetLabelParts( Delegate? deleg, string defaultIcon, string defaultTitle )
	{
		return deleg.TryGetActionGraphImplementation( out var graph, out _ )
			? (string.IsNullOrEmpty( graph.Icon ) ? defaultIcon : graph.Icon, string.IsNullOrEmpty( graph.Title ) ? defaultTitle : graph.Title)
			: null;
	}

	protected override void OnPaint()
	{
		if ( GetLocalStartEnd() is not var (start, end, tangent) )
		{
			return;
		}

		var normal = tangent.Perpendicular;

		var selected = Selected || Source.Selected || Transition is null;
		var hovered = Hovered || Source.Hovered;

		var thickness = selected || hovered ? 6f : 4f;
		var offset = thickness * 0.5f * normal;

		var color = selected
			? Color.Yellow : hovered
			? Color.White : Color.White.Darken( 0.125f );

		Paint.ClearPen();
		Paint.SetBrushLinear( start, end, color.Darken( 0.667f ), color );
		Paint.DrawPolygon( start - offset, end - tangent * 14f - offset, end - tangent * 14f + offset, start + offset );

		Paint.SetBrush( color );
		Paint.DrawArrow( end - tangent * 16f, end, 12f );

		var mid = (start + end) * 0.5f;
		var width = (end - start).Length;

		Paint.Translate( mid );
		Paint.Rotate( MathF.Atan2( tangent.y, tangent.x ) * 180f / MathF.PI );

		Paint.ClearBrush();
		Paint.SetPen( color );
		Paint.SetFont( "roboto", 10f );

		var conditionRect = new Rect( -width * 0.5f + 16f, -20f, width - 32f, 16f );
		var actionRect = new Rect( -width * 0.5f + 16f, 4f, width - 32f, 16f );

		(string Icon, string Title)? delayLabel = Transition?.Delay is { } seconds ? ("timer", FormatDuration( seconds )) : null;
		var conditionLabel = GetLabelParts( Transition?.Condition, "question_mark", "Condition" );
		var actionLabel = GetLabelParts( Transition?.OnTransition, "directions_run", "Action" );

		if ( tangent.x < 0f )
		{
			Paint.Rotate( 180f );

			var conditionAdvance = DrawLabel( conditionLabel, conditionRect, TextFlag.SingleLine | TextFlag.RightBottom );

			conditionRect = conditionRect.Shrink( 0f, 0f, conditionAdvance, 0f );

			DrawLabel( delayLabel, conditionRect, TextFlag.SingleLine | TextFlag.RightBottom );
			DrawLabel( actionLabel, actionRect, TextFlag.SingleLine | TextFlag.LeftTop );
		}
		else
		{
			var delayAdvance = DrawLabel( delayLabel, conditionRect, TextFlag.SingleLine | TextFlag.LeftBottom );

			conditionRect = conditionRect.Shrink( delayAdvance, 0f, 0f, 0f );

			DrawLabel( conditionLabel, conditionRect, TextFlag.SingleLine | TextFlag.LeftBottom );
			DrawLabel( actionLabel, actionRect, TextFlag.SingleLine | TextFlag.RightTop );
		}
	}

	private float DrawLabel( (string Icon, string Title)? label, Rect rect, TextFlag flags )
	{
		if ( label is not { Icon: var icon, Title: var title } )
		{
			return 0f;
		}

		rect = rect.Shrink( 20f, 0f, 0f, 0f );

		var textRect = Paint.MeasureText( rect, title, flags );
		var iconRect = new Rect( textRect.Left - 18f, rect.Top, 16f, 16f );

		Paint.DrawIcon( iconRect, icon, 12f );
		Paint.DrawText( rect, title, flags );

		return textRect.Width + 20f;
	}

	public void Layout()
	{
		var rect = Rect.FromPoints( Source.Center, Target?.Center ?? TargetPosition ).Grow( 64f );

		Position = rect.Position;
		Size = rect.Size;

		Update();
	}

	public void Delete()
	{
		Transition!.Remove();
		Destroy();

		SceneEditorSession.Active.Scene.EditLog( "Transition Removed", Transition.StateMachine );
	}

	private T CreateGraph<T>( string title )
		where T : Delegate
	{
		var graph = ActionGraph.Create<T>( EditorNodeLibrary );
		var inner = (ActionGraph)graph;

		inner.Title = title;
		inner.SetParameters(
			inner.Inputs.Values.Concat( InputDefinition.Target( typeof( GameObject ), Transition!.StateMachine.GameObject ) ),
			inner.Outputs.Values.ToArray() );

		return graph;
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

		if ( Transition.Condition is not null )
		{
			menu.AddOption( "Edit Condition", "edit", action: () => EditGraph( Transition.Condition ) );
			menu.AddOption( "Clear Condition", "clear", action: () =>
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
				Transition.Condition = CreateGraph<Func<bool>>( "Condition" );
				EditGraph( Transition.Condition );
				Update();

				SceneEditorSession.Active.Scene.EditLog( "Transition Condition Added", Transition.StateMachine );
			} );
		}

		if ( Transition.Delay is { } currentDelay )
		{
			menu.AddMenu( "Edit Delay", "timer" ).AddLineEdit( "Seconds", value: currentDelay.ToString( "R" ), autoFocus: true, onSubmit:
				delayStr =>
				{
					if ( !float.TryParse( delayStr, out var seconds ) || seconds < 0f )
					{
						return;
					}

					Transition.Delay = seconds;
					Update();

					SceneEditorSession.Active.Scene.EditLog( "Transition Delay Changed", Transition.StateMachine );
				} );
			menu.AddOption( "Clear Delay", "timer_off", action: () =>
			{
				Transition.Delay = null;
				Update();

				SceneEditorSession.Active.Scene.EditLog( "Transition Delay Removed", Transition.StateMachine );
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
		}

		menu.AddSeparator();

		if ( Transition.OnTransition is not null )
		{
			menu.AddOption( "Edit Action", "edit", action: () =>
			{
				EditGraph( Transition.OnTransition );
			} );
			menu.AddOption( "Clear Action", "clear", action: () =>
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
				Transition.OnTransition = CreateGraph<Action>( "Action" );
				EditGraph( Transition.OnTransition );
				Update();

				SceneEditorSession.Active.Scene.EditLog( "Transition Action Added", Transition.StateMachine );
			} );
		}

		menu.AddSeparator();

		menu.AddOption( "Delete Transition", "delete", action: Delete );

		menu.OpenAtCursor( true );
	}

	public int CompareTo( TransitionItem? other )
	{
		return Source.State.Id.CompareTo( other?.Source.State.Id ?? -1 );
	}
}
