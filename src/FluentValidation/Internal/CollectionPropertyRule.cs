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

	internal class CollectionRuleExecutor<T, TElement> : RuleExecutor<T, IEnumerable<TElement>, TElement> {
		public CollectionRuleExecutor(Func<T, IEnumerable<TElement>> propertyFunc) : base(propertyFunc) {
		}


		private static string InferPropertyName(LambdaExpression expression) {
			var paramExp = expression.Body as ParameterExpression;

			if (paramExp == null) {
				throw new InvalidOperationException("Could not infer property name for expression: " + expression + ". Please explicitly specify a property name by calling OverridePropertyName as part of the rule chain. Eg: RuleForEach(x => x).NotNull().OverridePropertyName(\"MyProperty\")");
			}

			return paramExp.Name;
		}

		private protected override async Task<IEnumerable<ValidationFailure>> InvokePropertyValidatorAsync(ValidationContext<T> context, PropertyRule<T> rule, IPropertyValidator<T, TElement> validator, string propertyName, Lazy<IEnumerable<TElement>> accessor, CancellationToken cancellation) {
			var collectionRule = (CollectionPropertyRule<T, TElement>) rule;

			if (string.IsNullOrEmpty(propertyName)) {
				propertyName = InferPropertyName(rule.Expression);
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
					if (collectionRule.Filter != null && !collectionRule.Filter(element)) {
						return Enumerable.Empty<ValidationFailure>();
					}

					string indexer = index.ToString();
					bool useDefaultIndexFormat = true;

					if (collectionRule.IndexBuilder != null) {
						indexer = collectionRule.IndexBuilder(context.InstanceToValidate, collectionPropertyValue, element, index);
						useDefaultIndexFormat = false;
					}

					ValidationContext<T> newContext = actualContext.CloneForChildCollectionValidator(actualContext.InstanceToValidate, preserveParentContext: true);
					newContext.PropertyChain.Add(propertyName);
					newContext.PropertyChain.AddIndexer(indexer, useDefaultIndexFormat);

					// if (collectionRule.Transformer != null) {
					// valueToValidate = collectionRule.Transformer(element);
					// }

					var newPropertyContext = new PropertyValidatorContext<T, TElement>(newContext, rule, newContext.PropertyChain.ToString(), element);
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

		private protected override IEnumerable<ValidationFailure> InvokePropertyValidator(ValidationContext<T> context, PropertyRule<T> rule, IPropertyValidator<T, TElement> validator, string propertyName, Lazy<IEnumerable<TElement>> accessor) {
			var collectionRule = (CollectionPropertyRule<T, TElement>) rule;


			if (!validator.Options.InvokeCondition(context)) return Enumerable.Empty<ValidationFailure>();
			// There's no need to check for the AsyncCondition here. If the validator has an async condition, then
			// the parent PropertyRule will call InvokePropertyValidatorAsync instead.

			if (string.IsNullOrEmpty(propertyName)) {
				propertyName = InferPropertyName(collectionRule.Expression);
			}

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

					if (collectionRule.Filter != null && !collectionRule.Filter(element)) {
						continue;
					}

					string indexer = index.ToString();
					bool useDefaultIndexFormat = true;

					if (collectionRule.IndexBuilder != null) {
						indexer = collectionRule.IndexBuilder(context.InstanceToValidate, collectionPropertyValue, element, index);
						useDefaultIndexFormat = false;
					}

					ValidationContext<T> newContext = actualContext.CloneForChildCollectionValidator(actualContext.InstanceToValidate, preserveParentContext: true);
					newContext.PropertyChain.Add(propertyName);
					newContext.PropertyChain.AddIndexer(indexer, useDefaultIndexFormat);

					// if (Transformer != null) {
					// valueToValidate = Transformer(element);
					// }

					var newPropertyContext = new PropertyValidatorContext<T, TElement>(newContext, rule, newContext.PropertyChain.ToString(), element);
					newPropertyContext.MessageFormatter.AppendArgument("CollectionIndex", index);
					results.AddRange(validator.Validate(newPropertyContext));
				}
			}

			return results;
		}

		private protected override Task<IEnumerable<ValidationFailure>> InvokeLegacyPropertyValidatorAsync(ValidationContext<T> context, PropertyRule<T> rule, IPropertyValidator validator, string propertyName, Lazy<object> accessor, CancellationToken cancellation) {
			throw new NotImplementedException();
		}

		private protected override IEnumerable<ValidationFailure> InvokeLegacyPropertyValidator(ValidationContext<T> context, PropertyRule<T> rule, IPropertyValidator validator, string propertyName, Lazy<object> accessor) {
			throw new NotImplementedException();
		}
	}

	/// <summary>
	/// Rule definition for collection properties
	/// </summary>
	/// <typeparam name="TElement"></typeparam>
	/// <typeparam name="T"></typeparam>
	public class CollectionPropertyRule<T, TElement> : PropertyRule<T> {
		internal CollectionPropertyRule(MemberInfo member, IRuleExecutor<T> executor, LambdaExpression expression, Func<CascadeMode> cascadeModeThunk, Type typeToValidate, Type containerType)
			: base(member, executor, expression, cascadeModeThunk, typeToValidate) {
		}

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
			//TODO: Expression caching.
			var compiled = expression.Compile();
			return new CollectionPropertyRule<T, TElement>(member, new CollectionRuleExecutor<T, TElement>(compiled), expression, cascadeModeThunk, typeof(TElement), typeof(T));
		}
	}
}
