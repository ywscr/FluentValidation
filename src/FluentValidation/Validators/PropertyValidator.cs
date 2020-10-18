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

namespace FluentValidation.Validators {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Internal;
	using Results;

	/*internal class LegacyValidatorWrapper<T, TProperty> : IPropertyValidator<T, TProperty> {
		private PropertyValidator _inner;

		public LegacyValidatorWrapper(PropertyValidator inner) {
			_inner = inner;
		}

		public bool ShouldValidateAsynchronously(IValidationContext context) {
			return _inner.ShouldValidateAsynchronously(context);
		}

		public IEnumerable<ValidationFailure> Validate(PropertyValidatorContext<T, TProperty> context) {
			return _inner.Validate(PropertyValidatorContext.Create(context));
		}

		public Task<IEnumerable<ValidationFailure>> ValidateAsync(PropertyValidatorContext<T, TProperty> context, CancellationToken cancellation) {
			return _inner.ValidateAsync(PropertyValidatorContext.Create(context), cancellation);
		}

		public bool HasCondition => _inner.HasCondition;

		public bool HasAsyncCondition => _inner.HasAsyncCondition;

		public void ApplyCondition(Func<IValidationContext<T>, bool> condition) {
			_inner.ApplyCondition(context => {
				return condition((IValidationContext<T>) context);
			});
		}

		public void ApplyAsyncCondition(Func<IValidationContext<T>, CancellationToken, Task<bool>> condition) {
			_inner.ApplyAsyncCondition((context, cancel) => {
				return condition((IValidationContext<T>) context.ParentContext, cancel);
			});
		}

		private Func<PropertyValidatorContext<T, TProperty>, object> _realStateProvider;

		public Func<PropertyValidatorContext<T, TProperty>, object> CustomStateProvider {
			get => _realStateProvider;
			set {
				_realStateProvider = value;
				_inner.CustomStateProvider = context => {
					return _realSeverityProvider(((PropertyValidatorContext) context).ToGeneric<T, TProperty>());
				};
			}
		}

		private Func<PropertyValidatorContext<T, TProperty>, Severity> _realSeverityProvider;

		public Func<PropertyValidatorContext<T, TProperty>, Severity> SeverityProvider {
			get => _realSeverityProvider;
			set {
				_realSeverityProvider = value;
				_inner.SeverityProvider = context => {
					return _realSeverityProvider(((PropertyValidatorContext) context).ToGeneric<T, TProperty>());
				};
			}
		}

		public string ErrorCode {
			get => _inner.ErrorCode;
			set => _inner.ErrorCode = value;
		}

		public string GetErrorMessage(PropertyValidatorContext<T, TProperty> context) {
			return _inner.GetErrorMessage(PropertyValidatorContext.Create(context));
		}

		public void SetErrorMessage(Func<PropertyValidatorContext<T, TProperty>, string> errorFactory) {
			_inner.SetErrorMessage(context => {
				return errorFactory(((PropertyValidatorContext) context).ToGeneric<T, TProperty>());
			});
		}

		public void SetErrorMessage(string errorMessage) {
			_inner.SetErrorMessage(errorMessage);
		}

		public IPropertyValidator<T, TProperty> Options => this;
		public bool InvokeCondition(IValidationContext<T> context) {
			return _inner.InvokeCondition(context);
		}

		public Task<bool> InvokeAsyncCondition(IValidationContext<T> context, CancellationToken token) {
			return _inner.InvokeAsyncCondition(context);
		}
	}

	public abstract class PropertyValidator : PropertyValidator<object, object> {
		public PropertyValidator(string errorMessage) : base(errorMessage) {
		}

		public PropertyValidator() {
		}

		protected sealed override bool IsValid(PropertyValidatorContext<object, object> context) {
			return IsValid((PropertyValidatorContext)context);
		}

		protected sealed override Task<bool> IsValidAsync(PropertyValidatorContext<object, object> context, CancellationToken cancellation) {
			return IsValidAsync((PropertyValidatorContext)context, cancellation);
		}

		protected abstract bool IsValid(PropertyValidatorContext context);

		protected virtual Task<bool> IsValidAsync(PropertyValidatorContext context, CancellationToken cancellation) {
			return Task.FromResult(IsValid(context));
		}

		public bool InvokeCondition(IValidationContext context) {
			if (_condition != null) {
				return _condition(context);
			}

			return true;
		}

		public async Task<bool> InvokeAsyncCondition(IValidationContext context, CancellationToken token) {
			if (_asyncCondition != null) {
				return await _asyncCondition(context, token);
			}

			return true;
		}
	}*/

	public abstract class PropertyValidator<T,TProperty> : IPropertyValidator<T,TProperty> {
		private string _errorMessage;
		private Func<PropertyValidatorContext<T,TProperty>, string> _errorMessageFactory;
		private Func<IValidationContext<T>, bool> _condition;
		private Func<IValidationContext<T>, CancellationToken, Task<bool>> _asyncCondition;

		/// <inheritdoc />
		IPropertyValidator<T,TProperty> IPropertyValidator<T, TProperty>.Options => this;

		protected PropertyValidator(string errorMessage) {
			SetErrorMessage(errorMessage);
		}

		protected PropertyValidator() {
		}

		/// <summary>
		/// Whether or not this validator has a condition associated with it.
		/// </summary>
		public bool HasCondition => _condition != null;

		/// <summary>
		/// Whether or not this validator has an async condition associated with it.
		/// </summary>
		public bool HasAsyncCondition => _asyncCondition != null;

		/// <summary>
		/// Adds a condition for this validator. If there's already a condition, they're combined together with an AND.
		/// </summary>
		/// <param name="condition"></param>
		public void ApplyCondition(Func<IValidationContext<T>, bool> condition) {
			if (_condition == null) {
				_condition = condition;
			}
			else {
				var original = _condition;
				_condition = ctx => condition(ctx) && original(ctx);
			}
		}

		/// <summary>
		/// Adds a condition for this validator. If there's already a condition, they're combined together with an AND.
		/// </summary>
		/// <param name="condition"></param>
		public void ApplyAsyncCondition(Func<IValidationContext<T>, CancellationToken, Task<bool>> condition) {
			if (_asyncCondition == null) {
				_asyncCondition = condition;
			}
			else {
				var original = _asyncCondition;
				_asyncCondition = async (ctx, ct) => await condition(ctx, ct) && await original(ctx, ct);
			}
		}

		public bool InvokeCondition(IValidationContext<T> context) {
			if (_condition != null) {
				return _condition(context);
			}

			return true;
		}

		public async Task<bool> InvokeAsyncCondition(IValidationContext<T> context, CancellationToken token) {
			if (_asyncCondition != null) {
				return await _asyncCondition(context, token);
			}

			return true;
		}

		/// <summary>
		/// Function used to retrieve custom state for the validator
		/// </summary>
		public Func<PropertyValidatorContext<T,TProperty>, object> CustomStateProvider { get; set; }

		/// <summary>
		/// Function used to retrieve the severity for the validator
		/// </summary>
		public Func<PropertyValidatorContext<T,TProperty>, Severity> SeverityProvider { get; set; }

		/// <summary>
		/// Retrieves the error code.
		/// </summary>
		public string ErrorCode { get; set; }

		/// <summary>
		/// Returns the default error message template for this validator, when not overridden.
		/// </summary>
		/// <returns></returns>
		protected virtual string GetDefaultMessageTemplate() => "No default error message has been specified";

		/// <summary>
		/// Gets the error message. If a context is supplied, it will be used to format the message if it has placeholders.
		/// If no context is supplied, the raw unformatted message will be returned, containing placeholders.
		/// </summary>
		/// <param name="context">The current property validator context.</param>
		/// <returns>Either the formatted or unformatted error message.</returns>
		public string GetErrorMessage(IPropertyValidatorContext context) {
			string rawTemplate = _errorMessageFactory?.Invoke(context as PropertyValidatorContext<T, TProperty>) ?? _errorMessage ?? GetDefaultMessageTemplate();

			if (context == null) {
				return rawTemplate;
			}

			return context.MessageFormatter.BuildMessage(rawTemplate);
		}

		/// <summary>
		/// Sets the overridden error message template for this validator.
		/// </summary>
		/// <param name="errorFactory">A function for retrieving the error message template.</param>
		public void SetErrorMessage(Func<PropertyValidatorContext<T,TProperty>, string> errorFactory) {
			_errorMessageFactory = errorFactory;
			_errorMessage = null;
		}

		/// <summary>
		/// Sets the overridden error message template for this validator.
		/// </summary>
		/// <param name="errorMessage">The error message to set</param>
		public void SetErrorMessage(string errorMessage) {
			_errorMessage = errorMessage;
			_errorMessageFactory = null;
		}

		/// <summary>
		/// Retrieves a localized string from the LanguageManager.
		/// If an ErrorCode is defined for this validator, the error code is used as the key.
		/// If no ErrorCode is defined (or the language manager doesn't have a translation for the error code)
		/// then the fallback key is used instead.
		/// </summary>
		/// <param name="fallbackKey">The fallback key to use for translation, if no ErrorCode is available.</param>
		/// <returns>The translated error message template.</returns>
		protected string Localized(string fallbackKey) {
			var errorCode = ErrorCode;

			if (errorCode != null) {
				string result = ValidatorOptions.Global.LanguageManager.GetString(errorCode);

				if (!string.IsNullOrEmpty(result)) {
					return result;
				}
			}

			return ValidatorOptions.Global.LanguageManager.GetString(fallbackKey);
		}


		/// <inheritdoc />
		public virtual IEnumerable<ValidationFailure> Validate(PropertyValidatorContext<T,TProperty> context) {
			if (IsValid(context)) return Enumerable.Empty<ValidationFailure>();

			PrepareMessageFormatterForValidationError(context);
			return new[] { CreateValidationError(context) };

		}

		/// <inheritdoc />
		public virtual async Task<IEnumerable<ValidationFailure>> ValidateAsync(PropertyValidatorContext<T,TProperty> context, CancellationToken cancellation) {
			if (await IsValidAsync(context, cancellation)) return Enumerable.Empty<ValidationFailure>();

			PrepareMessageFormatterForValidationError(context);
			return new[] {CreateValidationError(context)};
		}

		/// <inheritdoc />
		public virtual bool ShouldValidateAsynchronously(IValidationContext context) {
			// If the user has applied an async condition, then always go through the async path
			// even if validator is being run synchronously.
			if (HasAsyncCondition) return true;
			return false;
		}

		protected abstract bool IsValid(PropertyValidatorContext<T,TProperty> context);

#pragma warning disable 1998
		protected virtual async Task<bool> IsValidAsync(PropertyValidatorContext<T,TProperty> context, CancellationToken cancellation) {
			return IsValid(context);
		}
#pragma warning restore 1998

		/// <summary>
		/// Prepares the <see cref="MessageFormatter"/> of <paramref name="context"/> for an upcoming <see cref="ValidationFailure"/>.
		/// </summary>
		/// <param name="context">The validator context</param>
		protected virtual void PrepareMessageFormatterForValidationError(PropertyValidatorContext<T,TProperty> context) {
			context.MessageFormatter.AppendPropertyName(context.DisplayName);
			context.MessageFormatter.AppendPropertyValue(context.PropertyValue);

			// If there's a collection index cached in the root context data then add it
			// to the message formatter. This happens when a child validator is executed
			// as part of a call to RuleForEach. Usually parameters are not flowed through to
			// child validators, but we make an exception for collection indices.
			if (context.ParentContext.RootContextData.TryGetValue("__FV_CollectionIndex", out var index)) {
				// If our property validator has explicitly added a placeholder for the collection index
				// don't overwrite it with the cached version.
				if (!context.MessageFormatter.PlaceholderValues.ContainsKey("CollectionIndex")) {
					context.MessageFormatter.AppendArgument("CollectionIndex", index);
				}
			}
		}

		/// <summary>
		/// Creates an error validation result for this validator.
		/// </summary>
		/// <param name="context">The validator context</param>
		/// <returns>Returns an error validation result.</returns>
		protected virtual ValidationFailure CreateValidationError(PropertyValidatorContext<T,TProperty> context) {
			var error = context.Rule.MessageBuilder != null
				? context.Rule.MessageBuilder(new MessageBuilderContext(context, this))
				: GetErrorMessage(context);

			var failure = new ValidationFailure(context.PropertyName, error, context.PropertyValue);
			failure.FormattedMessagePlaceholderValues = context.MessageFormatter.PlaceholderValues;
			failure.ErrorCode = ErrorCode ?? ValidatorOptions.Global.ErrorCodeResolver(this);

			if (CustomStateProvider != null) {
				failure.CustomState = CustomStateProvider(context);
			}

			if (SeverityProvider != null) {
				failure.Severity = SeverityProvider(context);
			}

			return failure;
		}
	}
}
