namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Test assertion utilities
    /// </summary>
    public static class TestAssert
    {
        /// <summary>
        /// Asserts that a condition is true
        /// </summary>
        /// <param name="condition">The condition to check</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when condition is false</exception>
        public static void IsTrue(bool condition, string message = "Expected true but got false")
        {
            if (!condition)
            {
                throw new AssertionException(message);
            }
        }

        /// <summary>
        /// Asserts that a condition is false
        /// </summary>
        /// <param name="condition">The condition to check</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when condition is true</exception>
        public static void IsFalse(bool condition, string message = "Expected false but got true")
        {
            if (condition)
            {
                throw new AssertionException(message);
            }
        }

        /// <summary>
        /// Asserts that two values are equal
        /// </summary>
        /// <typeparam name="T">Type of values to compare</typeparam>
        /// <param name="expected">Expected value</param>
        /// <param name="actual">Actual value</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when values are not equal</exception>
        public static void AreEqual<T>(T expected, T actual, string? message = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                string defaultMessage = $"Expected '{expected}' but got '{actual}'";
                throw new AssertionException(message ?? defaultMessage);
            }
        }

        /// <summary>
        /// Asserts that two values are not equal
        /// </summary>
        /// <typeparam name="T">Type of values to compare</typeparam>
        /// <param name="expected">Expected value</param>
        /// <param name="actual">Actual value</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when values are equal</exception>
        public static void AreNotEqual<T>(T expected, T actual, string? message = null)
        {
            if (EqualityComparer<T>.Default.Equals(expected, actual))
            {
                string defaultMessage = $"Expected values to be different but both were '{actual}'";
                throw new AssertionException(message ?? defaultMessage);
            }
        }

        /// <summary>
        /// Asserts that a value is null
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when value is not null</exception>
        public static void IsNull(object? value, string message = "Expected null but got non-null value")
        {
            if (value != null)
            {
                throw new AssertionException(message);
            }
        }

        /// <summary>
        /// Asserts that a value is not null
        /// </summary>
        /// <param name="value">Value to check</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when value is null</exception>
        public static void IsNotNull(object? value, string message = "Expected non-null but got null")
        {
            if (value == null)
            {
                throw new AssertionException(message);
            }
        }

        /// <summary>
        /// Asserts that a collection contains the specified number of items
        /// </summary>
        /// <typeparam name="T">Type of collection items</typeparam>
        /// <param name="collection">Collection to check</param>
        /// <param name="expectedCount">Expected number of items</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when collection count doesn't match</exception>
        public static void CollectionCount<T>(IEnumerable<T> collection, int expectedCount, string? message = null)
        {
            ArgumentNullException.ThrowIfNull(collection);

            int actualCount = collection.Count();
            if (actualCount != expectedCount)
            {
                string defaultMessage = $"Expected collection count {expectedCount} but got {actualCount}";
                throw new AssertionException(message ?? defaultMessage);
            }
        }

        /// <summary>
        /// Asserts that a collection is empty
        /// </summary>
        /// <typeparam name="T">Type of collection items</typeparam>
        /// <param name="collection">Collection to check</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when collection is not empty</exception>
        public static void IsEmpty<T>(IEnumerable<T> collection, string message = "Expected empty collection")
        {
            CollectionCount(collection, 0, message);
        }

        /// <summary>
        /// Asserts that a collection is not empty
        /// </summary>
        /// <typeparam name="T">Type of collection items</typeparam>
        /// <param name="collection">Collection to check</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when collection is empty</exception>
        public static void IsNotEmpty<T>(IEnumerable<T> collection, string message = "Expected non-empty collection")
        {
            ArgumentNullException.ThrowIfNull(collection);

            if (!collection.Any())
            {
                throw new AssertionException(message);
            }
        }

        /// <summary>
        /// Asserts that a value is within the specified range
        /// </summary>
        /// <typeparam name="T">Type that implements IComparable</typeparam>
        /// <param name="value">Value to check</param>
        /// <param name="min">Minimum value (inclusive)</param>
        /// <param name="max">Maximum value (inclusive)</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when value is outside range</exception>
        public static void IsInRange<T>(T value, T min, T max, string? message = null) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0 || value.CompareTo(max) > 0)
            {
                string defaultMessage = $"Expected value between {min} and {max} but got {value}";
                throw new AssertionException(message ?? defaultMessage);
            }
        }

        /// <summary>
        /// Asserts that an action throws an exception of the specified type
        /// </summary>
        /// <typeparam name="TException">Expected exception type</typeparam>
        /// <param name="action">Action that should throw</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <returns>The thrown exception</returns>
        /// <exception cref="AssertionException">Thrown when no exception or wrong exception type is thrown</exception>
        public static TException Throws<TException>(Action action, string? message = null) where TException : Exception
        {
            ArgumentNullException.ThrowIfNull(action);

            try
            {
                action();
                string defaultMessage = $"Expected {typeof(TException).Name} but no exception was thrown";
                throw new AssertionException(message ?? defaultMessage);
            }
            catch (TException ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                string defaultMessage = $"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}";
                throw new AssertionException(message ?? defaultMessage);
            }
        }

        /// <summary>
        /// Asserts that an async action throws an exception of the specified type
        /// </summary>
        /// <typeparam name="TException">Expected exception type</typeparam>
        /// <param name="action">Async action that should throw</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <returns>The thrown exception</returns>
        /// <exception cref="AssertionException">Thrown when no exception or wrong exception type is thrown</exception>
        public static async Task<TException> ThrowsAsync<TException>(Func<Task> action, string? message = null) where TException : Exception
        {
            ArgumentNullException.ThrowIfNull(action);

            try
            {
                await action().ConfigureAwait(false);
                string defaultMessage = $"Expected {typeof(TException).Name} but no exception was thrown";
                throw new AssertionException(message ?? defaultMessage);
            }
            catch (TException ex)
            {
                return ex;
            }
            catch (Exception ex)
            {
                string defaultMessage = $"Expected {typeof(TException).Name} but got {ex.GetType().Name}: {ex.Message}";
                throw new AssertionException(message ?? defaultMessage);
            }
        }

        /// <summary>
        /// Forces a test failure with the specified message
        /// </summary>
        /// <param name="message">The failure message</param>
        /// <exception cref="AssertionException">Always thrown</exception>
        public static void Fail(string message = "Test failed")
        {
            throw new AssertionException(message);
        }

        /// <summary>
        /// Asserts that a value is greater than another value
        /// </summary>
        /// <typeparam name="T">Type that implements IComparable</typeparam>
        /// <param name="actual">Actual value</param>
        /// <param name="expected">Expected minimum value</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when actual is not greater than expected</exception>
        public static void IsGreaterThan<T>(T actual, T expected, string? message = null) where T : IComparable<T>
        {
            if (actual.CompareTo(expected) <= 0)
            {
                string defaultMessage = $"Expected value greater than {expected} but got {actual}";
                throw new AssertionException(message ?? defaultMessage);
            }
        }

        /// <summary>
        /// Asserts that a value is greater than or equal to another value
        /// </summary>
        /// <typeparam name="T">Type that implements IComparable</typeparam>
        /// <param name="actual">Actual value</param>
        /// <param name="expected">Expected minimum value</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when actual is not greater than or equal to expected</exception>
        public static void IsGreaterThanOrEqual<T>(T actual, T expected, string? message = null) where T : IComparable<T>
        {
            if (actual.CompareTo(expected) < 0)
            {
                string defaultMessage = $"Expected value greater than or equal to {expected} but got {actual}";
                throw new AssertionException(message ?? defaultMessage);
            }
        }

        /// <summary>
        /// Asserts that a value is less than another value
        /// </summary>
        /// <typeparam name="T">Type that implements IComparable</typeparam>
        /// <param name="actual">Actual value</param>
        /// <param name="expected">Expected maximum value</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when actual is not less than expected</exception>
        public static void IsLessThan<T>(T actual, T expected, string? message = null) where T : IComparable<T>
        {
            if (actual.CompareTo(expected) >= 0)
            {
                string defaultMessage = $"Expected value less than {expected} but got {actual}";
                throw new AssertionException(message ?? defaultMessage);
            }
        }

        /// <summary>
        /// Asserts that a value is less than or equal to another value
        /// </summary>
        /// <typeparam name="T">Type that implements IComparable</typeparam>
        /// <param name="actual">Actual value</param>
        /// <param name="expected">Expected maximum value</param>
        /// <param name="message">Message to display if assertion fails</param>
        /// <exception cref="AssertionException">Thrown when actual is not less than or equal to expected</exception>
        public static void IsLessThanOrEqual<T>(T actual, T expected, string? message = null) where T : IComparable<T>
        {
            if (actual.CompareTo(expected) > 0)
            {
                string defaultMessage = $"Expected value less than or equal to {expected} but got {actual}";
                throw new AssertionException(message ?? defaultMessage);
            }
        }
    }

    /// <summary>
    /// Exception thrown when test assertions fail
    /// </summary>
    public class AssertionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the AssertionException class
        /// </summary>
        /// <param name="message">The assertion failure message</param>
        public AssertionException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the AssertionException class
        /// </summary>
        /// <param name="message">The assertion failure message</param>
        /// <param name="innerException">The inner exception</param>
        public AssertionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}