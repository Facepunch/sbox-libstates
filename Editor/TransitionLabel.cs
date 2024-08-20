using Editor;

namespace Sandbox.States.Editor;

public sealed class TransitionLabel : GraphicsItem
{
	internal TransitionItem Transition { get; }

	public string? Icon { get; set; }
	public string? Text { get; set; }
	public float MaxWidth { get; set; } = 256f;
	public Color Color { get; set; } = Color.White.Darken( 0.125f );

	private bool _canShowText;

	public TransitionLabel( TransitionItem parent )
		: base( parent )
	{
		Transition = parent;
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
	}

	protected override void OnPaint()
	{
		SetFont();

		Paint.ClearBrush();
		Paint.SetPen( Color );

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
}
