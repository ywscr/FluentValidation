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

		public void ApplyCondition(PropertyRule<T> rule, Func<ValidationContext<T>, bool> predicate, ApplyConditionTo applyConditionTo);

		public void ApplyAsyncCondition(PropertyRule<T> rule, Func<ValidationContext<T>, CancellationToken, Task<bool>> predicate, ApplyConditionTo applyConditionTo);
	}

	internal class RuleExecutor<T, TProperty> : RuleExecutor<T, TProperty, TProperty> {
		public RuleExecutor(Func<T, TProperty> propertyFunc) : base(propertyFunc) {
		}

		private protected override async Task<IEnumerable<ValidationFailure>> InvokePropertyValidatorAsync(ValidationContext<T> context, PropertyRule<T> rule, IPropertyValidator<T,TProperty> validator, string propertyName, Lazy<TProperty> accessor, CancellationToken cancellation) {
			if (!validator.Options.InvokeCondition(context)) return Enumerable.Empty<ValidationFailure>();
			if (!await validator.Options.InvokeAsyncCondition(context, cancellation)) return Enumerable.Empty<ValidationFailure>();
			var propertyContext = new PropertyValidatorContext<T,TProperty>(context, rule, propertyName, accessor);
			return await validator.ValidateAsync(propertyContext, cancellation);
		}

		private protected override IEnumerable<ValidationFailure> InvokePropertyValidator(ValidationContext<T> context, PropertyRule<T> rule, IPropertyValidator<T,TProperty> validator, string propertyName, Lazy<TProperty> accessor) {
			if (!validator.Options.InvokeCondition(context)) return Enumerable.Empty<ValidationFailure>();
			var propertyContext = new PropertyValidatorContext<T,TProperty>(context, rule, propertyName, accessor);
			return validator.Validate(propertyContext);
		}
	}

	internal abstract class RuleExecutor<T,TProperty, TValue> : IRuleExecutor<T> {
		protected Func<T, TProperty> PropertyFunc { get; }

		public RuleExecutor(Func<T, TProperty> propertyFunc) {
			PropertyFunc = propertyFunc;
		}

		public virtual void Execute(ValidationContext<T> context, PropertyRule<T> rule, string propertyName, List<ValidationFailure> failures) {
			var cascade = rule.CascadeMode;
			var accessor = GetAccessor(context);

			// Invoke each validator and collect its results.
			foreach (var validator in rule.Validators) {
				IEnumerable<ValidationFailure> results;

				if (validator is IPropertyValidator<T, TValue> p) {
					// If it's one of the newer generic validators, make sure the generic code path is used.
					if (p.ShouldValidateAsynchronously(context)) {
						results = InvokePropertyValidatorAsync(context, rule, p, propertyName, accessor, default).GetAwaiter().GetResult();
					}
					else {
						results = InvokePropertyValidator(context, rule, p, propertyName, accessor);
					}
				}
				else {
					// TODO: What should happen here??
					throw new NotSupportedException();
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
			var accessor = GetAccessor(context);

			// Invoke each validator and collect its results.
			foreach (var validator in rule.Validators) {
				cancellation.ThrowIfCancellationRequested();
				IEnumerable<ValidationFailure> results;

				if (validator is IPropertyValidator<T, TValue> p) {
					if (p.ShouldValidateAsynchronously(context)) {
						results = await InvokePropertyValidatorAsync(context, rule, p, propertyName, accessor, cancellation);
					}
					else {
						results = InvokePropertyValidator(context, rule, p, propertyName, accessor);
					}

				}
				else {
					// TODO: what should happen here?
					throw new NotSupportedException();
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

		private protected abstract Task<IEnumerable<ValidationFailure>> InvokePropertyValidatorAsync(ValidationContext<T> context, PropertyRule<T> rule, IPropertyValidator<T, TValue> validator, string propertyName, Lazy<TProperty> accessor, CancellationToken cancellation);

		private protected abstract IEnumerable<ValidationFailure> InvokePropertyValidator(ValidationContext<T> context, PropertyRule<T> rule, IPropertyValidator<T, TValue> validator, string propertyName, Lazy<TProperty> accessor);

		protected Lazy<TProperty> GetAccessor(ValidationContext<T> context) {
			return new Lazy<TProperty>(() => PropertyFunc(context.InstanceToValidate), LazyThreadSafetyMode.None);
		}

		/// <summary>
		/// Applies a condition to the rule
		/// </summary>
		/// <param name="predicate"></param>
		/// <param name="applyConditionTo"></param>
		public void ApplyCondition(PropertyRule<T> rule, Func<ValidationContext<T>, bool> predicate, ApplyConditionTo applyConditionTo = ApplyConditionTo.AllValidators) {
			// Default behaviour for When/Unless as of v1.3 is to apply the condition to all previous validators in the chain.
			if (applyConditionTo == ApplyConditionTo.AllValidators) {
				foreach (var validator in rule.Validators) {
					if (validator is IPropertyValidator<T, TProperty> v) {
						v.Options.ApplyCondition(predicate);
					}
					else {
						// validator.Options.ApplyCondition(context => predicate(ValidationContext<T>.GetFromNonGenericContext(context)));
					}
				}
			}
			else {
				var current = rule.Validators.LastOrDefault();
				if (current != null) {
					if (current is IPropertyValidator<T, TProperty> v) {
						v.Options.ApplyCondition(predicate);
					}
					else {
						// current.Options.ApplyCondition(context => predicate(ValidationContext<T>.GetFromNonGenericContext(context)));
					}
				}
			}
		}

		/// <summary>
		/// Applies the condition to the rule asynchronously
		/// </summary>
		/// <param name="predicate"></param>
		/// <param name="applyConditionTo"></param>
		public void ApplyAsyncCondition(PropertyRule<T> rule, Func<ValidationContext<T>, CancellationToken, Task<bool>> predicate, ApplyConditionTo applyConditionTo = ApplyConditionTo.AllValidators) {
			// Default behaviour for When/Unless as of v1.3 is to apply the condition to all previous validators in the chain.
			if (applyConditionTo == ApplyConditionTo.AllValidators) {
				foreach (var validator in rule.Validators) {
					if (validator is IPropertyValidator<T, TProperty> v) {
						v.Options.ApplyAsyncCondition(predicate);
					}
					else {
						// validator.Options.ApplyAsyncCondition((context, ct) => predicate(ValidationContext<T>.GetFromNonGenericContext(context), ct));
					}

				}
			}
			else {
				var current = rule.Validators.LastOrDefault();
				if (current != null) {
					if (current is IPropertyValidator<T, TProperty> v) {
						v.Options.ApplyAsyncCondition(predicate);
					}
					else {
						// current.Options.ApplyAsyncCondition((context, ct) => predicate(ValidationContext<T>.GetFromNonGenericContext(context), ct));
					}
				}
			}
		}

	}
}
