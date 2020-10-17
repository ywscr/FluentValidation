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
	using Results;
	using Validators;

	/// <summary>
	/// Rule definition for collection properties
	/// </summary>
	/// <typeparam name="TElement"></typeparam>
	/// <typeparam name="T"></typeparam>
	public class CollectionPropertyRule<T, TElement> : PropertyRule<T, IEnumerable<TElement>>, IValidationRule<T, TElement>, ITransformable<T, TElement> {

		/// <summary>
		/// Initializes new instance of the CollectionPropertyRule class
		/// </summary>
		/// <param name="member"></param>
		/// <param name="propertyFunc"></param>
		/// <param name="expression"></param>
		/// <param name="cascadeModeThunk"></param>
		public CollectionPropertyRule(MemberInfo member, Func<T, IEnumerable<TElement>> propertyFunc, LambdaExpression expression, Func<CascadeMode> cascadeModeThunk) : base(member, propertyFunc, expression, cascadeModeThunk) {
			static TElement NoTransform(T _, TElement element) => element;
			ValidationFunction = context => ValidateCollection(context, NoTransform);
			AsyncValidationFunction = (context, cancel) => ValidateCollectionAsync(context, NoTransform, cancel);
		}

		public override Type TypeToValidate => typeof(TElement);

		/// <summary>
		/// Filter that should include/exclude items in the collection.
		/// </summary>
		public Func<TElement, bool> Filter { get; set; }

		/// <summary>
		/// Constructs the indexer in the property name associated with the error message.
		/// By default this is "[" + index + "]"
		/// </summary>
		public Func<T, IEnumerable<TElement>, TElement, int, string> IndexBuilder { get; set; }

		/// <summary>
		/// Creates a new property rule from a lambda expression.
		/// </summary>
		public static CollectionPropertyRule<T, TElement> Create(Expression<Func<T, IEnumerable<TElement>>> expression, Func<CascadeMode> cascadeModeThunk) {
			var member = expression.GetMember();
			var compiled = expression.Compile();

			return new CollectionPropertyRule<T, TElement>(member, compiled, expression, cascadeModeThunk);
		}

		internal IEnumerable<ValidationFailure> ValidateCollection<TValue>(IValidationContext<T> context, Func<T, TElement, TValue> transformFunc) {
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

			if (HasCondition) {
				if (!Condition(context)) {
					return Enumerable.Empty<ValidationFailure>();
				}
			}

			// TODO: For FV 9, throw an exception by default if synchronous validator has async condition.
			if (HasAsyncCondition) {
				if (! AsyncCondition(context, default).GetAwaiter().GetResult()) {
					return Enumerable.Empty<ValidationFailure>();
				}
			}

			var cascade = CascadeMode;
			var failures = new List<ValidationFailure>();
			var accessor = new Lazy<IEnumerable<TElement>>(() => PropertyFunc(context.InstanceToValidate), LazyThreadSafetyMode.None);

			// Invoke each validator and collect its results.
			foreach (var validator in Validators) {
				IEnumerable<ValidationFailure> results;
				if (validator.ShouldValidateAsynchronously(context))
					//TODO: For FV 9 by default disallow invocation of async validators when running synchronously.
					results = InvokePropertyValidatorAsync(context, validator, propertyName, accessor, transformFunc, default).GetAwaiter().GetResult();
				else
					results = InvokePropertyValidator(context, validator, propertyName, accessor, transformFunc);

				bool hasFailure = false;

				foreach (var result in results) {
					failures.Add(result);
					hasFailure = true;
				}

				// If there has been at least one failure, and our CascadeMode has been set to StopOnFirst
				// then don't continue to the next rule
#pragma warning disable 618
				if (hasFailure && (cascade == CascadeMode.StopOnFirstFailure || cascade == CascadeMode.Stop)) {
#pragma warning restore 618
					break;
				}
			}

			if (failures.Count > 0) {
				// Callback if there has been at least one property validator failed.
				OnFailure?.Invoke(context.InstanceToValidate, failures);
			}
			else {
				foreach (var dependentRule in DependentRules) {
					failures.AddRange(dependentRule.Validate(context));
				}
			}

			return failures;
		}

		internal async Task<IEnumerable<ValidationFailure>> ValidateCollectionAsync<TValue>(IValidationContext<T> context, Func<T, TElement, TValue> transformFunc, CancellationToken cancellation) {
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

			if (HasCondition) {
				if (!Condition(context)) {
					return Enumerable.Empty<ValidationFailure>();
				}
			}

			if (HasAsyncCondition) {
				if (! await AsyncCondition(context, cancellation)) {
					return Enumerable.Empty<ValidationFailure>();
				}
			}

			var cascade = CascadeMode;
			var failures = new List<ValidationFailure>();
			var accessor = new Lazy<IEnumerable<TElement>>(() => PropertyFunc(context.InstanceToValidate), LazyThreadSafetyMode.None);

			// Invoke each validator and collect its results.
			foreach (var validator in Validators) {
				cancellation.ThrowIfCancellationRequested();

				IEnumerable<ValidationFailure> results;
				if (validator.ShouldValidateAsynchronously(context))
					results = await InvokePropertyValidatorAsync(context, validator, propertyName, accessor, transformFunc, cancellation);
				else
					results = InvokePropertyValidator(context, validator, propertyName, accessor, transformFunc);

				bool hasFailure = false;

				foreach (var result in results) {
					failures.Add(result);
					hasFailure = true;
				}

				// If there has been at least one failure, and our CascadeMode has been set to StopOnFirst
				// then don't continue to the next rule
#pragma warning disable 618
				if (hasFailure && (cascade == CascadeMode.StopOnFirstFailure || cascade == CascadeMode.Stop)) {
#pragma warning restore 618
					break;
				}
			}

			if (failures.Count > 0) {
				// Callback if there has been at least one property validator failed.
				OnFailure?.Invoke(context.InstanceToValidate, failures);
			}
			else {
				failures.AddRange(await RunDependentRulesAsync(context, cancellation));
			}

			return failures;
		}

		private async Task<IEnumerable<ValidationFailure>> InvokePropertyValidatorAsync<TValue>(IValidationContext<T> context, IPropertyValidator validator, string propertyName, Lazy<IEnumerable<TElement>> accessor, Func<T, TElement, TValue> transformer, CancellationToken cancellation) {
			if (string.IsNullOrEmpty(propertyName)) {
				propertyName = InferPropertyName(Expression);
			}

			if (!validator.Options.InvokeCondition(context)) return Enumerable.Empty<ValidationFailure>();
			if (!await validator.Options.InvokeAsyncCondition(context, cancellation)) return Enumerable.Empty<ValidationFailure>();

			var collectionPropertyValue = accessor.Value;

			if (collectionPropertyValue != null) {
				if (string.IsNullOrEmpty(propertyName)) {
					throw new InvalidOperationException("Could not automatically determine the property name ");
				}

				var actualContext = ValidationContext<T>.GetFromNonGenericContext(context);

				var validatorTasks = collectionPropertyValue.Select(async (element, index) => {
					if (Filter != null && !Filter(element)) {
						return Enumerable.Empty<ValidationFailure>();
					}

					string indexer = index.ToString();
					bool useDefaultIndexFormat = true;

					if (IndexBuilder != null) {
						indexer = IndexBuilder(context.InstanceToValidate, collectionPropertyValue, element, index);
						useDefaultIndexFormat = false;
					}

					ValidationContext<T> newContext = actualContext.CloneForChildCollectionValidator(actualContext.InstanceToValidate, preserveParentContext: true);
					newContext.PropertyChain.Add(propertyName);
					newContext.PropertyChain.AddIndexer(indexer, useDefaultIndexFormat);

					var valueToValidate = transformer(context.InstanceToValidate, element);
					var newPropertyContext = new PropertyValidatorContext(newContext, this, newContext.PropertyChain.ToString(), valueToValidate);
					newPropertyContext.MessageFormatter.AppendArgument("CollectionIndex", index);
					return await validator.ValidateAsync(newPropertyContext, cancellation);
				});

				var results = new List<ValidationFailure>();

				foreach (var task in validatorTasks) {
					var failures = await task;
					results.AddRange(failures);
				}

				return results;
			}

			return Enumerable.Empty<ValidationFailure>();
		}

		private IEnumerable<Results.ValidationFailure> InvokePropertyValidator<TValue>(IValidationContext<T> context, IPropertyValidator validator, string propertyName, Lazy<IEnumerable<TElement>> accessor, Func<T, TElement, TValue> transformer) {
			if (string.IsNullOrEmpty(propertyName)) {
				propertyName = InferPropertyName(Expression);
			}

			if (!validator.Options.InvokeCondition(context)) return Enumerable.Empty<ValidationFailure>();
			// There's no need to check for the AsyncCondition here. If the validator has an async condition, then
			// the parent PropertyRule will call InvokePropertyValidatorAsync instead.

			var results = new List<ValidationFailure>();
			var collectionPropertyValue = accessor.Value;

			int count = 0;

			if (collectionPropertyValue != null) {
				if (string.IsNullOrEmpty(propertyName)) {
					throw new InvalidOperationException("Could not automatically determine the property name ");
				}

				var actualContext = ValidationContext<T>.GetFromNonGenericContext(context);

				foreach (var element in collectionPropertyValue) {
					int index = count++;

					if (Filter != null && !Filter(element)) {
						continue;
					}

					string indexer = index.ToString();
					bool useDefaultIndexFormat = true;

					if (IndexBuilder != null) {
						indexer = IndexBuilder(context.InstanceToValidate, collectionPropertyValue, element, index);
						useDefaultIndexFormat = false;
					}

					ValidationContext<T> newContext = actualContext.CloneForChildCollectionValidator(actualContext.InstanceToValidate, preserveParentContext: true);
					newContext.PropertyChain.Add(propertyName);
					newContext.PropertyChain.AddIndexer(indexer, useDefaultIndexFormat);

					var valueToValidate = transformer(context.InstanceToValidate, element);
					var newPropertyContext = new PropertyValidatorContext(newContext, this, newContext.PropertyChain.ToString(), valueToValidate);
					newPropertyContext.MessageFormatter.AppendArgument("CollectionIndex", index);
					results.AddRange(validator.Validate(newPropertyContext));
				}
			}

			return results;
		}

		private string InferPropertyName(LambdaExpression expression) {
			var paramExp = expression.Body as ParameterExpression;

			if (paramExp == null) {
				throw new InvalidOperationException("Could not infer property name for expression: " + expression + ". Please explicitly specify a property name by calling OverridePropertyName as part of the rule chain. Eg: RuleForEach(x => x).NotNull().OverridePropertyName(\"MyProperty\")");
			}

			return paramExp.Name;
		}

		IValidationRule<T, TTransformed> ITransformable<T, TElement>.Transform<TTransformed>(Func<T, TElement, TTransformed> transformer) {
			TTransformed Transformer(T instance, TElement collectionElement)
				=> transformer(instance, collectionElement);

			ValidationFunction = context => ValidateCollection(context, Transformer);
			AsyncValidationFunction = (context, cancel) =>  ValidateCollectionAsync(context, Transformer, cancel);
			return new TransformedRule<T, TElement, TTransformed>(this, transformer);
		}
	}
}
