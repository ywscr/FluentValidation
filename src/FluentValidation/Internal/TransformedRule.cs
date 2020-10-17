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
	using System.Linq.Expressions;
	using System.Reflection;
	using System.Threading;
	using System.Threading.Tasks;
	using Results;
	using Validators;

	internal interface ITransformable<T, TValue> {
		IValidationRule<T, TTransformed> Transform<TTransformed>(Func<T, TValue, TTransformed> transformer);
	}

	/// <summary>
	/// Wrapper for PropertyRule instances that handles transforming the property value to another type.
	/// </summary>
	internal class TransformedRule<T, TValue, TTransformed> : IValidationRule<T, TTransformed>, ITransformable<T, TTransformed> {
		private IValidationRule<T, TValue> _inner;
		private Func<T, TValue, TTransformed> _transformer;

		public TransformedRule(IValidationRule<T, TValue> inner, Func<T, TValue, TTransformed> transformer) {
			_inner = inner;
			_transformer = transformer;
		}

		public IValidationRule<T, TNew> Transform<TNew>(Func<T, TTransformed, TNew> transformer) {
			// This is for the rare use case where someone wants to do a double transform.
			TNew Transformer(T instance, TValue value)
				=> transformer(instance, _transformer(instance, value));

			return ((ITransformable<T, TValue>) _inner).Transform(Transformer);
		}

		#region Delegating Members

		public IEnumerable<IPropertyValidator> Validators => _inner.Validators;

		public string[] RuleSets {
			get => _inner.RuleSets;
			set => _inner.RuleSets = value;
		}

		public string GetDisplayName(IValidationContext context) {
			return _inner.GetDisplayName(context);
		}

		public string PropertyName {
			get => _inner.PropertyName;
			set => _inner.PropertyName = value;
		}

		public bool HasCondition => _inner.HasCondition;

		public bool HasAsyncCondition => _inner.HasAsyncCondition;

		public Type TypeToValidate => _inner.TypeToValidate;

		public Func<MessageBuilderContext, string> MessageBuilder {
			get => _inner.MessageBuilder;
			set => _inner.MessageBuilder = value;
		}

		public CascadeMode CascadeMode {
			get => _inner.CascadeMode;
			set => _inner.CascadeMode = value;
		}

		public LambdaExpression Expression => _inner.Expression;

		public MemberInfo Member => _inner.Member;

		public IEnumerable<ValidationFailure> Validate(IValidationContext<T> context) {
			return _inner.Validate(context);
		}

		public Task<IEnumerable<ValidationFailure>> ValidateAsync(IValidationContext<T> context, CancellationToken cancellation) {
			return _inner.ValidateAsync(context, cancellation);
		}

		public void ApplyCondition(Func<IValidationContext<T>, bool> predicate, ApplyConditionTo applyConditionTo = ApplyConditionTo.AllValidators) {
			_inner.ApplyCondition(predicate, applyConditionTo);
		}

		public void ApplyAsyncCondition(Func<IValidationContext<T>, CancellationToken, Task<bool>> predicate, ApplyConditionTo applyConditionTo = ApplyConditionTo.AllValidators) {
			_inner.ApplyAsyncCondition(predicate, applyConditionTo);
		}

		public void ApplySharedCondition(Func<IValidationContext<T>, bool> condition) {
			_inner.ApplySharedCondition(condition);
		}

		public void ApplySharedAsyncCondition(Func<IValidationContext<T>, CancellationToken, Task<bool>> condition) {
			_inner.ApplySharedAsyncCondition(condition);
		}

		public Action<T, IEnumerable<ValidationFailure>> OnFailure {
			get => _inner.OnFailure;
			set => _inner.OnFailure = value;
		}

		public List<IValidationRule<T>> DependentRules => _inner.DependentRules;

		public void AddValidator(IPropertyValidator validator) {
			_inner.AddValidator(validator);
		}

		public void SetDisplayName(string name) {
			_inner.SetDisplayName(name);
		}

		public void SetDisplayName(Func<IValidationContext<T>, string> factory) {
			_inner.SetDisplayName(factory);
		}

		public void ReplaceValidator(IPropertyValidator original, IPropertyValidator newValidator) {
			_inner.ReplaceValidator(original, newValidator);
		}

		public void RemoveValidator(IPropertyValidator original) {
			_inner.RemoveValidator(original);
		}

		public void ClearValidators() {
			_inner.ClearValidators();
		}

		public IPropertyValidator CurrentValidator => _inner.CurrentValidator;

		#endregion

	}
}
