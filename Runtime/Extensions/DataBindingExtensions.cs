using System;
using System.Linq.Expressions;
using Unity.Properties;
using UnityEngine.UIElements;

namespace TCS.UiToolkitUtils {
    public static class DataBindingExtensions {
        /// <summary>
        /// Configures a <see cref="DataBinding"/> instance with the specified property name, binding mode, 
        /// and update trigger. This method sets up the data source path, binding mode, and update trigger 
        /// for the binding.
        /// </summary>
        /// <param name="binding">The <see cref="DataBinding"/> instance to configure.</param>
        /// <param name="propertyName">The name of the property to bind to.</param>
        /// <param name="mode">The <see cref="BindingMode"/> to use for the binding (e.g., TwoWay, OneWay).</param>
        /// <param name="updateTrigger">
        /// The <see cref="BindingUpdateTrigger"/> that determines when the binding updates. 
        /// Defaults to <see cref="BindingUpdateTrigger.OnSourceChanged"/>.
        /// </param>
        /// <returns>The configured <see cref="DataBinding"/> instance.</returns>
        /// <code>
        ///   var binding = new DataBinding()
        ///          .Configure
        ///        (
        ///          nameOf(someDataSource.Property), 
        ///          BindingMode.TwoWay
        ///        );
        /// </code>
        public static DataBinding Configure
        (
            this DataBinding binding,
            string propertyName,
            BindingMode mode = BindingMode.ToTarget,
            BindingUpdateTrigger updateTrigger = BindingUpdateTrigger.OnSourceChanged
        ) {
            // Sets the data source path using the property name.
            binding.dataSourcePath = PropertyPath.FromName( propertyName );
            // Sets the binding mode (e.g., TwoWay, OneWay).
            binding.bindingMode = mode;
            // Sets the update trigger for the binding.
            binding.updateTrigger = updateTrigger;
            // Returns the configured DataBinding instance.
            return binding;
        }

        /// <summary>
        /// Configures a <see cref="DataBinding"/> instance with a property selector, binding mode, 
        /// and update trigger. This method sets up the data source path, binding mode, and update trigger 
        /// for the binding using a lambda expression to specify the property.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property being bound.</typeparam>
        /// <param name="binding">The <see cref="DataBinding"/> instance to configure.</param>
        /// <param name="propertySelector">
        /// A lambda expression that selects the property to bind to (e.g., () => MyProperty).
        /// </param>
        /// <param name="mode">The <see cref="BindingMode"/> to use for the binding (e.g., TwoWay, OneWay).</param>
        /// <param name="updateTrigger">
        /// The <see cref="BindingUpdateTrigger"/> that determines when the binding updates. 
        /// Defaults to <see cref="BindingUpdateTrigger.OnSourceChanged"/>.
        /// </param>
        /// <returns>The configured <see cref="DataBinding"/> instance.</returns>
        /// <code>
        ///   var binding = new DataBinding()
        ///          .Configure
        ///        (
        ///           () => someDataSource.Property, 
        ///           BindingMode.TwoWay
        ///        );
        /// </code>
        public static DataBinding Configure<TProperty>(
            this DataBinding binding,
            Expression<Func<TProperty>> propertySelector,
            BindingMode mode = BindingMode.ToTarget,
            BindingUpdateTrigger updateTrigger = BindingUpdateTrigger.OnSourceChanged
        ) {
            // Sets the data source type using the type of the property being bound.
            binding.dataSourceType = typeof(TProperty);
            // Sets the data source path using the property name derived from the lambda expression.
            binding.dataSourcePath = PropertyPath.FromName( GetMemberName( propertySelector ) );
            // Sets the binding mode (e.g., TwoWay, OneWay).
            binding.bindingMode = mode;
            // Sets the update trigger for the binding.
            binding.updateTrigger = updateTrigger;
            // Returns the configured DataBinding instance.
            return binding;
        }

        /// <summary>
        /// Configures a <see cref="DataBinding"/> instance with a data source, property selector, 
        /// binding mode, and update trigger. This method sets up the data source, data source path, 
        /// binding mode, and update trigger for the binding.
        /// </summary>
        /// <typeparam name="TProperty">The type of the property being bound.</typeparam>
        /// <param name="binding">The <see cref="DataBinding"/> instance to configure.</param>
        /// <param name="source">The data source object to bind to.</param>
        /// <param name="propertySelector">
        /// A lambda expression that selects the property to bind to (e.g., () => MyProperty).
        /// </param>
        /// <param name="mode">
        /// The <see cref="BindingMode"/> to use for the binding (e.g., TwoWay, OneWay). 
        /// Defaults to <see cref="BindingMode.ToTarget"/>.
        /// </param>
        /// <param name="updateTrigger">
        /// The <see cref="BindingUpdateTrigger"/> that determines when the binding updates. 
        /// Defaults to <see cref="BindingUpdateTrigger.OnSourceChanged"/>.
        /// </param>
        /// <returns>The configured <see cref="DataBinding"/> instance.</returns>
        /// <code>
        ///   var binding = new DataBinding()
        ///          .Configure
        ///        (
        ///           someDataSource, 
        ///           () => someDataSource.Property, 
        ///          BindingMode.TwoWay
        ///        );
        /// </code>
        public static DataBinding Configure<TProperty>(
            this DataBinding binding,
            object source,
            Expression<Func<TProperty>> propertySelector,
            BindingMode mode = BindingMode.ToTarget,
            BindingUpdateTrigger updateTrigger = BindingUpdateTrigger.OnSourceChanged
        ) {
            // Sets the data source for the binding.
            binding.dataSource = source;
            // Sets the data source type using the type of the provided source object.
            binding.dataSourceType = source.GetType();
            // Sets the data source path using the property name derived from the lambda expression.
            binding.dataSourcePath = PropertyPath.FromName( GetMemberName( propertySelector ) );
            // Sets the binding mode (e.g., TwoWay, OneWay).
            binding.bindingMode = mode;
            // Sets the update trigger for the binding.
            binding.updateTrigger = updateTrigger;
            // Returns the configured DataBinding instance.
            return binding;
        }

        /// <summary>
        /// Extracts the name of the member (property) from a lambda expression.
        /// </summary>
        /// <param name="expr">The lambda expression representing the property access.</param>
        /// <returns>The name of the member (property) accessed in the expression.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the expression does not represent simple property access.
        /// </exception>
        static string GetMemberName(LambdaExpression expr) {
            // Handles different expression types to extract the member name.
            return expr.Body switch {
                // If the body is a MemberExpression, return the member's name.
                MemberExpression member => member.Member.Name,
                // If the body is a UnaryExpression wrapping a MemberExpression, return the member's name.
                UnaryExpression { Operand: MemberExpression unaryMember } => unaryMember.Member.Name,
                // Otherwise, throw an exception indicating the expression is invalid.
                _ => throw new ArgumentException( "Expression must be a simple property access", nameof(expr) ),
            };
        }
    }
}