using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Diagnostics;

namespace Sandbox.States;

[Title( "State Machine" ), Icon( "smart_toy" ), Category( "State Machines" )]
public sealed class StateMachineComponent : Component
{
	/// <summary>
	/// How many instant state transitions in a row until we throw an error?
	/// </summary>
	public const int MaxInstantTransitions = 16;

	private readonly Dictionary<int, State> _states = new();
	private readonly Dictionary<int, Transition> _transitions = new();

	private int _nextId = 0;

	/// <summary>
	/// All states in this machine.
	/// </summary>
	public IEnumerable<State> States => _states.Values;

	/// <summary>
	/// All transitions between states in this machine.
	/// </summary>
	public IEnumerable<Transition> Transitions => _transitions.Values;

	/// <summary>
	/// Which state becomes active when the machine starts?
	/// </summary>
	public State? InitialState { get; set; }

	/// <summary>
	/// Which state is currently active?
	/// </summary>
	public State? CurrentState
	{
		get => CurrentStateId is {} id ? _states!.GetValueOrDefault( id ) : null;
		private set => CurrentStateId = value?.Id;
	}

	[Property] private int? CurrentStateId { get; set; }

	private float _stateTime;

	private bool _firstUpdate = true;

	protected override void OnStart()
	{
		if ( !Network.IsProxy && InitialState is { } initial )
		{
			CurrentState = initial;
		}
	}

	private static void InvokeSafe( Action? action )
	{
		try
		{
			action?.Invoke();
		}
		catch ( Exception ex )
		{
			Log.Error( ex );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( _firstUpdate )
		{
			_firstUpdate = false;

			InvokeSafe( CurrentState?.OnEnterState );
		}

		if ( !Network.IsProxy )
		{
			var transitions = 0;
			var prevTime = _stateTime;

			_stateTime += Time.Delta;

			while ( transitions++ < MaxInstantTransitions && CurrentState?.GetNextTransition( prevTime, _stateTime ) is { } transition )
			{
				DoTransition( transition.Id );

				prevTime = 0f;

				if ( transition.Delay is { } delay )
				{
					_stateTime -= delay;
				}
				else
				{
					_stateTime = 0f;
				}
			}
		}

		InvokeSafe( CurrentState?.OnUpdateState );
	}

	[Broadcast( NetPermission.OwnerOnly )]
	private void DoTransition( int transitionId )
	{
		var transition = _transitions!.GetValueOrDefault( transitionId )
			?? throw new Exception( $"Unknown transition id: {transitionId}" );

		var current = CurrentState!;

		Assert.AreEqual( current, transition.Source );

		InvokeSafe( current.OnLeaveState );
		InvokeSafe( transition.OnTransition );

		CurrentState = current = transition.Target;

		InvokeSafe( current.OnEnterState );
	}

	public State AddState()
	{
		var state = new State( this, _nextId++ );

		_states.Add( state.Id, state );

		state.IsValid = true;

		InitialState ??= state;

		return state;
	}

	internal void RemoveState( State state )
	{
		Assert.AreEqual( this, state.StateMachine );
		Assert.AreEqual( state, _states[state.Id] );

		if ( InitialState == state )
		{
			InitialState = null;
		}

		if ( CurrentState == state )
		{
			CurrentState = null;
		}

		var transitions = Transitions
			.Where( x => x.Source == state || x.Target == state )
			.ToArray();

		foreach ( var transition in transitions )
		{
			transition.Remove();
		}

		_states.Remove( state.Id );

		state.IsValid = false;
	}

	internal Transition AddTransition( State source, State target )
	{
		ArgumentNullException.ThrowIfNull( source, nameof( source ) );
		ArgumentNullException.ThrowIfNull( target, nameof( target ) );

		Assert.AreEqual( this, source.StateMachine );
		Assert.AreEqual( this, target.StateMachine );

		var transition = new Transition( _nextId++, source, target );

		_transitions.Add( transition.Id, transition );

		transition.IsValid = true;

		source.InvalidateTransitions();

		return transition;
	}

	internal void RemoveTransition( Transition transition )
	{
		Assert.AreEqual( this, transition.StateMachine );
		Assert.AreEqual( transition, _transitions[transition.Id] );

		_transitions.Remove( transition.Id );

		transition.IsValid = false;
		transition.Source.InvalidateTransitions();
	}

	internal void Clear()
	{
		_states.Clear();
		_transitions.Clear();

		InitialState = null;

		_nextId = 0;
	}

	[Property]
	private Model Serialized
	{
		get => Serialize();
		set => Deserialize( value );
	}

	internal record Model(
		IReadOnlyList<State.Model> States,
		IReadOnlyList<Transition.Model> Transitions,
		int? InitialStateId );

	internal Model Serialize()
	{
		return new Model(
			States.Select( x => x.Serialize() ).OrderBy( x => x.Id ).ToArray(),
			Transitions.Select( x => x.Serialize() ).OrderBy( x => x.Id ).ToArray(),
			InitialState?.Id );
	}

	internal void Deserialize( Model model )
	{
		Clear();

		foreach ( var stateModel in model.States )
		{
			var state = new State( this, stateModel.Id );

			_states.Add( state.Id, state );
			_nextId = Math.Max( _nextId, state.Id + 1 );

			state.Deserialize( stateModel );
		}

		foreach ( var transitionModel in model.Transitions )
		{
			var transition = new Transition( transitionModel.Id,
				_states[transitionModel.SourceId],
				_states[transitionModel.TargetId] );

			_transitions.Add( transition.Id, transition );
			_nextId = Math.Max( _nextId, transition.Id + 1 );

			transition.Deserialize( transitionModel );
		}

		InitialState = model.InitialStateId is { } id ? _states[id] : null;
	}
}
