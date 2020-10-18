namespace FluentValidation.Validators {
	using System;
	using System.Collections.Generic;
	using System.Threading;
	using System.Threading.Tasks;
	using Internal;
	using Results;

	/// <summary>
	/// Custom validator that allows for manual/direct creation of ValidationFailure instances.
	/// </summary>
	/// <typeparam name="TProperty"></typeparam>
	/// <typeparam name="T"></typeparam>
	public class CustomValidator<T, TProperty> : PropertyValidator<T,TProperty> {
		private readonly Action<TProperty, CustomContext<T,TProperty>> _action;
		private Func<TProperty, CustomContext<T,TProperty>, CancellationToken, Task> _asyncAction;
		private readonly bool _isAsync;

		/// <summary>
		/// Creates a new instance of the CustomValidator
		/// </summary>
		/// <param name="action"></param>
		public CustomValidator(Action<TProperty, CustomContext<T,TProperty>> action) {
			_isAsync = false;
			_action = action;
		}

		/// <summary>
		/// Creates a new instance of the CustomValidator.
		/// </summary>
		/// <param name="asyncAction"></param>
		public CustomValidator(Func<TProperty, CustomContext<T,TProperty>, CancellationToken, Task> asyncAction) {
			_isAsync = true;
			_asyncAction = asyncAction;
		}

		public override IEnumerable<ValidationFailure> Validate(PropertyValidatorContext<T,TProperty> context) {
			var customContext = new CustomContext<T,TProperty>(context);
			_action(context.PropertyValue, customContext);
			return customContext.Failures;
		}

		public override async Task<IEnumerable<ValidationFailure>> ValidateAsync(PropertyValidatorContext<T,TProperty> context, CancellationToken cancellation) {
			var customContext = new CustomContext<T,TProperty>(context);
			await _asyncAction(context.PropertyValue, customContext, cancellation);
			return customContext.Failures;
		}

		protected sealed override bool IsValid(PropertyValidatorContext<T,TProperty> context)
			=> throw new NotImplementedException();

		public override bool ShouldValidateAsynchronously(IValidationContext context) {
			return _isAsync;
		}
	}

	/// <summary>
	/// Custom validation context
	/// </summary>
	public class CustomContext<T,TProperty> {
		private PropertyValidatorContext<T,TProperty> _context;
		private List<ValidationFailure> _failures = new List<ValidationFailure>();

		/// <summary>
		/// Creates a new CustomContext
		/// </summary>
		/// <param name="context">The parent PropertyValidatorContext that represents this execution</param>
		public CustomContext(PropertyValidatorContext<T,TProperty> context) {
			_context = context;
		}

		/// <summary>
		/// Adds a new validation failure.
		/// </summary>
		/// <param name="propertyName">The property name</param>
		/// <param name="errorMessage">The error message</param>
		public void AddFailure(string propertyName, string errorMessage) {
			if (errorMessage == null) throw new ArgumentNullException(nameof(errorMessage), "An error message must be specified when calling AddFailure.");
			AddFailure(new ValidationFailure(propertyName ?? string.Empty, errorMessage));
		}

		/// <summary>
		/// Adds a new validation failure (the property name is inferred)
		/// </summary>
		/// <param name="errorMessage">The error message</param>
		public void AddFailure(string errorMessage) {
			if (errorMessage == null) throw new ArgumentNullException(nameof(errorMessage), "An error message must be specified when calling AddFailure.");
			AddFailure(_context.PropertyName, errorMessage);
		}

		/// <summary>
		/// Adds a new validation failure
		/// </summary>
		/// <param name="failure">The failure to add</param>
		public void AddFailure(ValidationFailure failure) {
			if (failure == null) throw new ArgumentNullException(nameof(failure));
			_failures.Add(failure);
		}

		internal IEnumerable<ValidationFailure> Failures => _failures;

		public IValidationRule<T> Rule => _context.Rule;
		public string PropertyName => _context.PropertyName;
		public string DisplayName => _context.DisplayName;
		public MessageFormatter MessageFormatter => _context.MessageFormatter;
		public T InstanceToValidate => _context.InstanceToValidate;
		public TProperty PropertyValue => _context.PropertyValue;
		public IValidationContext<T> ParentContext => _context.ParentContext;
	}
}
