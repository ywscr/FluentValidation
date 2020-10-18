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
	using System.Linq.Expressions;
	using System.Threading;
	using System.Threading.Tasks;
	using Internal;
	using Resources;

	/// <summary>
	/// Defines a property validator that can be run asynchronously.
	/// </summary>
	public abstract class AsyncPropertyValidator<T,TProperty> : PropertyValidator<T,TProperty> {
		protected AsyncPropertyValidator(string errorMessage)
			: base(errorMessage) {
		}

		protected AsyncPropertyValidator() { }

		public sealed override bool ShouldValidateAsynchronously(IValidationContext context)
			=> true;

		protected sealed override bool IsValid(PropertyValidatorContext<T,TProperty> context)
			=> throw new NotSupportedException($"Async validator {GetType().Name} cannot be invoked synchronously.");

		protected abstract override Task<bool> IsValidAsync(PropertyValidatorContext<T,TProperty> context, CancellationToken cancellation);
	}
}
