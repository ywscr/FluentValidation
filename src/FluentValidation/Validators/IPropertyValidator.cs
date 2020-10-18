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
	using System.Threading;
	using System.Threading.Tasks;
	using Resources;
	using Results;

	public interface IPropertyValidator<T, TProperty> : IPropertyValidator {
		/// <summary>
		/// Performs validation
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		IEnumerable<ValidationFailure> Validate(PropertyValidatorContext<T,TProperty> context);

		/// <summary>
		/// Performs validation asynchronously.
		/// </summary>
		/// <param name="context"></param>
		/// <param name="cancellation"></param>
		/// <returns></returns>
		Task<IEnumerable<ValidationFailure>> ValidateAsync(PropertyValidatorContext<T,TProperty> context, CancellationToken cancellation);

		/// <summary>
		/// Adds a condition for this validator. If there's already a condition, they're combined together with an AND.
		/// </summary>
		/// <param name="condition"></param>
		public void ApplyCondition(Func<IValidationContext<T>, bool> condition);

		/// <summary>
		/// Adds a condition for this validator. If there's already a condition, they're combined together with an AND.
		/// </summary>
		/// <param name="condition"></param>
		public void ApplyAsyncCondition(Func<IValidationContext<T>, CancellationToken, Task<bool>> condition);

		/// <summary>
		/// Function used to retrieve custom state for the validator
		/// </summary>
		Func<PropertyValidatorContext<T,TProperty>, object> CustomStateProvider { get; set; }

		/// <summary>
		/// Function used to retrieve the severity for the validator
		/// </summary>
		Func<PropertyValidatorContext<T,TProperty>, Severity> SeverityProvider { get; set; }

		/// <summary>
		/// Retrieves the error code.
		/// </summary>
		string ErrorCode { get; set; }

		/// <summary>
		/// Sets the overridden error message template for this validator.
		/// </summary>
		/// <param name="errorFactory">A function for retrieving the error message template.</param>
		void SetErrorMessage(Func<PropertyValidatorContext<T, TProperty>, string> errorFactory);

		/// <summary>
		/// Sets the overridden error message template for this validator.
		/// </summary>
		/// <param name="errorMessage">The error message to set</param>
		void SetErrorMessage(string errorMessage);

		/// <summary>
		/// Configuration options.
		/// </summary>
		IPropertyValidator<T, TProperty> Options { get; }

		bool InvokeCondition(IValidationContext<T> context);
		Task<bool> InvokeAsyncCondition(IValidationContext<T> context, CancellationToken token);
	}

	/// <summary>
	/// A custom property validator.
	/// This interface should not be implemented directly in your code as it is subject to change.
	/// Please inherit from <see cref="PropertyValidator">PropertyValidator</see> instead.
	/// </summary>
	public interface IPropertyValidator {
		/// <summary>
		/// Whether or not this validator has a condition associated with it.
		/// </summary>
		public bool HasCondition { get; }

		/// <summary>
		/// Whether or not this validator has an async condition associated with it.
		/// </summary>
		public bool HasAsyncCondition { get; }

		/// <summary>
		/// Determines whether this validator should be run asynchronously or not.
		/// </summary>
		/// <param name="context"></param>
		/// <returns></returns>
		bool ShouldValidateAsynchronously(IValidationContext context);

		/// <summary>
		/// Gets the error message. If a context is supplied, it will be used to format the message if it has placeholders.
		/// If no context is supplied, the raw unformatted message will be returned, containing placeholders.
		/// </summary>
		/// <param name="context">The current property validator context.</param>
		/// <returns>Either the formatted or unformatted error message.</returns>
		string GetErrorMessage(IPropertyValidatorContext context);
	}

}
