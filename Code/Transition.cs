using System;
using Sandbox.Diagnostics;

namespace Sandbox.States;

public sealed class Transition : IComparable<Transition>, IValid
{
	private float? _delay;
	private Func<bool>? _condition;

	/// <summary>
	/// The state machine containing this transition.
	/// </summary>
	public StateMachineComponent StateMachine => Source.StateMachine;

	/// <summary>
	/// Unique ID of this transition in the <see cref="StateMachineComponent"/>.
	/// </summary>
	public int Id { get; }

	/// <summary>
	/// The state this transition originates from.
	/// </summary>
	public State Source { get; }

	/// <summary>
	/// The destination of this transition.
	/// </summary>
	public State Target { get; }

	/// <summary>
	/// Does this transition still belong to a state.
	/// </summary>
	public bool IsValid { get; internal set; }

	internal Transition( int id, State source, State target )
	{
		Source = source;
		Target = target;
		Id = id;
	}

	/// <summary>
	/// Optional delay before this transition is taken.
	/// If null, this transition can be taken at any time.
	/// </summary>
	public float? Delay
	{
		get => _delay;
		set
		{
			_delay = value;
			Source.InvalidateTransitions();
		}
	}

	/// <summary>
	/// Optional condition to evaluate.
	/// </summary>
	public Func<bool>? Condition
	{
		get => _condition;
		set
		{
			_condition = value;
			Source.InvalidateTransitions();
		}
	}

	/// <summary>
	/// Action performed when this transition is taken.
	/// </summary>
	public Action? OnTransition { get; set; }

	public void Remove()
	{
		if ( !IsValid ) return;
		StateMachine.RemoveTransition( this );
	}

	public int CompareTo( Transition? other )
	{
		if ( other is null ) return 1;

		var delayCompare = (Delay ?? float.PositiveInfinity).CompareTo( other.Delay ?? float.PositiveInfinity );
		if ( delayCompare != 0 ) return delayCompare;

		var conditionCompare = (Condition is null).CompareTo( other.Condition is null );
		if ( conditionCompare != 0 ) return conditionCompare;

		return Target.Id.CompareTo( other.Target.Id );
	}

	internal record Model( int Id, int SourceId, int TargetId, float? Delay, Func<bool>? Condition, Action? OnTransition );

	internal Model Serialize()
	{
		return new Model( Id, Source.Id, Target.Id, Delay, Condition, OnTransition );
	}

	internal void Deserialize( Model model )
	{
		Assert.AreEqual( Id, model.Id );
		Assert.AreEqual( Source.Id, model.SourceId );
		Assert.AreEqual( Target.Id, model.TargetId );

		Delay = model.Delay;
		Condition = model.Condition;
		OnTransition = model.OnTransition;
	}
}
