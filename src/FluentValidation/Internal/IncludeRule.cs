namespace FluentValidation.Internal {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;
	using Results;
	using Validators;

	/// <summary>
	/// Marker interface indicating an include rule.
	/// </summary>
	public interface IIncludeRule { }

	/// <summary>
	/// Include rule
	/// </summary>
	public class IncludeRule<T> : PropertyRule<T>, IIncludeRule {

		/// <summary>
		/// Creates a new IncludeRule
		/// </summary>
		/// <param name="validator"></param>
		/// <param name="cascadeModeThunk"></param>
		internal IncludeRule(IValidator<T> validator, Func<CascadeMode> cascadeModeThunk)
			: base(null, new RuleExecutor<T,T>(x => x), null, cascadeModeThunk, typeof(T)) {
			AddValidator(new ChildValidatorAdaptor<T,T>(validator, validator.GetType()));
		}

		/// <summary>
		/// Creates a new IncludeRule
		/// </summary>
		/// <param name="func"></param>
		/// <param name="cascadeModeThunk"></param>
		/// <param name="validatorType"></param>
		internal IncludeRule(Func<PropertyValidatorContext<T,T>, IValidator<T>> func,  Func<CascadeMode> cascadeModeThunk, Type validatorType)
			: base(null, new RuleExecutor<T,T>(x => x), null, cascadeModeThunk, typeof(T)) {
			AddValidator(new ChildValidatorAdaptor<T,T>(func,  validatorType));
		}

		/// <summary>
		/// Creates a new include rule from an existing validator
		/// </summary>
		/// <param name="validator"></param>
		/// <param name="cascadeModeThunk"></param>
		/// <returns></returns>
		public static IncludeRule<T> Create(IValidator<T> validator, Func<CascadeMode> cascadeModeThunk) {
			return new IncludeRule<T>(validator, cascadeModeThunk);
		}

		/// <summary>
		/// Creates a new include rule from an existing validator
		/// </summary>
		/// <param name="func"></param>
		/// <param name="cascadeModeThunk"></param>
		/// <typeparam name="TValidator"></typeparam>
		/// <returns></returns>
		public static IncludeRule<T> Create<TValidator>(Func<T, TValidator> func, Func<CascadeMode> cascadeModeThunk)
			where TValidator : IValidator<T> {
			return new IncludeRule<T>(ctx => func(ctx.InstanceToValidate), cascadeModeThunk, typeof(TValidator));
		}


		public override IEnumerable<ValidationFailure> Validate(ValidationContext<T> context) {
			context.RootContextData[MemberNameValidatorSelector.DisableCascadeKey] = true;
			var result = base.Validate(context).ToList();
			context.RootContextData.Remove(MemberNameValidatorSelector.DisableCascadeKey);
			return result;
		}

		public override async Task<IEnumerable<ValidationFailure>> ValidateAsync(ValidationContext<T> context, CancellationToken cancellation) {
			context.RootContextData[MemberNameValidatorSelector.DisableCascadeKey] = true;
			var result = await base.ValidateAsync(context, cancellation);
			result = result.ToList();
			context.RootContextData.Remove(MemberNameValidatorSelector.DisableCascadeKey);
			return result;
		}
	}
}
