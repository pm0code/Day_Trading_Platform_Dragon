using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;
using TradingPlatform.Core.Models;

namespace TradingPlatform.UnitTests.Extensions
{
    /// <summary>
    /// FluentAssertions extensions for TradingResult
    /// </summary>
    public static class TradingResultExtensions
    {
        public static TradingResultAssertions<T> Should<T>(this TradingResult<T> instance)
        {
            return new TradingResultAssertions<T>(instance);
        }

        public static TradingResultAssertions Should(this TradingResult instance)
        {
            return new TradingResultAssertions(instance);
        }
    }

    public class TradingResultAssertions<T> : ReferenceTypeAssertions<TradingResult<T>, TradingResultAssertions<T>>
    {
        public TradingResultAssertions(TradingResult<T> instance) : base(instance)
        {
        }

        protected override string Identifier => "TradingResult";

        public AndConstraint<TradingResultAssertions<T>> BeSuccess(string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(Subject.IsSuccess)
                .FailWith("Expected TradingResult to be successful{reason}, but it was not. Error: {0}", 
                    Subject.Error?.Message ?? "No error message");

            return new AndConstraint<TradingResultAssertions<T>>(this);
        }

        public AndConstraint<TradingResultAssertions<T>> BeFailure(string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(!Subject.IsSuccess)
                .FailWith("Expected TradingResult to be a failure{reason}, but it was successful");

            return new AndConstraint<TradingResultAssertions<T>>(this);
        }

        public AndConstraint<TradingResultAssertions<T>> HaveValue(T expectedValue, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(Subject.IsSuccess)
                .FailWith("Expected TradingResult to have a value{reason}, but it was not successful")
                .Then
                .ForCondition(Equals(Subject.Value, expectedValue))
                .FailWith("Expected TradingResult value to be {0}{reason}, but found {1}", 
                    expectedValue, Subject.Value);

            return new AndConstraint<TradingResultAssertions<T>>(this);
        }

        public AndConstraint<TradingResultAssertions<T>> HaveValueMatching(Func<T, bool> predicate, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(Subject.IsSuccess)
                .FailWith("Expected TradingResult to have a value{reason}, but it was not successful")
                .Then
                .ForCondition(Subject.Value != null && predicate(Subject.Value))
                .FailWith("Expected TradingResult value to match predicate{reason}, but it did not");

            return new AndConstraint<TradingResultAssertions<T>>(this);
        }

        public AndConstraint<TradingResultAssertions<T>> HaveError(string expectedMessage, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(!Subject.IsSuccess)
                .FailWith("Expected TradingResult to have an error{reason}, but it was successful")
                .Then
                .ForCondition(Subject.Error != null && Subject.Error.Message.Contains(expectedMessage))
                .FailWith("Expected TradingResult error to contain '{0}'{reason}, but found '{1}'", 
                    expectedMessage, Subject.Error?.Message ?? "No error");

            return new AndConstraint<TradingResultAssertions<T>>(this);
        }

        public AndConstraint<TradingResultAssertions<T>> HaveErrorCode(string expectedCode, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(!Subject.IsSuccess)
                .FailWith("Expected TradingResult to have an error{reason}, but it was successful")
                .Then
                .ForCondition(Subject.Error != null && Subject.Error.Code == expectedCode)
                .FailWith("Expected TradingResult error code to be '{0}'{reason}, but found '{1}'", 
                    expectedCode, Subject.Error?.Code ?? "No code");

            return new AndConstraint<TradingResultAssertions<T>>(this);
        }
    }

    public class TradingResultAssertions : ReferenceTypeAssertions<TradingResult, TradingResultAssertions>
    {
        public TradingResultAssertions(TradingResult instance) : base(instance)
        {
        }

        protected override string Identifier => "TradingResult";

        public AndConstraint<TradingResultAssertions> BeSuccess(string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(Subject.IsSuccess)
                .FailWith("Expected TradingResult to be successful{reason}, but it was not. Error: {0}", 
                    Subject.Error?.Message ?? "No error message");

            return new AndConstraint<TradingResultAssertions>(this);
        }

        public AndConstraint<TradingResultAssertions> BeFailure(string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(!Subject.IsSuccess)
                .FailWith("Expected TradingResult to be a failure{reason}, but it was successful");

            return new AndConstraint<TradingResultAssertions>(this);
        }

        public AndConstraint<TradingResultAssertions> HaveError(string expectedMessage, string because = "", params object[] becauseArgs)
        {
            Execute.Assertion
                .BecauseOf(because, becauseArgs)
                .ForCondition(!Subject.IsSuccess)
                .FailWith("Expected TradingResult to have an error{reason}, but it was successful")
                .Then
                .ForCondition(Subject.Error != null && Subject.Error.Message.Contains(expectedMessage))
                .FailWith("Expected TradingResult error to contain '{0}'{reason}, but found '{1}'", 
                    expectedMessage, Subject.Error?.Message ?? "No error");

            return new AndConstraint<TradingResultAssertions>(this);
        }
    }
}