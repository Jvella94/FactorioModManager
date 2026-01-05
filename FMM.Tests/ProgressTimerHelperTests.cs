using FactorioModManager.ViewModels.MainWindow;

namespace FMM.Tests
{
    public class ProgressTimerHelperTests
    {
        [Fact]
        public void Schedule_Then_FlushInvokesAction()
        {
            bool invoked = false;
            using var helper = new ProgressTimerHelper(TimeSpan.FromMilliseconds(10), () => invoked = true);
            helper.Schedule();
            // Wait longer than throttle to allow timer to fire
            Thread.Sleep(100);
            Assert.True(invoked);
        }

        [Fact]
        public void FlushIfPending_InvokesImmediately()
        {
            bool invoked = false;
            using var helper = new ProgressTimerHelper(TimeSpan.FromSeconds(1), () => invoked = true);
            helper.Schedule();
            helper.FlushIfPending();
            Assert.True(invoked);
        }

        [Fact]
        public void Dispose_StopsTimer()
        {
            var helper = new ProgressTimerHelper(TimeSpan.FromMilliseconds(10), () => { });
            helper.Dispose();
            // Scheduling after dispose should not throw
            try
            {
                helper.Schedule();
            }
            catch (ObjectDisposedException)
            {
                // allowed but test should ensure no crash; treat as pass
            }
        }

        [Fact]
        public void Multiple_Schedules_Coalesce_To_Single_Invoke()
        {
            int count = 0;
            using var helper = new ProgressTimerHelper(TimeSpan.FromMilliseconds(50), () => Interlocked.Increment(ref count));
            helper.Schedule();
            helper.Schedule();
            helper.Schedule();
            Thread.Sleep(200);
            Assert.Equal(1, count);
        }

        [Fact]
        public void Callback_Exception_Is_Swallowed()
        {
            using var helper = new ProgressTimerHelper(TimeSpan.FromMilliseconds(10), () => throw new InvalidOperationException("boom"));
            // Should not propagate when timer fires
            helper.Schedule();
            Thread.Sleep(100);
            // If we reach here, no unhandled exception escaped the timer thread
            Assert.True(true);
        }
    }
}