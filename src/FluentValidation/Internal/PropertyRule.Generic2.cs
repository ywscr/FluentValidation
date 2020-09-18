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

namespace FluentValidation.Internal {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;
	using Resources;
	using Results;
	using Validators;

	/// <summary>
	/// Defines a rule associated with a property.
	/// </summary>
	public class PropertyRule<T> : IValidationRule {
		private readonly List<IPropertyValidator> _validators = new List<IPropertyValidator>();
		private Func<CascadeMode> _cascadeModeThunk;
		private string _propertyDisplayName;
		private string _propertyName;
		private string[] _ruleSet = new string[0];
		private Func<ValidationContext<T>, bool> _condition;
		private Func<ValidationContext<T>, CancellationToken, Task<bool>> _asyncCondition;

#pragma warning disable 618
		//TODO: Replace with Func<IValidationContext, string> for FV 10.
		private IStringSource _displayNameSource;
#pragma warning restore 618

		internal IRuleExecutor<T> Executor { get; set; }

		/// <summary>
		/// Condition for all validators in this rule.
		/// </summary>
		public Func<ValidationContext<T>, bool> Condition => _condition;

		/// <summary>
		/// Asynchronous condition for all validators in this rule.
		/// </summary>
		public Func<ValidationContext<T>, CancellationToken, Task<bool>> AsyncCondition => _asyncCondition;

		/// <summary>
		/// Property associated with this rule.
		/// </summary>
		public MemberInfo Member { get; }

		/// <summary>
		/// Expression that was used to create the rule.
		/// </summary>
		public LambdaExpression Expression { get; }

		/// <summary>
		/// String source that can be used to retrieve the display name (if null, falls back to the property name)
		/// </summary>
		[Obsolete("This property is deprecated and will be removed in FluentValidation 10. Use the GetDisplayName and SetDisplayName instead.")]
		public IStringSource DisplayName {
			get => _displayNameSource;
			set => _displayNameSource = value;
		}

		/// <summary>
		/// Sets the display name for the property.
		/// </summary>
		/// <param name="name">The property's display name</param>
		public void SetDisplayName(string name) {
#pragma warning disable 618
			_displayNameSource = new StaticStringSource(name);
#pragma warning restore 618
		}

		/// <summary>
		/// Sets the display name for the property using a function.
		/// </summary>
		/// <param name="factory">The function for building the display name</param>
		public void SetDisplayName(Func<IValidationContext, string> factory) {
			if (factory == null) throw new ArgumentNullException(nameof(factory));
#pragma warning disable 618
			_displayNameSource = new BackwardsCompatibleStringSource<IValidationContext>(factory);
#pragma warning restore 618
		}

		/// <summary>
		/// Rule set that this rule belongs to (if specified)
		/// </summary>
		public string[] RuleSets {
			get => _ruleSet;
			set => _ruleSet = value ?? new string[0];
		}

		/// <summary>
		/// Function that will be invoked if any of the validators associated with this rule fail.
		/// </summary>
		public Action<object, IEnumerable<ValidationFailure>> OnFailure { get; set; }

		/// <summary>
		/// The current validator being configured by this rule.
		/// </summary>
		public IPropertyValidator CurrentValidator => _validators.LastOrDefault();

		/// <summary>
		/// Type of the property being validated
		/// </summary>
		public Type TypeToValidate { get; }

		/// <summary>
		/// Cascade mode for this rule.
		/// </summary>
		public CascadeMode CascadeMode {
			get => _cascadeModeThunk();
			set => _cascadeModeThunk = () => value;
		}

		/// <summary>
		/// Validators associated with this rule.
		/// </summary>
		public IEnumerable<IPropertyValidator> Validators => _validators;

		/// <summary>
		/// Creates a new property rule.
		/// </summary>
		/// <param name="member">Property</param>
		/// <param name="propertyFunc">Function to get the property value</param>
		/// <param name="expression">Lambda expression used to create the rule</param>
		/// <param name="cascadeModeThunk">Function to get the cascade mode.</param>
		/// <param name="typeToValidate">Type to validate</param>
		/// <param name="containerType">Container type that owns the property</param>
		internal PropertyRule(MemberInfo member, IRuleExecutor<T> ruleExecutor, LambdaExpression expression, Func<CascadeMode> cascadeModeThunk, Type typeToValidate) {
			Member = member;
			Executor = ruleExecutor;
			Expression = expression;
			TypeToValidate = typeToValidate;
			_cascadeModeThunk = cascadeModeThunk;

			DependentRules = new List<IValidationRule>();
			PropertyName = ValidatorOptions.Global.PropertyNameResolver(typeof(T), member, expression);
#pragma warning disable 618
			_displayNameSource = new BackwardsCompatibleStringSource<IValidationContext>(context => ValidatorOptions.Global.DisplayNameResolver(typeof(T), member, expression));
#pragma warning restore 618
		}

		/// <summary>
		/// Creates a new property rule from a lambda expression.
		/// </summary>
		public static PropertyRule<T> Create<TProperty>(Expression<Func<T, TProperty>> expression) {
			return Create(expression, () => ValidatorOptions.Global.CascadeMode);
		}

		/// <summary>
		/// Creates a new property rule from a lambda expression.
		/// </summary>
		public static PropertyRule<T> Create<TProperty>(Expression<Func<T, TProperty>> expression, Func<CascadeMode> cascadeModeThunk, bool bypassCache = false) {
			var member = expression.GetMember();
			var compiled = AccessorCache<T>.GetCachedAccessor(member, expression, bypassCache);
			var executor = new RuleExecutor<T,TProperty>(compiled);
			return new PropertyRule<T>(member, executor, expression, cascadeModeThunk, typeof(TProperty));
		}

		/// <summary>
		/// Adds a validator to the rule.
		/// </summary>
		public void AddValidator(IPropertyValidator validator) {
			_validators.Add(validator);
		}

		/// <summary>
		/// Replaces a validator in this rule. Used to wrap validators.
		/// </summary>
		public void ReplaceValidator(IPropertyValidator original, IPropertyValidator newValidator) {
			var index = _validators.IndexOf(original);

			if (index > -1) {
				_validators[index] = newValidator;
			}
		}

		/// <summary>
		/// Remove a validator in this rule.
		/// </summary>
		public void RemoveValidator(IPropertyValidator original) {
			_validators.Remove(original);
		}

		/// <summary>
		/// Clear all validators from this rule.
		/// </summary>
		public void ClearValidators() {
			_validators.Clear();
		}

		/// <summary>
		/// Returns the property name for the property being validated.
		/// Returns null if it is not a property being validated (eg a method call)
		/// </summary>
		public string PropertyName {
			get { return _propertyName; }
			set {
				_propertyName = value;
				_propertyDisplayName = _propertyName.SplitPascalCase();
			}
		}

		/// <summary>
		/// Allows custom creation of an error message
		/// </summary>
		public Func<MessageBuilderContext, string> MessageBuilder { get; set; }

		/// <summary>
		/// Dependent rules
		/// </summary>
		public List<IValidationRule> DependentRules { get; }

		/// <summary>
		/// Display name for the property.
		/// </summary>
		[Obsolete("Calling GetDisplayName without a context parameter is deprecated and will be removed in FluentValidation 10. If you really need this behaviour, you can call the overload that takes a context but pass in null.")]
		public string GetDisplayName() {
			return GetDisplayName(null);
		}

		/// <summary>
		/// Display name for the property.
		/// </summary>
		public string GetDisplayName(ICommonContext context) {
			//TODO: For FV10, change the parameter from ICommonContext to IValidationContext.
			string result = null;

			if (_displayNameSource != null) {
				result = _displayNameSource.GetString(context);
			}

			if (result == null) {
				result = _propertyDisplayName;
			}

			return result;
		}

		/// <summary>
		/// Performs validation using a validation context and returns a collection of Validation Failures.
		/// </summary>
		/// <param name="context">Validation Context</param>
		/// <returns>A collection of validation failures</returns>
		public virtual IEnumerable<ValidationFailure> Validate(ValidationContext<T> context) {
			string displayName = GetDisplayName(context);

			if (PropertyName == null && displayName == null) {
				//No name has been specified. Assume this is a model-level rule, so we should use empty string instead.
				displayName = string.Empty;
			}

			// Construct the full name of the property, taking into account overriden property names and the chain (if we're in a nested validator)
			string propertyName = context.PropertyChain.BuildPropertyName(PropertyName ?? displayName);

			// Ensure that this rule is allowed to run.
			// The validatselector has the opportunity to veto this before any of the validators execute.
			if (!context.Selector.CanExecute(this, propertyName, context)) {
				yield break;
			}

			if (_condition != null) {
				if (!_condition(context)) {
					yield break;
				}
			}

			// TODO: For FV 9, throw an exception by default if synchronous validator has async condition.
			if (_asyncCondition != null) {
				if (!_asyncCondition(context, default).GetAwaiter().GetResult()) {
					yield break;
				}
			}

			var failures = new List<ValidationFailure>();

			Executor.Execute(context, this, propertyName, failures);

			if (failures.Count > 0) {
				// Callback if there has been at least one property validator failed.
				OnFailure?.Invoke(context.InstanceToValidate, failures);
			}
			else {
				foreach (var dependentRule in DependentRules) {
#pragma warning disable 618
					foreach (var failure in dependentRule.Validate(context)) {
#pragma warning restore 618
						yield return failure;
					}
				}
			}
		}

		/// <summary>
		/// Performs asynchronous validation using a validation context and returns a collection of Validation Failures.
		/// </summary>
		/// <param name="context">Validation Context</param>
		/// <param name="cancellation"></param>
		/// <returns>A collection of validation failures</returns>
		public virtual async Task<IEnumerable<ValidationFailure>> ValidateAsync(ValidationContext<T> context, CancellationToken cancellation) {
			if (!context.IsAsync()) {
				context.RootContextData["__FV_IsAsyncExecution"] = true;
			}

			string displayName = GetDisplayName(context);

			if (PropertyName == null && displayName == null) {
				//No name has been specified. Assume this is a model-level rule, so we should use empty string instead.
				displayName = string.Empty;
			}

			// Construct the full name of the property, taking into account overriden property names and the chain (if we're in a nested validator)
			string propertyName = context.PropertyChain.BuildPropertyName(PropertyName ?? displayName);

			// Ensure that this rule is allowed to run.
			// The validatselector has the opportunity to veto this before any of the validators execute.
			if (!context.Selector.CanExecute(this, propertyName, context)) {
				return Enumerable.Empty<ValidationFailure>();
			}

			if (_condition != null) {
				if (!_condition(context)) {
					return Enumerable.Empty<ValidationFailure>();
				}
			}

			if (_asyncCondition != null) {
				if (! await _asyncCondition(context, cancellation)) {
					return Enumerable.Empty<ValidationFailure>();
				}
			}

			var failures = new List<ValidationFailure>();

			await Executor.ExecuteAsync(context, this, propertyName, failures, cancellation);


			if (failures.Count > 0) {
				// Callback if there has been at least one property validator failed.
				OnFailure?.Invoke(context.InstanceToValidate, failures);
			}
			else {
				failures.AddRange(await RunDependentRulesAsync(context, cancellation));
			}

			return failures;
		}

		private async Task<IEnumerable<ValidationFailure>> RunDependentRulesAsync(IValidationContext context, CancellationToken cancellation) {
			var failures = new List<ValidationFailure>();

			foreach (var rule in DependentRules) {
				cancellation.ThrowIfCancellationRequested();
#pragma warning disable 618
				failures.AddRange(await rule.ValidateAsync(context, cancellation));
#pragma warning restore 618
			}

			return failures;
		}

		// TODO: Remove Backwards compatibility methods.
		IEnumerable<ValidationFailure> IValidationRule.Validate(IValidationContext context) {
			return Validate(ValidationContext<T>.GetFromNonGenericContext(context));
		}

		Task<IEnumerable<ValidationFailure>> IValidationRule.ValidateAsync(IValidationContext context, CancellationToken cancellation) {
			return ValidateAsync(ValidationContext<T>.GetFromNonGenericContext(context), cancellation);
		}

		/// <summary>
		/// Applies a condition to the rule
		/// </summary>
		/// <param name="predicate"></param>
		/// <param name="applyConditionTo"></param>
		public void ApplyCondition(Func<PropertyValidatorContext, bool> predicate, ApplyConditionTo applyConditionTo = ApplyConditionTo.AllValidators) {
			// Default behaviour for When/Unless as of v1.3 is to apply the condition to all previous validators in the chain.
			if (applyConditionTo == ApplyConditionTo.AllValidators) {
				foreach (var validator in Validators) {
					validator.Options.ApplyCondition(predicate);
				}

				foreach (var dependentRule in DependentRules) {
					dependentRule.ApplyCondition(predicate, applyConditionTo);
				}
			}
			else {
				CurrentValidator.Options.ApplyCondition(predicate);
			}
		}

		/// <summary>
		/// Applies the condition to the rule asynchronously
		/// </summary>
		/// <param name="predicate"></param>
		/// <param name="applyConditionTo"></param>
		public void ApplyAsyncCondition(Func<PropertyValidatorContext, CancellationToken, Task<bool>> predicate, ApplyConditionTo applyConditionTo = ApplyConditionTo.AllValidators) {
			// Default behaviour for When/Unless as of v1.3 is to apply the condition to all previous validators in the chain.
			if (applyConditionTo == ApplyConditionTo.AllValidators) {
				foreach (var validator in Validators) {
					validator.Options.ApplyAsyncCondition(predicate);
				}

				foreach (var dependentRule in DependentRules) {
					dependentRule.ApplyAsyncCondition(predicate, applyConditionTo);
				}
			}
			else {
				CurrentValidator.Options.ApplyAsyncCondition(predicate);
			}
		}

		public void ApplySharedCondition(Func<IValidationContext, bool> condition) {
			if (_condition == null) {
				_condition = condition;
			}
			else {
				var original = _condition;
				_condition = ctx => condition(ctx) && original(ctx);
			}
		}

		public void ApplySharedAsyncCondition(Func<IValidationContext, CancellationToken, Task<bool>> condition) {
			if (_asyncCondition == null) {
				_asyncCondition = condition;
			}
			else {
				var original = _asyncCondition;
				_asyncCondition = async (ctx, ct) => await condition(ctx, ct) && await original(ctx, ct);
			}

		}
	}
}
