﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;

namespace MvvmFoundation.Wpf
{
    /// <summary>
    /// Monitors the PropertyChanged event of an object that implements INotifyPropertyChanged,
    /// and executes callback methods (i.e. handlers) registered for properties of that object.
    /// </summary>
    /// <typeparam name="TPropertySource">The type of object to monitor for property changes.</typeparam>
    public class PropertyObserver<TPropertySource> : IWeakEventListener where TPropertySource : INotifyPropertyChanged
    {
        /// <summary>
        /// Initializes a new instance of PropertyObserver, which observes the 'propertySource' object for property changes.
        /// </summary>
        /// <param name="propertySource">The object to monitor for property changes.</param>
        public PropertyObserver(TPropertySource propertySource)
        {
            if (propertySource == null)
            {
                throw new ArgumentNullException(nameof(propertySource));
            }

            _propertySourceRef = new WeakReference(propertySource);
            _propertyNameToHandlerMap = new Dictionary<string?, Action<TPropertySource>>();
        }

        /// <summary>
        /// Registers a callback to be invoked when the PropertyChanged event has been raised for the specified property.
        /// </summary>
        /// <param name="expression">A lambda expression like 'n => n.PropertyName'.</param>
        /// <param name="handler">The callback to invoke when the property has changed.</param>
        /// <returns>The object on which this method was invoked, to allow for multiple invocations chained together.</returns>
        public PropertyObserver<TPropertySource> RegisterHandler(Expression<Func<TPropertySource, object>> expression, Action<TPropertySource> handler)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            string? propertyName = GetPropertyName(expression);

            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("'expression' did not provide a property name.");
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var propertySource = this.GetPropertySource();

            if (propertySource != null)
            {
                Debug.Assert(!_propertyNameToHandlerMap.ContainsKey(propertyName), $"Why is the '{propertyName}' property being registered again?");

                _propertyNameToHandlerMap[propertyName] = handler;
                PropertyChangedEventManager.AddListener(propertySource, this, propertyName);
            }

            return this;
        }

        /// <summary>
        /// Removes the callback associated with the specified property.
        /// </summary>
        /// <returns>The object on which this method was invoked, to allow for multiple invocations chained together.</returns>
        public PropertyObserver<TPropertySource> UnregisterHandler(Expression<Func<TPropertySource, object>> expression)
        {
            if (expression == null)
            {
                throw new ArgumentNullException(nameof(expression));
            }

            string? propertyName = GetPropertyName(expression);

            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentException("'expression' did not provide a property name.");
            }

            var propertySource = this.GetPropertySource();

            if (propertySource != null)
            {
                if (_propertyNameToHandlerMap.ContainsKey(propertyName))
                {
                    _propertyNameToHandlerMap.Remove(propertyName);
                    PropertyChangedEventManager.RemoveListener(propertySource, this, propertyName);
                }
            }

            return this;
        }

        bool IWeakEventListener.ReceiveWeakEvent(Type managerType, object sender, EventArgs e)
        {
            bool handled = false;

            if (managerType == typeof(PropertyChangedEventManager))
            {
                if (e is PropertyChangedEventArgs args && sender is TPropertySource propertySource)
                {
                    string? propertyName = args.PropertyName;

                    if (string.IsNullOrEmpty(propertyName))
                    {
                        // When the property name is empty, all properties are considered to be invalidated.
                        // Iterate over a copy of the list of handlers, in case a handler is registered by a callback.
                        foreach (var handler in _propertyNameToHandlerMap.Values.ToArray())
                        {
                            handler(propertySource);
                        }

                        handled = true;
                    }
                    else
                    {
                        if (_propertyNameToHandlerMap.TryGetValue(propertyName, out var handler))
                        {
                            handler(propertySource);
                            handled = true;
                        }
                    }
                }
            }

            return handled;
        }

        private static string? GetPropertyName(Expression<Func<TPropertySource, object>> expression)
        {
            var lambda = expression as LambdaExpression;
            MemberExpression? memberExpression;

            if (lambda.Body is UnaryExpression unaryExpression)
            {
                memberExpression = unaryExpression.Operand as MemberExpression;
            }
            else
            {
                memberExpression = lambda.Body as MemberExpression;
            }

            Debug.Assert(memberExpression != null, "Please provide a lambda expression like 'n => n.PropertyName'");

            if (memberExpression != null)
            {
                var propertyInfo = memberExpression.Member as PropertyInfo;
                return propertyInfo?.Name;
            }

            return null;
        }

        private TPropertySource? GetPropertySource()
        {
            try
            {
                return (TPropertySource?)_propertySourceRef.Target;
            }
            catch
            {
                return default;
            }
        }

        private readonly Dictionary<string?, Action<TPropertySource>> _propertyNameToHandlerMap;
        private readonly WeakReference _propertySourceRef;
    }
}