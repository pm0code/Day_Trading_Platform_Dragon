using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using TradingPlatform.Core.Canonical;
using TradingPlatform.Testing;

namespace TradingPlatform.Tests.Canonical
{
    /// <summary>
    /// Comprehensive unit tests for CanonicalProgressReporter following canonical test patterns
    /// </summary>
    public class CanonicalProgressReporterTests : CanonicalTestBase
    {
        public CanonicalProgressReporterTests(ITestOutputHelper output) : base(output)
        {
        }

        #region Constructor and Basic Usage Tests

        [Fact]
        public async Task Constructor_Should_InitializeWithOperationName()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing progress reporter initialization");

                // Act
                using var progress = new CanonicalProgressReporter("Test Operation");

                // Assert
                var report = progress.GetCurrentReport();
                AssertWithLogging(
                    report.OperationName,
                    "Test Operation",
                    "Operation name should be set"
                );
                AssertWithLogging(
                    report.ProgressPercentage,
                    0.0,
                    "Initial progress should be 0"
                );
                AssertWithLogging(
                    report.IsCompleted,
                    false,
                    "Should not be completed initially"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Constructor_Should_AcceptCustomProgressHandler()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing custom progress handler");

                // Arrange
                var progressReports = new List<ProgressReport>();
                Action<ProgressReport> customHandler = report => progressReports.Add(report);

                // Act
                using var progress = new CanonicalProgressReporter(
                    "Custom Handler Test",
                    customHandler
                );

                progress.ReportProgress(50, "Halfway");

                // Assert
                AssertWithLogging(
                    progressReports.Count,
                    1,
                    "Custom handler should receive progress report"
                );
                AssertWithLogging(
                    progressReports[0].ProgressPercentage,
                    50.0,
                    "Progress percentage should be correct"
                );

                await Task.CompletedTask;
            });
        }

        #endregion

        #region Progress Reporting Tests

        [Fact]
        public async Task ReportProgress_Should_UpdateProgressPercentage()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing progress percentage updates");

                // Arrange
                using var progress = new CanonicalProgressReporter("Progress Test");

                // Act & Assert - multiple progress updates
                progress.ReportProgress(25, "Quarter done");
                var report1 = progress.GetCurrentReport();
                AssertWithLogging(report1.ProgressPercentage, 25.0, "Should be at 25%");
                AssertWithLogging(report1.Message, "Quarter done", "Message should be updated");

                progress.ReportProgress(50, "Half done");
                var report2 = progress.GetCurrentReport();
                AssertWithLogging(report2.ProgressPercentage, 50.0, "Should be at 50%");

                progress.ReportProgress(100, "Complete");
                var report3 = progress.GetCurrentReport();
                AssertWithLogging(report3.ProgressPercentage, 100.0, "Should be at 100%");

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ReportProgress_Should_ClampPercentageToValidRange()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing progress percentage clamping");

                // Arrange
                using var progress = new CanonicalProgressReporter("Clamp Test");

                // Act & Assert - test negative
                progress.ReportProgress(-50, "Negative");
                var report1 = progress.GetCurrentReport();
                AssertWithLogging(report1.ProgressPercentage, 0.0, "Negative should clamp to 0");

                // Act & Assert - test over 100
                progress.ReportProgress(150, "Over 100");
                var report2 = progress.GetCurrentReport();
                AssertWithLogging(report2.ProgressPercentage, 100.0, "Over 100 should clamp to 100");

                await Task.CompletedTask;
            });
        }

        #endregion

        #region Staged Progress Tests

        [Fact]
        public async Task ReportStageProgress_Should_CalculateCorrectPercentage()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing staged progress calculation");

                // Arrange
                using var progress = new CanonicalProgressReporter("Staged Operation", totalStages: 4);

                // Act & Assert - progress through stages
                progress.ReportStageProgress(1, 0, "Starting stage 1");
                var report1 = progress.GetCurrentReport();
                AssertWithLogging(report1.ProgressPercentage, 0.0, "Stage 1 start should be 0%");

                progress.ReportStageProgress(1, 100, "Completed stage 1");
                var report2 = progress.GetCurrentReport();
                AssertWithLogging(report2.ProgressPercentage, 25.0, "Stage 1 complete should be 25%");

                progress.ReportStageProgress(3, 50, "Halfway through stage 3");
                var report3 = progress.GetCurrentReport();
                AssertWithLogging(report3.ProgressPercentage, 62.5, "Stage 3 halfway should be 62.5%");

                progress.ReportStageProgress(4, 100, "All done");
                var report4 = progress.GetCurrentReport();
                AssertWithLogging(report4.ProgressPercentage, 100.0, "All stages complete should be 100%");

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ReportStageProgress_Should_HandleInvalidStageNumbers()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing invalid stage number handling");

                // Arrange
                using var progress = new CanonicalProgressReporter("Stage Validation", totalStages: 3);

                // Act & Assert - stage 0 (invalid)
                progress.ReportStageProgress(0, 50, "Invalid stage 0");
                var report1 = progress.GetCurrentReport();
                AssertWithLogging(report1.ProgressPercentage, 0.0, "Invalid stage 0 should not update progress");

                // Act & Assert - stage beyond total (invalid)
                progress.ReportStageProgress(5, 50, "Invalid stage 5");
                var report2 = progress.GetCurrentReport();
                AssertWithLogging(report2.ProgressPercentage, 0.0, "Invalid stage 5 should not update progress");

                await Task.CompletedTask;
            });
        }

        #endregion

        #region Completion Tests

        [Fact]
        public async Task Complete_Should_MarkAsCompleted()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing completion marking");

                // Arrange
                using var progress = new CanonicalProgressReporter("Completion Test");

                // Act
                progress.Complete("Operation finished successfully");
                var report = progress.GetCurrentReport();

                // Assert
                AssertWithLogging(report.IsCompleted, true, "Should be marked as completed");
                AssertWithLogging(report.ProgressPercentage, 100.0, "Progress should be 100%");
                AssertWithLogging(
                    report.Message,
                    "Operation finished successfully",
                    "Completion message should be set"
                );
                AssertConditionWithLogging(
                    report.CompletedAt.HasValue,
                    "Completion time should be set"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Complete_Should_PreventFurtherUpdates()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing updates after completion");

                // Arrange
                using var progress = new CanonicalProgressReporter("No Updates After Complete");
                progress.Complete("Done");

                // Act - try to update after completion
                progress.ReportProgress(50, "Should not update");
                var report = progress.GetCurrentReport();

                // Assert
                AssertWithLogging(report.ProgressPercentage, 100.0, "Progress should remain at 100%");
                AssertWithLogging(report.Message, "Done", "Message should not change after completion");
                AssertWithLogging(report.IsCompleted, true, "Should remain completed");

                await Task.CompletedTask;
            });
        }

        #endregion

        #region Time Estimation Tests

        [Fact]
        public async Task GetCurrentReport_Should_EstimateRemainingTime()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing time estimation");

                // Arrange
                using var progress = new CanonicalProgressReporter("Time Estimation Test");

                // Act - simulate progress over time
                progress.ReportProgress(25, "25% done");
                await Task.Delay(100); // Simulate work

                progress.ReportProgress(50, "50% done");
                var report = progress.GetCurrentReport();

                // Assert
                AssertConditionWithLogging(
                    report.EstimatedTimeRemaining.HasValue,
                    "Should have time remaining estimate"
                );
                AssertConditionWithLogging(
                    report.EstimatedTimeRemaining?.TotalMilliseconds > 0,
                    "Estimated time should be positive"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task GetCurrentReport_Should_NotEstimateTimeAtZeroProgress()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing time estimation at zero progress");

                // Arrange
                using var progress = new CanonicalProgressReporter("No Time Estimate");

                // Act
                var report = progress.GetCurrentReport();

                // Assert
                AssertConditionWithLogging(
                    !report.EstimatedTimeRemaining.HasValue,
                    "Should not estimate time at 0% progress"
                );

                await Task.CompletedTask;
            });
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task ReportError_Should_SetErrorState()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing error reporting");

                // Arrange
                using var progress = new CanonicalProgressReporter("Error Test");
                var exception = new InvalidOperationException("Test error");

                // Act
                progress.ReportError(exception, "Operation failed");
                var report = progress.GetCurrentReport();

                // Assert
                AssertWithLogging(report.HasError, true, "Should indicate error state");
                AssertWithLogging(report.ErrorMessage, "Operation failed", "Error message should be set");
                AssertNotNull(report.LastException, "Exception should be stored");
                AssertWithLogging(
                    report.LastException?.Message,
                    "Test error",
                    "Exception message should match"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task ReportError_Should_NotOverwriteProgress()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing error reporting preserves progress");

                // Arrange
                using var progress = new CanonicalProgressReporter("Error Progress Test");
                progress.ReportProgress(75, "Almost done");

                // Act
                progress.ReportError(new Exception("Error occurred"), "Failed at 75%");
                var report = progress.GetCurrentReport();

                // Assert
                AssertWithLogging(report.ProgressPercentage, 75.0, "Progress should be preserved");
                AssertWithLogging(report.HasError, true, "Should be in error state");

                await Task.CompletedTask;
            });
        }

        #endregion

        #region Disposal Tests

        [Fact]
        public async Task Dispose_Should_CompleteIfNotAlreadyCompleted()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing disposal auto-completion");

                // Arrange
                var progress = new CanonicalProgressReporter("Disposal Test");
                progress.ReportProgress(50, "Halfway");

                // Act
                progress.Dispose();
                var report = progress.GetCurrentReport();

                // Assert
                AssertWithLogging(report.IsCompleted, true, "Should be completed on disposal");
                AssertConditionWithLogging(
                    report.Message?.Contains("disposed") ?? false,
                    "Should indicate disposal in message"
                );

                await Task.CompletedTask;
            });
        }

        [Fact]
        public async Task Dispose_Should_NotAffectAlreadyCompletedProgress()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing disposal of completed progress");

                // Arrange
                var progress = new CanonicalProgressReporter("Already Complete");
                progress.Complete("Finished normally");
                var messageBeforeDispose = progress.GetCurrentReport().Message;

                // Act
                progress.Dispose();
                var report = progress.GetCurrentReport();

                // Assert
                AssertWithLogging(
                    report.Message,
                    messageBeforeDispose,
                    "Completion message should not change"
                );

                await Task.CompletedTask;
            });
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task ProgressReporter_Should_BeThreadSafe()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing thread safety");

                // Arrange
                using var progress = new CanonicalProgressReporter("Thread Safety Test");
                var tasks = new List<Task>();
                var random = new Random();

                // Act - multiple threads updating progress
                for (int i = 0; i < 10; i++)
                {
                    int threadId = i;
                    tasks.Add(Task.Run(() =>
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            progress.ReportProgress(
                                random.Next(0, 101),
                                $"Update from thread {threadId}"
                            );
                            Thread.Sleep(1);
                        }
                    }));
                }

                await Task.WhenAll(tasks);

                // Assert - should not throw and should have valid state
                var finalReport = progress.GetCurrentReport();
                AssertConditionWithLogging(
                    finalReport.ProgressPercentage >= 0 && finalReport.ProgressPercentage <= 100,
                    "Progress should be in valid range after concurrent updates"
                );
                AssertNotNull(finalReport.Message, "Should have a message");
            });
        }

        #endregion

        #region Integration with IProgress<T> Tests

        [Fact]
        public async Task AsIProgress_Should_WorkWithStandardInterface()
        {
            await ExecuteTestAsync(async () =>
            {
                LogTestStep("Testing IProgress<T> interface compatibility");

                // Arrange
                var progressReports = new List<ProgressReport>();
                var progress = new CanonicalProgressReporter(
                    "IProgress Test",
                    report => progressReports.Add(report)
                );

                // Act - use as IProgress<ProgressReport>
                IProgress<ProgressReport> iProgress = progress.AsIProgress();
                iProgress.Report(new ProgressReport
                {
                    OperationName = "IProgress Test",
                    ProgressPercentage = 33.3,
                    Message = "Via IProgress"
                });

                // Assert
                AssertWithLogging(progressReports.Count, 1, "Should receive progress report");
                AssertWithLogging(
                    progressReports[0].ProgressPercentage,
                    33.3,
                    "Progress should be updated via IProgress"
                );

                await Task.CompletedTask;
            });
        }

        #endregion
    }
}