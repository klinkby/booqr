using System.IO;
using Klinkby.Booqr.Api.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Klinkby.Booqr.Api.Tests.Worker;

public class HeartbeatServiceTests
{
    [Fact(Timeout = 5000)]
    public async Task GIVEN_Started_THEN_FileExists()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var fakeTime = new FakeTimeProvider();
        var logger = NullLogger<HeartbeatService>.Instance;

        var service = new HeartbeatService(fakeTime, logger, tempPath);

        try
        {
            // Act
            await service.StartAsync(TestContext.Current.CancellationToken);

            // Assert
            Assert.True(File.Exists(tempPath), "Heartbeat file should exist after StartAsync");
        }
        finally
        {
            await service.StopAsync(TestContext.Current.CancellationToken);
            service.Dispose();
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    [Fact(Timeout = 5000)]
    public async Task GIVEN_Started_WHEN_TimeAdvancedByInterval_THEN_MtimeRefreshed()
    {
        // Arrange
        var tempPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var fakeTime = new FakeTimeProvider(new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero));
        var logger = NullLogger<HeartbeatService>.Instance;

        var service = new HeartbeatService(fakeTime, logger, tempPath);

        try
        {
            // Act - Start service and record initial state
            await service.StartAsync(TestContext.Current.CancellationToken);
            var initialMtime = File.GetLastWriteTimeUtc(tempPath);

            // Advance time by the heartbeat interval (30 seconds)
            fakeTime.Advance(TimeSpan.FromSeconds(30));

            // Assert - The file's last-write time should match the advanced fake time
            var newMtime = File.GetLastWriteTimeUtc(tempPath);
            Assert.True(
                newMtime > initialMtime,
                $"File mtime should advance: initial={initialMtime:O}, new={newMtime:O}");

            // The new mtime should match the advanced fake time (within 1 second tolerance for system clock variance)
            var expectedMtime = fakeTime.GetUtcNow().UtcDateTime;
            Assert.True(
                Math.Abs((newMtime - expectedMtime).TotalSeconds) < 1,
                $"File mtime should match advanced fake time: expected={expectedMtime:O}, actual={newMtime:O}");
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
            service.Dispose();
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }
}
