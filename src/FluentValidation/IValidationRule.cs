#region License
// Copyright (c) .NET Foundation and contributors.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
// The latest version of this file can be found at https://github.com/FluentValidation/FluentValidation
#endregion

namespace FluentValidation {
	using System;
	using System.Collections.Generic;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;
	using Internal;
	using Results;
	using Validators;

	/// <summary>
	/// Defines a rule associated with a property which can have multiple validators.
	/// This interface is used for construction of rules and should not be implemented in your code directly.
	/// </summary>
	public interface IValidationRule {
		/// <summary>
		/// The validators that are grouped under this rule.
		/// </summary>
		IEnumerable<IPropertyValidator> Validators { get; }

		/// <summary>
		/// Name of the rule-set to which this rule belongs.
		/// </summary>
		string[] RuleSets { get; set; }

		/// <summary>
		/// Display name for the property.
		/// </summary>
		string GetDisplayName(IValidationContext context);

		/// <summary>
		/// Returns the property name for the property being validated.
		/// Returns null if it is not a property being validated (eg a method call)
		/// </summary>
		string PropertyName { get; set; }

		/// <summary>
		/// Whether this rule has a condition.
		/// </summary>
		bool HasCondition { get; }

		/// <summary>
		/// Whether this rule has an async condition.
		/// </summary>
		bool HasAsyncCondition { get; }

		/// <summary>
		/// Type of the property being validated
		/// </summary>
		Type TypeToValidate { get; }

		// TODO: Remove this from the interface.
		public Func<MessageBuilderContext, string> MessageBuilder { get; set; }

		/// <summary>
		/// Cascade mode for this rule.
		/// </summary>
		CascadeMode CascadeMode { get; set; }

		/// <summary>
		/// Expression that was used to create the rule.
		/// </summary>
		LambdaExpression Expression { get; }

		/// <summary>
		/// Property associated with this rule.
		/// </summary>
		MemberInfo Member { get; }
	}

	/// <summary>
	/// Defines a rule associated with a property which can have multiple validators.
	/// This interface is used for construction of rules and should not be implemented in your code directly.
	/// </summary>
	public interface IValidationRule<T> : IValidationRule {

		/// <summary>
		/// Performs validation using a validation context and returns a collection of Validation Failures.
		/// </summary>
		/// <param name="context">Validation Context</param>
		/// <returns>A collection of validation failures</returns>
		IEnumerable<ValidationFailure> Validate(IValidationContext<T> context);

		/// <summary>
		/// Performs validation using a validation context and returns a collection of Validation Failures asynchronously.
		/// </summary>
		/// <param name="context">Validation Context</param>
		/// <param name="cancellation">Cancellation token</param>
		/// <returns>A collection of validation failures</returns>
		Task<IEnumerable<ValidationFailure>> ValidateAsync(IValidationContext<T> context, CancellationToken cancellation);

		/// <summary>
		/// Applies a condition to either all the validators in the rule, or the most recent validator in the rule chain.
		/// </summary>
		/// <param name="predicate">The condition to apply</param>
		/// <param name="applyConditionTo">Indicates whether the condition should be applied to all validators in the rule, or only the current one</param>
		void ApplyCondition(Func<IValidationContext<T>, bool> predicate, ApplyConditionTo applyConditionTo = ApplyConditionTo.AllValidators);

		/// <summary>
		/// Applies an asynchronous condition to either all the validators in the rule, or the most recent validator in the rule chain.
		/// </summary>
		/// <param name="predicate">The condition to apply</param>
		/// <param name="applyConditionTo">Indicates whether the condition should be applied to all validators in the rule, or only the current one</param>
		void ApplyAsyncCondition(Func<IValidationContext<T>, CancellationToken, Task<bool>> predicate, ApplyConditionTo applyConditionTo = ApplyConditionTo.AllValidators);

		/// <summary>
		/// Applies a condition that wraps the entire rule.
		/// </summary>
		/// <param name="condition">The condition to apply.</param>
		void ApplySharedCondition(Func<IValidationContext<T>, bool> condition);

		/// <summary>
		/// Applies an asynchronous condition that wraps the entire rule.
		/// </summary>
		/// <param name="condition">The condition to apply.</param>
		void ApplySharedAsyncCondition(Func<IValidationContext<T>, CancellationToken, Task<bool>> condition);

		/// <summary>
		/// Function that will be invoked if any of the validators associated with this rule fail.
		/// </summary>
		Action<T, IEnumerable<ValidationFailure>> OnFailure { get; set; }

		/// <summary>
		/// Dependent rules
		/// </summary>
		List<IValidationRule<T>> DependentRules { get; }

		/// <summary>
		/// Adds a validator to the rule.
		/// </summary>
		void AddValidator(IPropertyValidator validator);

		/// <summary>
		/// Sets the display name for the property.
		/// </summary>
		/// <param name="name">The property's display name</param>
		void SetDisplayName(string name);

		/// <summary>
		/// Sets the display name for the property using a function.
		/// </summary>
		/// <param name="factory">The function for building the display name</param>
		void SetDisplayName(Func<IValidationContext<T>, string> factory);

		/// <summary>
		/// Replaces a validator in this rule. Used to wrap validators.
		/// </summary>
		void ReplaceValidator(IPropertyValidator original, IPropertyValidator newValidator);

		/// <summary>
		/// Remove a validator in this rule.
		/// </summary>
		void RemoveValidator(IPropertyValidator original);

		/// <summary>
		/// Clear all validators from this rule.
		/// </summary>
		void ClearValidators();
	}

	/// <summary>
	/// Defines a rule associated with a property which can have multiple validators.
	/// This interface is used for construction of rules and should not be implemented in your code directly.
	/// </summary>
	public interface IValidationRule<T, TProperty> : IValidationRule<T> {
		/// <summary>
		/// The current validator being configured by this rule.
		/// </summary>
		IPropertyValidator<T,TProperty> CurrentValidator { get; }
	}

	/// <summary>
	/// Defines a rule associated with a collection property where each
	/// element in the collection can have multiple validators.
	/// This interface is used for construction of rules and should not be implemented in your code directly.
	/// </summary>
	public interface ICollectionRule<T, TElement> : IValidationRule<T, TElement> {
		/// <summary>
		/// Filter that should include/exclude items in the collection.
		/// </summary>
		public Func<TElement, bool> Filter { get; set; }

		/// <summary>
		/// Constructs the indexer in the property name associated with the error message.
		/// By default this is "[" + index + "]"
		/// </summary>
		public Func<T, IEnumerable<TElement>, TElement, int, string> IndexBuilder { get; set; }
	}
}
