using Editor;
using Facepunch.ActionGraphs;
using System;
using System.Linq;
using System.Text;

namespace Sandbox.States.Editor;

public abstract record TransitionActionGraph<T>( TransitionItem Item ) : ITransitionLabelSource
	where T : Delegate
{
	public Transition Transition => Item.Transition!;
	public abstract string Title { get; }

	public string? Icon => ActionGraph is { } graph ? graph.HasErrors() ? "error" : graph.Icon ?? DefaultIcon : null;
	public string? Text => ActionGraph is { } graph ? graph.Title ?? "Unnamed" : null;

	public abstract string? Description { get; }

	string? ITransitionLabelSource.Description
	{
		get
		{
			var builder = new StringBuilder();

			builder.Append( $"<p>{Description}</p>" );

			if ( ActionGraph is {} graph )
			{
				if ( graph.Description is { } desc )
				{
					builder.Append( $"<p>{desc}</p>" );
				}

				if ( graph.HasErrors() )
				{
					builder.Append( "<p><font color=\"#ff0000\">" );

					foreach ( var message in graph.Messages.Where( x => x.IsError ) )
					{
						builder.AppendLine( message.Value );
					}

					builder.Append( "</font></p>" );
				}
			}

			return builder.ToString();
		}
	}

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
				Item.ForceUpdate();

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
			Item.ForceUpdate();

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
