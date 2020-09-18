namespace FluentValidation.Internal {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Results;
	using Validators;

	internal interface IRuleExecutor<T> {
		void Execute(ValidationContext<T> context, PropertyRule<T> rule, string propertyName, List<ValidationFailure> failures);
		Task ExecuteAsync(ValidationContext<T> context, PropertyRule<T> rule, string propertyName, List<ValidationFailure> failures, CancellationToken cancellation);
	}

	internal class RuleExecutor<T,TProperty> : IRuleExecutor<T> {
		private Func<T, TProperty> _propertyFunc;

		public RuleExecutor(Func<T, TProperty> propertyFunc) {
			_propertyFunc = propertyFunc;
		}

		public virtual void Execute(ValidationContext<T> context, PropertyRule<T> rule, string propertyName, List<ValidationFailure> failures) {
			var cascade = rule.CascadeMode;
			var accessor = new Lazy<TProperty>(() => _propertyFunc(context.InstanceToValidate), LazyThreadSafetyMode.None);

			// Invoke each validator and collect its results.
			foreach (var validator in rule.Validators) {
				IEnumerable<ValidationFailure> results;

				if (validator is IPropertyValidator<T, TProperty> p) {
					// If it's one of the newer generic validators, make sure the generic code path is used.
					if (p.ShouldValidateAsynchronously(context)) {
						results = InvokePropertyValidatorAsync(context, p, propertyName, accessor, default).GetAwaiter().GetResult();
					}
					else {
						results = InvokePropertyValidator(context, p, propertyName, accessor);
					}
				}
				else {
					if (validator.ShouldValidateAsynchronously(context)) {
						results = InvokeLegacyPropertyValidatorAsync(context, validator, propertyName, accessor, default).GetAwaiter().GetResult();
					}
					else {
						results = InvokeLegacyPropertyValidator(context, validator, propertyName, accessor);
					}
				}

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
		}

		public virtual async Task ExecuteAsync(ValidationContext<T> context, PropertyRule<T> rule, string propertyName, List<ValidationFailure> failures, CancellationToken cancellation) {
			var cascade = rule.CascadeMode;
			var accessor = new Lazy<TProperty>(() => _propertyFunc(context.InstanceToValidate), LazyThreadSafetyMode.None);

			// Invoke each validator and collect its results.
			foreach (var validator in rule.Validators) {
				cancellation.ThrowIfCancellationRequested();
				IEnumerable<ValidationFailure> results;

				if (validator is IPropertyValidator<T, TProperty> p) {
					if (p.ShouldValidateAsynchronously(context)) {
						results = await InvokePropertyValidatorAsync(context, p, propertyName, accessor, cancellation);
					}
					else {
						results = InvokePropertyValidator(context, p, propertyName, accessor);
					}

				}
				else {
					if (validator.ShouldValidateAsynchronously(context)) {
						results = await InvokeLegacyPropertyValidatorAsync(context, validator, propertyName, accessor, cancellation);
					}
					else {
						results = InvokeLegacyPropertyValidator(context, validator, propertyName, accessor);
					}
				}


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
		}

		private protected virtual async Task<IEnumerable<ValidationFailure>> InvokePropertyValidatorAsync(ValidationContext<T> context, IPropertyValidator<T,TProperty> validator, string propertyName, Lazy<TProperty> accessor, CancellationToken cancellation) {
			var propertyContext = new PropertyValidatorContext<T,TProperty>(context, _rule, propertyName, accessor);
			if (!validator.Options.InvokeCondition(propertyContext)) return Enumerable.Empty<ValidationFailure>();
			if (!await validator.Options.InvokeAsyncCondition(propertyContext, cancellation)) return Enumerable.Empty<ValidationFailure>();
			return await validator.ValidateAsync(propertyContext, cancellation);
		}

		private protected virtual IEnumerable<ValidationFailure> InvokePropertyValidator(ValidationContext<T> context, IPropertyValidator<T,TProperty> validator, string propertyName, Lazy<TProperty> accessor) {
			var propertyContext = new PropertyValidatorContext<T,TProperty>(context, _rule, propertyName, accessor);
			if (!validator.Options.InvokeCondition(propertyContext)) return Enumerable.Empty<ValidationFailure>();
			return validator.Validate(propertyContext);
		}

		private protected virtual async Task<IEnumerable<ValidationFailure>> InvokeLegacyPropertyValidatorAsync(ValidationContext<T> context, IPropertyValidator validator, string propertyName, Lazy<TProperty> accessor, CancellationToken cancellation) {
			var propertyContext = new PropertyValidatorContext(context, _rule, propertyName, accessor);
			if (!validator.Options.InvokeCondition(propertyContext)) return Enumerable.Empty<ValidationFailure>();
			if (!await validator.Options.InvokeAsyncCondition(propertyContext, cancellation)) return Enumerable.Empty<ValidationFailure>();
			return await validator.ValidateAsync(propertyContext, cancellation);
		}

		private protected virtual IEnumerable<ValidationFailure> InvokeLegacyPropertyValidator(ValidationContext<T> context, IPropertyValidator validator, string propertyName, Lazy<TProperty> accessor) {
			var propertyContext = new PropertyValidatorContext(context, _rule, propertyName, accessor);
			if (!validator.Options.InvokeCondition(propertyContext)) return Enumerable.Empty<ValidationFailure>();
			return validator.Validate(propertyContext);
		}

	}
}
