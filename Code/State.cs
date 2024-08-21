using System;
using System.Collections.Generic;
using Sandbox.Diagnostics;

namespace Sandbox.States;

public sealed class State : IValid
{
	private readonly List<Transition> _orderedTransitions = new();
	private bool _transitionsDirty = false;

	public StateMachineComponent StateMachine { get; }

	/// <summary>
	/// Unique ID of this state in its containing <see cref="StateMachineComponent"/>.
	/// </summary>
	public int Id { get; }

	/// <summary>
	/// Helpful name of this state.
	/// </summary>
	public string Name { get; set; } = "Unnamed";

	public bool IsValid { get; internal set; }

	public IReadOnlyList<Transition> Transitions
	{
		get
		{
			if ( _transitionsDirty ) UpdateTransitions();
			return _orderedTransitions;
		}
	}

	/// <summary>
	/// Event dispatched on the owner when this state is entered.
	/// </summary>
	public Action? OnEnterState { get; set; }

	/// <summary>
	/// Event dispatched on the owner while this state is active.
	/// </summary>
	public Action? OnUpdateState { get; set; }

	/// <summary>
	/// Event dispatched on the owner when this state is exited.
	/// </summary>
	public Action? OnLeaveState { get; set; }

	public Vector2 EditorPosition { get; set; }

	internal State( StateMachineComponent stateMachine, int id )
	{
		StateMachine = stateMachine;
		Id = id;
	}

	private void UpdateTransitions()
	{
		_transitionsDirty = false;
		_orderedTransitions.Clear();

		foreach ( var transition in StateMachine.Transitions )
		{
			if ( transition.Source == this )
			{
				_orderedTransitions.Add( transition );
			}
		}

		_orderedTransitions.Sort();
	}

	internal Transition? GetNextTransition( string message )
	{
		foreach ( var transition in Transitions )
		{
			if ( transition.Message != message )
			{
				continue;
			}

			if ( transition.Target == this )
			{
				// TODO
				continue;
			}

			if ( transition.Delay is not null )
			{
				continue;
			}

			try
			{
				if ( transition.Condition?.Invoke() is not false )
				{
					return transition;
				}
			}
			catch ( Exception e )
			{
				Log.Error( e );
			}
		}

		return null;
	}

	internal Transition? GetNextTransition( float prevTime, float nextTime )
	{
		foreach ( var transition in Transitions )
		{
			if ( transition.Message is not null )
			{
				continue;
			}

			if ( transition.Target == this )
			{
				// TODO
				continue;
			}

			if ( transition.Delay is { } delay )
			{
				if ( delay > prevTime || delay > nextTime )
				{
					continue;
				}
			}

			try
			{
				if ( transition.Condition?.Invoke() is not false )
				{
					return transition;
				}
			}
			catch ( Exception e )
			{
				Log.Error( e );
			}
		}

		return null;
	}

	public Transition AddTransition( State target )
	{
		return StateMachine.AddTransition( this, target );
	}

	public void Remove()
	{
		if ( !IsValid ) return;
		StateMachine.RemoveState( this );
	}

	internal void InvalidateTransitions()
	{
		_transitionsDirty = true;
	}

	internal record Model( int Id, string Name, Action? OnEnterState, Action? OnUpdateState, Action? OnLeaveState, Model.UserDataModel? UserData )
	{
		public record UserDataModel( Vector2 Position );
	}

	internal Model Serialize()
	{
		return new Model( Id, Name, OnEnterState, OnUpdateState, OnLeaveState, new Model.UserDataModel( EditorPosition ) );
	}

	internal void Deserialize( Model model )
	{
		Assert.AreEqual( Id, model.Id );

		Name = model.Name;

		OnEnterState = model.OnEnterState;
		OnUpdateState = model.OnUpdateState;
		OnLeaveState = model.OnLeaveState;

		EditorPosition = model.UserData?.Position ?? Vector2.Zero;
	}
}
