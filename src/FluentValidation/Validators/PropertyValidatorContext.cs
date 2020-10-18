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
	using Internal;

	public interface IPropertyValidatorContext {
		IValidationContext ParentContext { get; }
		IValidationRule Rule { get; }
		string PropertyName { get; }
		string DisplayName { get; }
		object InstanceToValidate { get; }
		MessageFormatter MessageFormatter { get; }
		object PropertyValue { get; }
	}

	public class PropertyValidatorContext<T,TProperty> : IPropertyValidatorContext {
		private MessageFormatter _messageFormatter;
		private TProperty _propertyValue;
		private Lazy<TProperty> _propertyValueAccessor;

		public IValidationContext<T> ParentContext { get; private set; }
		public IValidationRule<T> Rule { get; private set; }
		public string PropertyName { get; private set; }

		public string DisplayName => Rule.GetDisplayName(ParentContext);

		public T InstanceToValidate => ParentContext.InstanceToValidate;
		public MessageFormatter MessageFormatter => _messageFormatter ??= ValidatorOptions.Global.MessageFormatterFactory();

		//Lazily load the property value
		//to allow the delegating validator to cancel validation before value is obtained
		public TProperty PropertyValue
			=> _propertyValueAccessor != null ? _propertyValueAccessor.Value : _propertyValue;

		object IPropertyValidatorContext.PropertyValue => PropertyValue;
		IValidationRule IPropertyValidatorContext.Rule => Rule;
		IValidationContext IPropertyValidatorContext.ParentContext => ParentContext;
		object IPropertyValidatorContext.InstanceToValidate => InstanceToValidate;

		public PropertyValidatorContext(IValidationContext<T> parentContext, IValidationRule<T> rule, string propertyName, TProperty propertyValue) {
			ParentContext = parentContext;
			Rule = rule;
			PropertyName = propertyName;
			_propertyValue = propertyValue;
		}

		public PropertyValidatorContext(IValidationContext<T> parentContext, IValidationRule<T> rule, string propertyName, Lazy<TProperty> propertyValueAccessor) {
			ParentContext = parentContext;
			Rule = rule;
			PropertyName = propertyName;
			_propertyValueAccessor = propertyValueAccessor;
		}
	}

	public class PropertyValidatorContext : PropertyValidatorContext<object, object>, IPropertyValidatorContext {
		public new IValidationContext ParentContext { get; }
		public new IValidationRule Rule { get; }
		public new string PropertyName { get; }
		public new string DisplayName => Rule.GetDisplayName(ParentContext);
		public new object InstanceToValidate => ParentContext.InstanceToValidate;
		public new object PropertyValue { get; }

		public PropertyValidatorContext(IValidationContext parentContext, IValidationRule rule, string propertyName, object propertyValue)
			: base(null, null, propertyName, propertyValue)
		{
			ParentContext = parentContext;
			Rule = rule;
			PropertyValue = propertyValue;
			PropertyName = propertyName;
		}

		internal static PropertyValidatorContext Create<T, TProperty>(PropertyValidatorContext<T, TProperty> realContext) {
			return new PropertyValidatorContext(realContext.ParentContext, realContext.Rule, realContext.PropertyName, realContext.PropertyValue);
		}

		internal PropertyValidatorContext<T, TProperty> ToGeneric<T,TProperty>() {
			return new PropertyValidatorContext<T, TProperty>((IValidationContext<T>) ParentContext, (IValidationRule<T>) Rule, PropertyName, (TProperty) PropertyValue);
		}

	}
}
