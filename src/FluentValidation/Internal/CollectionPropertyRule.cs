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

namespace FluentValidation.Internal {
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Linq.Expressions;
	using System.Reflection;
	using Validators;

	/// <summary>
	/// Rule definition for collection properties
	/// </summary>
	/// <typeparam name="TElement"></typeparam>
	/// <typeparam name="T"></typeparam>
	internal class CollectionPropertyRule<T, TElement> : PropertyRule<T, IEnumerable<TElement>>, ICollectionRule<T, TElement>, ITransformable<T, TElement> {

		/// <summary>
		/// Initializes new instance of the CollectionPropertyRule class
		/// </summary>
		/// <param name="member"></param>
		/// <param name="propertyFunc"></param>
		/// <param name="expression"></param>
		/// <param name="cascadeModeThunk"></param>
		public CollectionPropertyRule(MemberInfo member, Func<T, IEnumerable<TElement>> propertyFunc, LambdaExpression expression, Func<CascadeMode> cascadeModeThunk) : base(member, propertyFunc, expression, cascadeModeThunk) {
			static TElement NoTransform(T _, TElement element) => element;
			Executor = new CollectionRuleExecutor<T, TElement, TElement>(this, NoTransform);
		}

		public override Type TypeToValidate => typeof(TElement);

		/// <summary>
		/// Filter that should include/exclude items in the collection.
		/// </summary>
		public Func<TElement, bool> Filter { get; set; }

		/// <summary>
		/// Constructs the indexer in the property name associated with the error message.
		/// By default this is "[" + index + "]"
		/// </summary>
		public Func<T, IEnumerable<TElement>, TElement, int, string> IndexBuilder { get; set; }

		/// <summary>
		/// Creates a new property rule from a lambda expression.
		/// </summary>
		public static CollectionPropertyRule<T, TElement> Create(Expression<Func<T, IEnumerable<TElement>>> expression, Func<CascadeMode> cascadeModeThunk) {
			var member = expression.GetMember();
			var compiled = expression.Compile();

			return new CollectionPropertyRule<T, TElement>(member, compiled, expression, cascadeModeThunk);
		}

		IValidationRule<T, TTransformed> ITransformable<T, TElement>.Transform<TTransformed>(Func<T, TElement, TTransformed> transformer) {
			TTransformed Transformer(T instance, TElement collectionElement)
				=> transformer(instance, collectionElement);

			Executor = new CollectionRuleExecutor<T, TElement, TTransformed>(this, Transformer);
			return new TransformedRule<T, TElement, TTransformed>(this, transformer);
		}

		public new IPropertyValidator<T,TElement> CurrentValidator
			=> (IPropertyValidator<T, TElement>) Validators.LastOrDefault();
	}

}
