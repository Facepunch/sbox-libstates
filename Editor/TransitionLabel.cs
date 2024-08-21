using Editor;

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

		Cursor = CursorShape.Finger;
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

		var (_, selected) = Transition.GetSelectedState();

		var hovered = Hovered;
		selected |= Selected;

		var color = TransitionItem.GetPenColor( hovered, selected );

		if ( !selected && Source.Color is { } overrideColor )
		{
			if ( hovered ) color = overrideColor.Desaturate( 0.5f ).Lighten( 0.5f );
			else color = overrideColor;
		}

		Paint.ClearBrush();
		Paint.SetPen( color );

		var iconWidth = string.IsNullOrEmpty( Icon ) ? 0f : 22f;

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
