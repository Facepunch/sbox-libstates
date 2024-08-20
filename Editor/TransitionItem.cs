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

	private readonly TransitionLabel? _eventLabel;
	private readonly TransitionLabel? _conditionLabel;
	private readonly TransitionLabel? _actionLabel;

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

		if ( transition is not null )
		{
			_eventLabel = new TransitionLabel( this, new TransitionEvent( this ) );
			_conditionLabel = new TransitionLabel( this, new TransitionCondition( this ) );
			_actionLabel = new TransitionLabel( this, new TransitionAction( this ) );
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

	public (bool Hovered, bool Selected) GetSelectedState()
	{
		var selected = Selected || Source.Selected || Transition is null;
		var hovered = Hovered || Source.Hovered;

		return (hovered, selected);
	}

	public static Color GetPenColor( bool hovered, bool selected )
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

		LabelLayout();
		Update();
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
			? new Vector2( margin, Size.y * 0.5f - 28f )
			: new Vector2( Width - totalWidth - margin, Size.y * 0.5f + 4f );

		foreach ( var label in labels )
		{
			if ( label is null ) continue;

			label.Position = origin;
			origin.x += label.Width;

			label.Update();
		}
	}

	private void LabelLayout()
	{
		AlignLabels( true, _eventLabel, _conditionLabel );
		AlignLabels( false, _actionLabel );
	}

	public void Delete()
	{
		Transition!.Remove();
		Destroy();

		SceneEditorSession.Active.Scene.EditLog( "Transition Removed", Transition.StateMachine );
	}

	public void OnContextMenu( ContextMenuEvent e )
	{
		if ( Transition is null ) return;

		e.Accepted = true;
		Selected = true;

		var menu = new global::Editor.Menu { DeleteOnClose = true };

		menu.AddHeading( "Transition" );

		foreach ( var label in Children.OfType<TransitionLabel>() )
		{
			// If label has contents, let the user right-click on that instead.
			if ( label.Source.IsValid ) continue;

			label.BuildContextMenu( menu );
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
		LabelLayout();
		Update();
	}

	public int CompareTo( TransitionItem? other )
	{
		return Source.State.Id.CompareTo( other?.Source.State.Id ?? -1 );
	}
}
