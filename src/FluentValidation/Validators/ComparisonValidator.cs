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
	using System.Reflection;

	internal class ComparisonValidator<T, TProperty, TComparison> : PropertyValidator<T,TProperty>, IComparisonValidator {
		private readonly Func<PropertyValidatorContext<T, TProperty>, (bool Success, TComparison ComparisonValue)> _predicate;
		private readonly string _comparisonMemberDisplayName;

		public ComparisonValidator(Comparison comparison, TProperty valueToCompare, Func<PropertyValidatorContext<T,TProperty>, (bool Success, TComparison ComparisonValue)> predicate) {
			_predicate = predicate;
			Comparison = comparison;
			ValueToCompareToCompare = valueToCompare;
		}

		public ComparisonValidator(Comparison comparison, MemberInfo member, string memberDisplayName, Func<PropertyValidatorContext<T,TProperty>, (bool Success, TComparison ComparisonValue)> predicate) {
			Comparison = comparison;
			MemberToCompare = member;
			_comparisonMemberDisplayName = memberDisplayName;
			_predicate = predicate;
		}

		public Comparison Comparison { get; }
		public MemberInfo MemberToCompare { get; }
		public TProperty ValueToCompareToCompare { get; }
		object IComparisonValidator.ValueToCompare => ValueToCompareToCompare;

		protected override bool IsValid(PropertyValidatorContext<T, TProperty> context) {
			if(context.PropertyValue == null) {
				// If we're working with a nullable type then this rule should not be applied.
				// If you want to ensure that it's never null then a NotNull rule should also be applied.
				return true;
			}

			var result = _predicate(context);;

			if (!result.Success) {
				context.MessageFormatter.AppendArgument("ComparisonValue", result.ComparisonValue);
				context.MessageFormatter.AppendArgument("ComparisonProperty", _comparisonMemberDisplayName ?? "");
			}

			return result.Success;
		}

		protected override string GetDefaultMessageTemplate() {
			return Comparison switch {
				Comparison.Equal => Localized("EqualValidator"),
				Comparison.NotEqual => Localized("NotEqualValidator"),
				Comparison.GreaterThan => Localized("GreaterThanValidator"),
				Comparison.GreaterThanOrEqual => Localized("GreaterThanOrEqualValidator"),
				Comparison.LessThan => Localized("LessThanValidator"),
				Comparison.LessThanOrEqual => Localized("LessThanOrEqualValidator"),
				_ => null
			};
		}
	}

	/// <summary>
	/// Defines a comparison validator
	/// </summary>
	public interface IComparisonValidator : IPropertyValidator {
		/// <summary>
		/// Metadata- the comparison type
		/// </summary>
		Comparison Comparison { get; }
		/// <summary>
		/// Metadata- the member being compared
		/// </summary>
		MemberInfo MemberToCompare { get; }
		/// <summary>
		/// Metadata- the value being compared
		/// </summary>
		object ValueToCompare { get; }
	}

#pragma warning disable 1591
	public enum Comparison {
		Equal,
		NotEqual,
		LessThan,
		GreaterThan,
		GreaterThanOrEqual,
		LessThanOrEqual
	}
#pragma warning restore 1591

}
