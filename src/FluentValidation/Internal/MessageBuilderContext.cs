namespace FluentValidation.Internal {
	using System;
	using Resources;
	using Validators;

	public class MessageBuilderContext {
		private IPropertyValidatorContext _innerContext;

		public MessageBuilderContext(IPropertyValidatorContext innerContext, IPropertyValidator propertyValidator) {
			_innerContext = innerContext;
			PropertyValidator = propertyValidator;
		}

		public IPropertyValidator PropertyValidator { get; }

		public IValidationContext ParentContext => _innerContext.ParentContext;

		public IValidationRule Rule => _innerContext.Rule;

		public string PropertyName => _innerContext.PropertyName;

		public string DisplayName => _innerContext.DisplayName;

		public MessageFormatter MessageFormatter => _innerContext.MessageFormatter;

		public object InstanceToValidate => _innerContext.InstanceToValidate;
		public object PropertyValue => _innerContext.PropertyValue;

		public string GetDefaultMessage() {
			return PropertyValidator.GetErrorMessage(_innerContext);
		}
	}
}
