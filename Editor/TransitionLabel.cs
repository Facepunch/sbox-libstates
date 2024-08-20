using System;
using Editor;
using Facepunch.ActionGraphs;

namespace Sandbox.States.Editor;

public interface ITransitionLabelSource : IDeletable, IDoubleClickable, IValid
{
	string Title { get; }
	string? Description { get; }

	string? Icon { get; }
	string? Text { get; }

	public Color? Color => null;

	void BuildContextMenu( global::Editor.Menu menu );
}

public abstract record TransitionActionGraph<T>( TransitionItem Item ) : ITransitionLabelSource
	where T : Delegate
{
	public Transition Transition => Item.Transition!;
	public abstract string Title { get; }

	public string? Icon => ActionGraph is { } graph ? graph.HasErrors() ? "error" : graph.Icon ?? DefaultIcon : null;
	public string? Text => ActionGraph is { } graph ? graph.Title ?? "Unnamed" : null;

	public abstract string? Description { get; }

	string? ITransitionLabelSource.Description => ActionGraph is { Description: { } desc }
		? $"<p>{Description}</p><p>{desc}</p>"
		: Description;

	public Color? Color => ActionGraph is { } graph && graph.HasErrors() ? global::Color.Red.Darken( 0.05f ) : (Color?)null;

	protected abstract string DefaultIcon { get; }
	protected abstract T? Delegate { get; set; }

	public bool IsValid => ActionGraph is not null;

	protected ActionGraph? ActionGraph
	{
		get => Delegate.TryGetActionGraphImplementation( out var graph, out _ ) ? graph : null;
		set => Delegate = (ActionGraph<T>?)value;
	}

	public void BuildContextMenu( global::Editor.Menu menu )
	{
		if ( !IsValid )
		{
			menu.AddOption( $"Add {Title}", DefaultIcon, action: CreateOrEdit );

			return;
		}

		menu.AddHeading( Title );

		if ( Delegate is not null )
		{
			menu.AddOption( "Edit", "edit", action: CreateOrEdit );
			menu.AddOption( "Clear", "clear", action: () =>
			{
				Delegate = null;
				Item.Update();

				SceneEditorSession.Active.Scene.EditLog( $"Transition {Title} Removed", Transition.StateMachine );
			} );
		}
	}

	private void CreateOrEdit()
	{
		if ( Delegate is null )
		{
			Delegate = Item.Source.View.CreateGraph<T>( Title );
			EditorEvent.Run( "actiongraph.inspect", ActionGraph );
			Item.Update();

			SceneEditorSession.Active.Scene.EditLog( $"Transition {Title} Added", Transition.StateMachine );
		}
		else
		{
			EditorEvent.Run( "actiongraph.inspect", ActionGraph );
		}
	}

	public void Delete()
	{
		Delegate = null;
	}

	public void DoubleClick()
	{
		CreateOrEdit();
	}
}

public record TransitionCondition( TransitionItem Item ) : TransitionActionGraph<Func<bool>>( Item )
{
	public override string Title => "Condition";
	public override string Description => "This transition will only be taken if this expression is true.";

	protected override string DefaultIcon => "question_mark";

	protected override Func<bool>? Delegate
	{
		get => Transition.Condition;
		set => Transition.Condition = value;
	}
}

public record TransitionAction( TransitionItem Item ) : TransitionActionGraph<Action>( Item )
{
	public override string Title => "Action";
	public override string Description => "Action performed when this transition is taken.";

	protected override string DefaultIcon => "directions_run";

	protected override Action? Delegate
	{
		get => Transition.OnTransition;
		set => Transition.OnTransition = value;
	}
}

public sealed class TransitionLabel : GraphicsItem, IContextMenuSource, IDeletable, IDoubleClickable
{
	public TransitionItem Transition { get; }
	public ITransitionLabelSource Source { get; }

	public float MaxWidth { get; set; } = 256f;

	private bool _canShowText;

	public string? Icon => Source.Icon;
	public string? Text => Source.Text;

	public TransitionLabel( TransitionItem parent, ITransitionLabelSource source )
		: base( parent )
	{
		Transition = parent;
		Source = source;

		HoverEvents = true;
		Selectable = true;

		ZIndex = -1;
	}

	private void SetFont()
	{
		Paint.SetFont( "roboto", 10f );
	}

	public void Layout()
	{
		SetFont();

		var iconWidth = string.IsNullOrEmpty( Icon ) ? 0f : 24f;

		_canShowText = MaxWidth - iconWidth >= 8f;

		var textWidth = string.IsNullOrEmpty( Text ) || !_canShowText
			? 0f
			: Paint.MeasureText( new Rect( 0f, 0f, MaxWidth - iconWidth, 24f ), Text, TextFlag.LeftCenter | TextFlag.SingleLine ).Width;

		PrepareGeometryChange();

		Size = new Vector2( iconWidth + textWidth, 24f );
		Tooltip = Source.Description;
	}

	protected override void OnHoverEnter( GraphicsHoverEvent e )
	{
		base.OnHoverEnter( e );
		Transition.ForceUpdate();
	}

	protected override void OnHoverLeave( GraphicsHoverEvent e )
	{
		base.OnHoverLeave( e );
		Transition.ForceUpdate();
	}

	protected override void OnPaint()
	{
		SetFont();

		var (hovered, selected) = Transition.GetSelectedState();

		hovered |= Hovered;
		selected |= Selected;

		var color = TransitionItem.GetPenColor( hovered, selected );

		if ( !selected && Source.Color is { } overrideColor )
		{
			if ( hovered ) color = overrideColor.Desaturate( 0.5f ).Lighten( 0.5f );
			else color = overrideColor;
		}

		Paint.ClearBrush();
		Paint.SetPen( color );

		var iconWidth = string.IsNullOrEmpty( Icon ) ? 0f : 24f;

		if ( Width >= 24f && !string.IsNullOrEmpty( Icon ) )
		{
			Paint.DrawIcon( new Rect( 0f, 0f, 24f, Height ), Icon, 12f );
		}

		if ( _canShowText && !string.IsNullOrEmpty( Text ) )
		{
			Paint.DrawText( new Rect( iconWidth, 0f, Width - iconWidth, Height ), Text, TextFlag.LeftCenter | TextFlag.SingleLine );
		}
	}

	public void BuildContextMenu( global::Editor.Menu menu )
	{
		Source.BuildContextMenu( menu );
	}

	public void OnContextMenu( ContextMenuEvent e )
	{
		e.Accepted = true;

		Selected = true;

		var menu = new global::Editor.Menu { DeleteOnClose = true };

		BuildContextMenu( menu );

		menu.OpenAtCursor( true );
	}

	public void Delete()
	{
		Source.Delete();
		Transition.ForceUpdate();
	}

	public void DoubleClick()
	{
		Source.DoubleClick();
	}
}
