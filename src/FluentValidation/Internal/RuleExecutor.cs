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
	using System.Threading;
	using System.Threading.Tasks;
	using Results;
	using Validators;

	internal class RuleExecutor<T, TProperty, TValue> : IRuleExecutor<T> {
		private PropertyRule<T, TProperty> _rule;
		private Func<T, TValue> _propertyValueAccessor;

		public RuleExecutor(PropertyRule<T, TProperty> rule, Func<T, TValue> propertyValueAccessor) {
			_rule = rule;
			_propertyValueAccessor = propertyValueAccessor;
		}

		public virtual IEnumerable<ValidationFailure> Validate(IValidationContext<T> context) {
			string displayName = _rule.GetDisplayName(context);

			if (_rule.PropertyName == null && displayName == null) {
				//No name has been specified. Assume this is a model-level rule, so we should use empty string instead.
				displayName = string.Empty;
			}

			// Construct the full name of the property, taking into account overriden property names and the chain (if we're in a nested validator)
			string propertyName = context.PropertyChain.BuildPropertyName(_rule.PropertyName ?? displayName);

			// Ensure that this rule is allowed to run.
			// The validatselector has the opportunity to veto this before any of the validators execute.
			if (!context.Selector.CanExecute(_rule, propertyName, context)) {
				return Enumerable.Empty<ValidationFailure>();
			}

			if (_rule.HasCondition) {
				if (!_rule.Condition(context)) {
					return Enumerable.Empty<ValidationFailure>();
				}
			}

			if (_rule.HasAsyncCondition) {
				if (! _rule.AsyncCondition(context, default).GetAwaiter().GetResult()) {
					return Enumerable.Empty<ValidationFailure>();
				}
			}

			var cascade = _rule.CascadeMode;
			var failures = new List<ValidationFailure>();
			var accessor = new Lazy<TValue>(() => _propertyValueAccessor(context.InstanceToValidate), LazyThreadSafetyMode.None);

			// Invoke each validator and collect its results.
			foreach (var validator in _rule.Validators) {
				if (validator.ShouldValidateAsynchronously(context)) {
					failures.AddRange(InvokePropertyValidatorAsync(context, validator, propertyName, accessor, default).GetAwaiter().GetResult());
				}
				else {
					failures.AddRange(InvokePropertyValidator(context, validator, propertyName, accessor));
				}

				// If there has been at least one failure, and our CascadeMode has been set to StopOnFirst
				// then don't continue to the next rule
#pragma warning disable 618
				if (failures.Count > 0 && (cascade == CascadeMode.StopOnFirstFailure || cascade == CascadeMode.Stop)) {
					break;
				}
#pragma warning restore 618
			}

			if (failures.Count > 0) {
				// Callback if there has been at least one property validator failed.
				_rule.OnFailure?.Invoke(context.InstanceToValidate, failures);
			}
			else {
				foreach (var dependentRule in _rule.DependentRules) {
					failures.AddRange(dependentRule.Validate(context));
				}
			}

			return failures;

		}

		public virtual async Task<IEnumerable<ValidationFailure>> ValidateAsync(IValidationContext<T> context, CancellationToken cancellation) {
			if (!context.IsAsync()) {
				context.RootContextData["__FV_IsAsyncExecution"] = true;
			}

			string displayName = _rule.GetDisplayName(context);

			if (_rule.PropertyName == null && displayName == null) {
				//No name has been specified. Assume this is a model-level rule, so we should use empty string instead.
				displayName = string.Empty;
			}

			// Construct the full name of the property, taking into account overriden property names and the chain (if we're in a nested validator)
			string propertyName = context.PropertyChain.BuildPropertyName(_rule.PropertyName ?? displayName);

			// Ensure that this rule is allowed to run.
			// The validatselector has the opportunity to veto this before any of the validators execute.
			if (!context.Selector.CanExecute(_rule, propertyName, context)) {
				return Enumerable.Empty<ValidationFailure>();
			}

			if (_rule.HasCondition) {
				if (!_rule.Condition(context)) {
					return Enumerable.Empty<ValidationFailure>();
				}
			}

			if (_rule.HasAsyncCondition) {
				if (! await _rule.AsyncCondition(context, cancellation)) {
					return Enumerable.Empty<ValidationFailure>();
				}
			}

			var cascade = _rule.CascadeMode;
			var failures = new List<ValidationFailure>();
			var accessor = new Lazy<TValue>(() => _propertyValueAccessor(context.InstanceToValidate), LazyThreadSafetyMode.None);

			// Invoke each validator and collect its results.
			foreach (var validator in _rule.Validators) {
				cancellation.ThrowIfCancellationRequested();

				if (validator.ShouldValidateAsynchronously(context)) {
					failures.AddRange(await InvokePropertyValidatorAsync(context, validator, propertyName, accessor, cancellation));
				}
				else {
					failures.AddRange(InvokePropertyValidator(context, validator, propertyName, accessor));
				}

				// If there has been at least one failure, and our CascadeMode has been set to StopOnFirst
				// then don't continue to the next rule
#pragma warning disable 618
				if (failures.Count > 0 && (cascade == CascadeMode.StopOnFirstFailure || cascade == CascadeMode.Stop)) {
					break;
				}
#pragma warning restore 618
			}

			if (failures.Count > 0) {
				// Callback if there has been at least one property validator failed.
				_rule.OnFailure?.Invoke(context.InstanceToValidate, failures);
			}
			else {
				foreach (var dependentRule in _rule.DependentRules) {
					cancellation.ThrowIfCancellationRequested();
					failures.AddRange(await dependentRule.ValidateAsync(context, cancellation));
				}
			}

			return failures;
		}

		public void ApplyCondition(Func<IValidationContext<T>, bool> predicate, ApplyConditionTo applyConditionTo = ApplyConditionTo.AllValidators) {
			// Default behaviour for When/Unless as of v1.3 is to apply the condition to all previous validators in the chain.
			// TODO: Remove the currying once PropertyValidators are generic.
			if (applyConditionTo == ApplyConditionTo.AllValidators) {
				foreach (var validator in _rule.Validators) {
					validator.Options.ApplyCondition(ctx=> predicate(ValidationContext<T>.GetFromNonGenericContext(ctx)));
				}

				foreach (var dependentRule in _rule.DependentRules) {
					dependentRule.ApplyCondition(predicate, applyConditionTo);
				}
			}
			else {
				_rule.CurrentValidator.Options.ApplyCondition(ctx=> predicate(ValidationContext<T>.GetFromNonGenericContext(ctx)));
			}
		}

		public void ApplyAsyncCondition(Func<IValidationContext<T>, CancellationToken, Task<bool>> predicate, ApplyConditionTo applyConditionTo = ApplyConditionTo.AllValidators) {
			// Default behaviour for When/Unless as of v1.3 is to apply the condition to all previous validators in the chain.
			// TODO: Remove the currying once PropertyValidators are generic.
			if (applyConditionTo == ApplyConditionTo.AllValidators) {
				foreach (var validator in _rule.Validators) {
					validator.Options.ApplyAsyncCondition((ctx, c) => predicate(ValidationContext<T>.GetFromNonGenericContext(ctx), c));
				}

				foreach (var dependentRule in _rule.DependentRules) {
					dependentRule.ApplyAsyncCondition(predicate, applyConditionTo);
				}
			}
			else {
				_rule.CurrentValidator.Options.ApplyAsyncCondition((ctx, c) => predicate(ValidationContext<T>.GetFromNonGenericContext(ctx), c));
			}

		}

		private async Task<IEnumerable<ValidationFailure>> InvokePropertyValidatorAsync(IValidationContext<T> context, IPropertyValidator validator, string propertyName, Lazy<TValue> accessor, CancellationToken cancellation) {
			if (!validator.Options.InvokeCondition(context)) return Enumerable.Empty<ValidationFailure>();
			if (!await validator.Options.InvokeAsyncCondition(context, cancellation)) return Enumerable.Empty<ValidationFailure>();
			var propertyContext = new PropertyValidatorContext(context, _rule, propertyName, accessor.Value);
			return await validator.ValidateAsync(propertyContext, cancellation);
		}

		private IEnumerable<ValidationFailure> InvokePropertyValidator(IValidationContext<T> context, IPropertyValidator validator, string propertyName, Lazy<TValue> accessor) {
			if (!validator.Options.InvokeCondition(context)) return Enumerable.Empty<ValidationFailure>();
			var propertyContext = new PropertyValidatorContext(context, _rule, propertyName, accessor.Value);
			return validator.Validate(propertyContext);
		}

	}
}
